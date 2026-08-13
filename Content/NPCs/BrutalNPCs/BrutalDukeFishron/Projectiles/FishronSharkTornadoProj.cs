using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 鲨鱼龙卷地形物：驻场行走的水龙卷，吞吸爆裂气泡升级，
    /// 升级后周期甩出鲨鱼龙。ai[0]=已吞气泡数 ai[1]=层级(0/1) localAI[0]=寿命计时
    /// </summary>
    internal class FishronSharkTornadoProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int TornadoDamage = 42;
        /// <summary>基础寿命</summary>
        private const int BaseLife = 1500;
        /// <summary>升格追加寿命</summary>
        private const int TierBonusLife = 300;
        /// <summary>起身帧数</summary>
        private const int RiseTime = 42;
        /// <summary>消散帧数</summary>
        private const int FadeTime = 40;
        /// <summary>升格所需气泡数</summary>
        private const int ChargeToUpgrade = 5;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        private ref float Charge => ref Projectile.ai[0];
        private ref float Tier => ref Projectile.ai[1];
        /// <summary>寿命刷新戳（ai[2] 随包同步）：变化即把 timeLeft 抬回下限</summary>
        private ref float RefreshStamp => ref Projectile.ai[2];
        private ref float LifeTimer => ref Projectile.localAI[0];
        private ref float SharkronTimer => ref Projectile.localAI[1];
        /// <summary>本地已消化的刷新戳</summary>
        private ref float SeenStamp => ref Projectile.localAI[2];

        /// <summary>刷新戳抬升的寿命下限</summary>
        internal const int RefreshLife = 900;

        /// <summary>
        /// 服务端调用：打刷新戳延长寿命。timeLeft 不在弹幕同步包里，
        /// 直改只有服务端生效，客户端会按旧寿命提前杀掉本地副本（隐形龙卷）
        /// </summary>
        internal static void RefreshLifetime(Projectile proj) {
            proj.ai[2] += 1f;
            proj.netUpdate = true;
        }

        private float seed;

        private bool Upgraded => Tier >= 1f;
        private float ColumnWidth => Upgraded ? 190f : 150f;
        private float ColumnHeight => Upgraded ? 620f : 460f;
        private float WalkSpeed => Upgraded ? 2.9f : 2.1f;

        /// <summary>起身/消散包络</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp(LifeTimer / RiseTime, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);
                return Math.Min(rise * rise, fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 460;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BaseLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void SetStaticDefaults() {
            //绘制 quad 宽出命中盒一倍余：本体近出屏时不允许整柱瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 520;
        }

        /// <summary>起身/消散期判定关闭</summary>
        private bool HitWindowOpen => LifeTimer >= RiseTime * 0.6f && Projectile.timeLeft >= FadeTime;

        public override bool CanHitPlayer(Player target) => HitWindowOpen;

        public override void AI() {
            LifeTimer++;
            seed = Projectile.whoAmI * 0.617f;

            //升格落地（尺寸+加寿）由同步的 ai[1] 驱动，所有端各执行一次——
            //width/timeLeft 不入同步包，只在服务端改会让客户端错位并提前消亡
            if (Upgraded && Projectile.width != (int)ColumnWidth) {
                Projectile.timeLeft += TierBonusLife;
                Resize();
            }
            //寿命刷新戳落地（所有端）
            if (SeenStamp != RefreshStamp) {
                SeenStamp = RefreshStamp;
                Projectile.timeLeft = Math.Max(Projectile.timeLeft, RefreshLife);
            }

            //起身期无伤害
            Projectile.damage = HitWindowOpen ? TornadoDamage : 0;

            //贴地行走：向最近玩家缓步推进
            Player nearest = FindNearestPlayer();
            float dir = 0f;
            if (nearest != null && LifeTimer > RiseTime) {
                dir = Math.Sign(nearest.Center.X - Projectile.Center.X);
                Projectile.position.X += dir * WalkSpeed * Envelope;
            }

            //足底吸附地表，攀爬限速
            Vector2 ground = FishronMotionFX.FindSurfaceBelow(
                new Vector2(Projectile.Center.X, Projectile.Center.Y - ColumnHeight * 0.2f), out _);
            float targetBottom = ground.Y + 8f;
            float currentBottom = Projectile.position.Y + Projectile.height;
            float shift = MathHelper.Clamp(targetBottom - currentBottom, -9f, 9f);
            Projectile.position.Y += shift;

            //吞吸气泡升级（服务端裁决落旗，尺寸/加寿由上方全端落地块承接）
            if (!VaultUtils.isClient) {
                AbsorbBubbles();
                if (!Upgraded && Charge >= ChargeToUpgrade) {
                    Tier = 1f;
                    Projectile.netUpdate = true;
                }
                //升格后周期甩出鲨鱼龙
                if (Upgraded && LifeTimer > RiseTime && Projectile.timeLeft > FadeTime) {
                    SharkronTimer++;
                    if (SharkronTimer >= 110 && CountSharkrons() < 4 && nearest != null) {
                        SharkronTimer = 0;
                        LaunchSharkron(nearest);
                    }
                }
                //周期位置校正
                if (Main.GameUpdateCount % 30 == 0) {
                    Projectile.netUpdate = true;
                }
            }

            UpdateVisuals(dir);
        }

        private void Resize() {
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            Projectile.width = (int)ColumnWidth;
            Projectile.height = (int)ColumnHeight;
            Projectile.position = new Vector2(bottom.X - Projectile.width / 2f, bottom.Y - Projectile.height);
        }

        private Player FindNearestPlayer() {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (var p in Main.ActivePlayers) {
                if (p.dead) {
                    continue;
                }
                float d = Vector2.DistanceSquared(p.Center, Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>拉拽并吞掉靠近的爆裂气泡，转化为龙卷成长</summary>
        private void AbsorbBubbles() {
            foreach (var n in Main.ActiveNPCs) {
                if (n.type != NPCID.DetonatingBubble) {
                    continue;
                }
                float dist = Vector2.Distance(n.Center, Projectile.Center);
                if (dist > 240f) {
                    continue;
                }
                if (dist > 50f) {
                    //吸入牵引（限频同步，防止逐帧刷包）
                    Vector2 pull = (Projectile.Center - n.Center).SafeNormalize(Vector2.Zero) * 6f;
                    n.velocity = Vector2.Lerp(n.velocity, pull, 0.2f);
                    if (Main.GameUpdateCount % 10 == 0) {
                        n.netUpdate = true;
                    }
                    continue;
                }
                //吞掉
                n.life = 0;
                n.HitEffect();
                n.active = false;
                n.netUpdate = true;
                Charge++;
                Projectile.netUpdate = true;
            }
        }

        private static int CountSharkrons() {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.Sharkron || n.type == NPCID.Sharkron2) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>从龙卷顶部甩出一条鲨鱼龙，抛物线扑向玩家</summary>
        private void LaunchSharkron(Player target) {
            Vector2 top = new(Projectile.Center.X, Projectile.position.Y + 30f);
            int idx = NPC.NewNPC(Projectile.GetSource_FromAI(), (int)top.X, (int)top.Y, NPCID.Sharkron2);
            if (idx < 0 || idx >= Main.maxNPCs) {
                return;
            }
            NPC shark = Main.npc[idx];
            //直接进入飞行段
            shark.ai[0] = 1f;
            shark.ai[1] = 1f;
            shark.target = target.whoAmI;
            Vector2 dir = (target.Center + target.velocity * 14f - top).SafeNormalize(-Vector2.UnitY);
            shark.velocity = dir * 15f - Vector2.UnitY * 3f;
            shark.rotation = shark.velocity.ToRotation();
            shark.direction = Math.Sign(shark.velocity.X) >= 0 ? 1 : -1;
            shark.spriteDirection = shark.direction;
            shark.netUpdate = true;
        }

        private void UpdateVisuals(float walkDir) {
            if (VaultUtils.isServer) {
                return;
            }
            float env = Envelope;
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);

            //基座泡沫环
            if (Main.rand.NextBool(3)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                    bottom + new Vector2(Main.rand.NextFloat(-ColumnWidth * 0.6f, ColumnWidth * 0.6f), -8f),
                    new Vector2(walkDir * 1.2f, -Main.rand.NextFloat(0.5f, 1.4f)),
                    FishronMotionFX.FoamWhite * (0.35f * env), Main.rand.NextFloat(0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(24, 40), Main.rand.NextFloat(-0.03f, 0.03f));
            }
            //底部卷吸：柱外碎浪被拖向基座再卷起——吸入感来自向心初速
            if (Main.rand.NextBool(3)) {
                float sideSign = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = bottom + new Vector2(sideSign * Main.rand.NextFloat(0.7f, 1.5f) * ColumnWidth * 0.5f,
                    -Main.rand.NextFloat(4f, 26f));
                Vector2 vel = new(-sideSign * Main.rand.NextFloat(2.5f, 4.5f), -Main.rand.NextFloat(1f, 2.5f));
                FishronMotionFX.SpawnSprayCone(pos, vel.SafeNormalize(-Vector2.UnitY), 1,
                    vel.Length() * 0.7f, vel.Length(), 0.3f, 0.75f * env);
            }
            //柱身甩出的水珠
            if (Main.rand.NextBool(2)) {
                float h = Main.rand.NextFloat(0.1f, 0.95f);
                Vector2 pos = bottom - new Vector2(0, Projectile.height * h)
                    + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * ColumnWidth * (1f - h * 0.45f), 0);
                Vector2 vel = new(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1f, 4f));
                FishronMotionFX.SpawnSprayCone(pos, vel.SafeNormalize(-Vector2.UnitY), 1,
                    vel.Length() * 0.6f, vel.Length(), 0.4f, 0.8f * env);
            }
            //顶冠散逸：顶口水沫被风切横甩出去，向上漂散
            if (Main.rand.NextBool(4)) {
                Vector2 top = bottom - new Vector2(0, Projectile.height * Main.rand.NextFloat(0.88f, 1.02f));
                float flingSign = Main.rand.NextBool() ? 1f : -1f;
                InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                    top + new Vector2(flingSign * ColumnWidth * Main.rand.NextFloat(0.1f, 0.4f), 0),
                    new Vector2(flingSign * Main.rand.NextFloat(1.5f, 3.5f), -Main.rand.NextFloat(0.8f, 2f)),
                    FishronMotionFX.FoamWhite * (0.3f * env), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.05f, 0.05f));
            }
            //风声
            if (LifeTimer % 40 == 0 && env > 0.5f) {
                SoundEngine.PlaySound(SoundID.DD2_BookStaffTwisterLoop with {
                    Volume = 0.5f + (Upgraded ? 0.25f : 0f),
                    Pitch = -0.3f,
                    MaxInstances = 3
                }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, FishronMotionFX.SeaGreen.ToVector3() * 0.6f * env);
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }

            Effect effect = EffectLoader.FishronTornado?.Value;
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            //quad 大幅宽于名义柱径：撕裂轮廓与离体飞沫全部留在画布内侧，
            //护栏只作采样保险，绝不承担切边（塑料感的旧病根之一）；
            //3.0×配合 shader 内 0.175 半宽预算=两侧各 ≥13% 永久空白带
            float drawW = ColumnWidth * 3.0f;
            float drawH = ColumnHeight * 1.30f;
            Vector2 drawCenter = bottom - new Vector2(0, drawH * 0.5f);

            if (effect == null || noiseTex == null) {
                DrawSpriteFallback(env, bottom, drawW, drawH);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env);
            effect.Parameters["uGrade"]?.SetValue(Upgraded ? 1f : 0f);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uDeepColor"]?.SetValue(FishronMotionFX.DeepSea.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(FishronMotionFX.FoamWhite.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(FishronMotionFX.SeaGreen.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new(drawW / pixel.Width, drawH / pixel.Height);
            sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White,
                0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失兜底：旋筒贴图堆叠</summary>
        private void DrawSpriteFallback(float env, Vector2 bottom, float drawW, float drawH) {
            Texture2D cyclone = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Cyclone")?.Value;
            if (cyclone == null) {
                return;
            }
            int layers = 7;
            for (int i = 0; i < layers; i++) {
                float t = i / (float)(layers - 1);
                Vector2 pos = bottom - new Vector2(0, drawH * t) - Main.screenPosition;
                float w = MathHelper.Lerp(drawW * 0.5f, drawW, t) / cyclone.Width;
                float rot = Main.GlobalTimeWrappedHourly * (4f - t * 1.5f) * (i % 2 == 0 ? 1f : -1f) + seed;
                Color c = Color.Lerp(FishronMotionFX.DeepSea, FishronMotionFX.SeaGreen, t);
                c = new Color(c.R, c.G, c.B, 0) * (env * 0.55f);
                Main.EntitySpriteDraw(cyclone, pos, null, c, rot, cyclone.Size() / 2f,
                    new Vector2(w, w * 0.6f), SpriteEffects.None, 0);
            }
        }
    }
}
