using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 幻影星球：ai[0]=宿主 whoAmI，ai[1]=模式 0持握齐射/1引力井环绕，ai[2]=模式0的齐射延迟帧。
    /// 持握期贴宿主呼吸，齐射拍各端按宿主目标确定性放飞。
    /// 本体=MLordOrb.fx"微型蚀月"（蚀盘暗面遮挡+旋涡虹膜+新月冕环），
    /// 凝聚显形→点火过曝→飞行拖尾→碎裂余痕四相齐备，着色器缺席走旧三层软光回退
    /// </summary>
    internal class MLordOrbProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float LaunchSpeed = 12.5f;
        private const float MaxSpeed = 19f;
        /// <summary>移速统一倍率：齐射/环绕两模式一并翻倍（生成侧速度数值不改口径）</summary>
        private const float SpeedBoost = 2f;

        /// <summary>球盘占画布半径，与 MLordOrb.fx 头部 DiscR 契约同步</summary>
        private const float DiscR = 0.42f;
        /// <summary>蚀盘可见半径px（34px 判定盒藏于可见体内）</summary>
        private const float VisRadius = 28f;
        /// <summary>井轨凝聚帧长：与 CanDamage 的 Timer&gt;12 无伤窗对齐（伤害窗=视觉窗）</summary>
        private const int WellFormTime = 12;
        /// <summary>持握凝聚帧长：远短于最短齐射延迟，放飞前必然满形</summary>
        private const int HeldFormTime = 18;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float Launched => ref Projectile.localAI[1];
        private NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private bool WellMode => Projectile.ai[1] == 1f;

        private Vector2 heldOffset;
        private bool offsetCaptured;
        /// <summary>放飞点火过曝量（表现层，逐帧衰减，纯白驻留 ≤2 帧）</summary>
        private float launchFlash;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 640;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.045f + Projectile.velocity.Length() * 0.004f;
            Lighting.AddLight(Projectile.Center, MLordDirector.Phantasmal.ToVector3() * 0.45f);

            if (WellMode) {
                WellOrbitAI();
            }
            else {
                HeldVolleyAI();
            }

            //点火过曝衰减
            launchFlash = launchFlash > 0.02f ? launchFlash * 0.55f : 0f;

            if (VaultUtils.isServer) {
                return;
            }

            if (Launched == 0f && !WellMode) {
                //持握凝聚：星尘向心汇聚（凝聚即预告），满形后转稀疏环境闪
                bool forming = Timer < HeldFormTime;
                if (forming ? Main.rand.NextBool(2) : Main.rand.NextBool(9)) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(28f, 58f);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(from, (Projectile.Center - from) * 0.13f,
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 16));
                }
            }
            else if (Main.rand.NextFloat() < 0.06f + Projectile.velocity.Length() * 0.007f) {
                //飞行星屑剥落 ∝ 速度
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Projectile.velocity * Main.rand.NextFloat(0.06f, 0.12f),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.DeepViolet, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        /// <summary>
        /// 持握→齐射：贴宿主呼吸，齐射由权威端裁定写速度并 netUpdate 广播，
        /// 客户端凭"速度非零"识别放飞（避免两端各自预判目标造成弹道分叉）
        /// </summary>
        private void HeldVolleyAI() {
            NPC host = Host;
            int launchDelay = (int)Projectile.ai[2];

            if (Launched == 0f) {
                //客户端：收到权威端速度即视作已放飞
                if (Projectile.velocity.LengthSquared() > 1f) {
                    Launched = 1f;
                    IgniteLaunch();
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 6 }, Projectile.Center);
                    }
                    return;
                }

                if (!host.Alives()) {
                    //宿主没了：权威端就地放飞
                    if (!VaultUtils.isClient) {
                        Projectile.velocity = Vector2.UnitY * (LaunchSpeed * SpeedBoost);
                        Projectile.netUpdate = true;
                    }
                    return;
                }

                if (!offsetCaptured) {
                    heldOffset = Projectile.Center - host.Center;
                    offsetCaptured = true;
                }

                //持握呼吸：轻微离心张合
                float breath = 1f + 0.06f * (float)Math.Sin(Timer * 0.11f + Projectile.whoAmI * 0.7f);
                Projectile.Center = host.Center + heldOffset * breath;
                Projectile.velocity = Vector2.Zero;

                //权威端裁定放飞
                if (!VaultUtils.isClient && Timer >= launchDelay) {
                    Launched = 1f;
                    int targetIndex = host.target;
                    Vector2 aim = Vector2.UnitY;
                    if (targetIndex >= 0 && targetIndex < Main.maxPlayers) {
                        Player target = Main.player[targetIndex];
                        if (target.active && !target.dead) {
                            aim = (target.Center + target.velocity * 11f - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        }
                    }
                    Projectile.velocity = aim * (LaunchSpeed * SpeedBoost);
                    Projectile.netUpdate = true;
                    IgniteLaunch();
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 6 }, Projectile.Center);
                    }
                }
                return;
            }

            //飞行段复合加速，绝不匀速
            if (Projectile.velocity.Length() < MaxSpeed * SpeedBoost) {
                Projectile.velocity *= 1.013f;
            }
        }

        /// <summary>放飞点火：过曝 pop + 沿瞄向星屑散射（纯表现，服务端只置量）</summary>
        private void IgniteLaunch() {
            launchFlash = 1f;
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f)) * Main.rand.NextFloat(3.5f, 9f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + aim * 8f, vel,
                    Color.Lerp(MLordDirector.MoonWhite, MLordDirector.Phantasmal, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        /// <summary>引力井环绕：向最近引力井加速，井灭后直线甩出。轨道对初值敏感，权威端周期广播矫偏</summary>
        private void WellOrbitAI() {
            //点火倍率：首帧按生成包初速翻倍（各端确定性同步）
            if (Timer == 1f) {
                Projectile.velocity *= SpeedBoost;
            }
            Projectile wellProj = FindNearestWell();
            if (wellProj != null) {
                Vector2 toWell = wellProj.Center - Projectile.Center;
                float dist = Math.Max(toWell.Length(), 60f);
                //平方衰减向心力，近处收紧——向心力按倍率平方缩放：轨道半径不变，角速度翻倍
                float gravity = MathHelper.Clamp(9000f / (dist * dist) * 6f, 0.08f, 0.8f) * (SpeedBoost * SpeedBoost);
                Projectile.velocity += toWell.SafeNormalize(Vector2.Zero) * gravity;
                if (Projectile.velocity.Length() > 17f * SpeedBoost) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * (17f * SpeedBoost);
                }
                //椭圆轨道混沌敏感：权威端 30 帧一次位置矫偏
                if (!VaultUtils.isClient && (int)Timer % 30 == 0) {
                    Projectile.netUpdate = true;
                }
            }
            else if (Timer > 40f && Projectile.timeLeft > 70) {
                //井没了：甩出后限时消散
                Projectile.timeLeft = 70;
            }
        }

        private Projectile FindNearestWell() {
            int type = ModContent.ProjectileType<MLordGravityWellProj>();
            Projectile best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type) {
                    continue;
                }
                float dist = Projectile.DistanceSQ(p.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = p;
                }
            }
            return best;
        }

        //持握期无伤，出手后判定
        public override bool? CanDamage() {
            if (WellMode) {
                return Timer > 12f ? null : false;
            }
            return Launched == 1f ? null : false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            MLordScreenFX.StarBurst(Projectile.Center, 0.55f, 7);
            //蚀月碎裂：暗紫碎屑重力弧外抛，余痕活得比弹体久
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + vel * 3f, vel,
                    Color.Lerp(MLordDirector.DeepViolet, MLordDirector.Phantasmal, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(18, 28));
            }
            SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 6 }, Projectile.Center);
        }

        /// <summary>凝聚进度 0~1：井轨前 12 帧 / 持握前 18 帧，均落在既有无伤窗内</summary>
        private float FormProgress() {
            if (WellMode) {
                return MathHelper.Clamp(Timer / (float)WellFormTime, 0f, 1f);
            }
            if (Launched == 1f) {
                return 1f;
            }
            return MathHelper.Clamp(Timer / (float)HeldFormTime, 0f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.MLordOrb?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                DrawFallback();
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            float speed = Projectile.velocity.Length();
            float form = FormProgress();

            //速度拉伸与新月方位：飞行=船首新月领航，持握=光缘随自转巡游（旋转读点）
            float stretch = MathHelper.Clamp(speed * 0.02f, 0f, 0.55f);
            Vector2 stretchDir = speed > 2f ? Projectile.velocity.SafeNormalize(Vector2.UnitY) : Vector2.UnitY;
            Vector2 crescentDir = speed > 2f ? stretchDir : Projectile.rotation.ToRotationVector2();

            //uniform 全参数重设（共享 shader 的设备全局残留陷阱）
            effect.CurrentTechnique = effect.Techniques["TechEclipse"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.37f);
            effect.Parameters["uAlpha"]?.SetValue(1f);
            effect.Parameters["uForm"]?.SetValue(form);
            effect.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(launchFlash, 0f, 1f));
            effect.Parameters["uSpin"]?.SetValue(0.9f + speed * 0.03f);
            effect.Parameters["uStretchDir"]?.SetValue(stretchDir);
            effect.Parameters["uStretch"]?.SetValue(stretch);
            effect.Parameters["uCrescentDir"]?.SetValue(crescentDir);
            effect.Parameters["uColDark"]?.SetValue(OrbDark.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(MLordDirector.DeepViolet.ToVector3());
            effect.Parameters["uColMain"]?.SetValue(MLordDirector.Phantasmal.ToVector3());
            effect.Parameters["uColBright"]?.SetValue(MLordDirector.MoonWhite.ToVector3());

            //球盘=画布半径 0.42，quad 按可见半径折算（与 .fx 头部契约同步）；凝聚期缩入
            float quadPx = VisRadius / DiscR * 2f * MathHelper.Lerp(0.62f, 1f, VaultUtils.EaseOutCubic(form));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            //拖尾=本体同材质残影（契约5：横轴比 0.55~0.85）；Immediate 每次 Draw 重 Apply，逐影变参生效
            if (speed > 4f) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    //trail 缓存未填满前是零向量，画出去会闪到世界原点
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    effect.Parameters["uAlpha"]?.SetValue(0.10f + 0.34f * k);
                    effect.Parameters["uFlash"]?.SetValue(0f);
                    effect.CurrentTechnique.Passes[0].Apply();
                    Vector2 gpos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float gquad = quadPx * MathHelper.Lerp(0.55f, 0.85f, k);
                    sb.Draw(canvas, gpos, null, Color.White, 0f, canvas.Size() * 0.5f,
                        gquad / canvas.Width, SpriteEffects.None, 0f);
                }
                //残影跑完还原本体参数
                effect.Parameters["uAlpha"]?.SetValue(1f);
                effect.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(launchFlash, 0f, 1f));
                effect.CurrentTechnique.Passes[0].Apply();
            }

            //本体
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadPx / canvas.Width, SpriteEffects.None, 0f);

            sb.End();
            //归还噪声槽（帧内邻居防串）
            gd.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        //―――― 着色器缺席回退：旧三层软光画法 ――――

        /// <summary>星球暗鞘色（真 alpha 遮挡层，契约4.4：暗层禁走加色）</summary>
        private static readonly Color OrbDark = new(20, 12, 50);

        /// <summary>回退本体三层：暗紫外鞘（真 alpha 剪影）+ 深紫晕 + 幻影芯（加色）</summary>
        private static void DrawOrbBody(Texture2D glow, Texture2D star, Vector2 screenPos,
            float bodyRot, Vector2 bodyScale, float alpha, float starRot) {
            Main.EntitySpriteDraw(glow, screenPos, null, OrbDark * (0.88f * alpha),
                bodyRot, glow.Size() / 2f, bodyScale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.DeepViolet with { A = 0 } * (0.85f * alpha),
                bodyRot, glow.Size() / 2f, bodyScale * 1.7f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.Phantasmal with { A = 0 } * alpha,
                bodyRot, glow.Size() / 2f, bodyScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenPos, null, MLordDirector.MoonWhite with { A = 0 } * (0.75f * alpha),
                starRot, star.Size() / 2f, 0.24f * alpha, SpriteEffects.None, 0);
        }

        private void DrawFallback() {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }

            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float phase = 0.82f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 1.3f);

            //速度各向异性拉伸主体
            float stretch = MathHelper.Clamp(speed * 0.02f, 0f, 0.55f);
            Vector2 bodyScale = new Vector2(0.34f * (1f + stretch), 0.34f * (1f - stretch * 0.4f));
            float bodyRot = speed > 2f ? Projectile.velocity.ToRotation() : Projectile.rotation;

            if (speed > 4f) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    DrawOrbBody(glow, star, pos, bodyRot,
                        bodyScale * MathHelper.Lerp(0.55f, 0.85f, k), (0.1f + 0.38f * k) * phase,
                        Projectile.rotation);
                }
            }

            DrawOrbBody(glow, star, screenPos, bodyRot, bodyScale, phase, Projectile.rotation);
        }
    }
}
