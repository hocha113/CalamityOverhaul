using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Everdeeps
{
    /// <summary>
    /// 渊涡水龙卷:共鸣满溢的结算物。在触发点起身成巨柱,
    /// 缓慢漂向最近的敌人,把非 Boss 敌人卷向柱轴,持续打击柱内目标;
    /// 柱身悬浮不贴地,水龙卷里有深渊生物的剪影在游。
    /// 绘制复用 FishronTornado.fx 换深渊色板
    /// </summary>
    internal class EverdeepMaelstrom : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>起身帧数</summary>
        private const int RiseTime = 34;
        /// <summary>消散帧数</summary>
        private const int FadeTime = 44;
        /// <summary>总寿命</summary>
        private const int TotalLife = 380;

        private const float ColumnWidth = 210f;
        private const float ColumnHeight = 640f;

        private ref float LifeTimer => ref Projectile.localAI[0];

        private float seed;
        /// <summary>漂移速度,缓进缓出</summary>
        private Vector2 driftVel;

        /// <summary>起身/消散包络</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp(LifeTimer / RiseTime, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);
                return Math.Min(rise * rise, fade);
            }
        }

        private bool HitWindowOpen => LifeTimer >= RiseTime * 0.55f && Projectile.timeLeft >= FadeTime / 2;

        public override void SetDefaults() {
            Projectile.width = 190;
            Projectile.height = 600;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalLife;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
        }

        public override void SetStaticDefaults() {
            //绘制 quad 宽出命中盒一倍余,近出屏不许整柱瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 560;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanHitNPC(NPC target) => HitWindowOpen ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //连续 tick 打蠕虫体节收敛一点
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.65f;
            }
        }

        public override void AI() {
            LifeTimer++;
            seed = Projectile.whoAmI * 0.617f;
            float env = Envelope;

            //出生拍:各端按 LifeTimer 自播,不依赖生成端
            if (LifeTimer == 1f && !VaultUtils.isServer) {
                SpawnBeat();
            }

            //缓慢漂向最近的敌人,无目标时原地悬浮微沉浮
            NPC quarry = FindQuarry();
            Vector2 want = Vector2.Zero;
            if (quarry != null && LifeTimer > RiseTime) {
                Vector2 to = quarry.Center - Projectile.Center;
                if (to.Length() > 40f) {
                    want = to.SafeNormalize(Vector2.Zero) * 2.3f;
                }
            }
            driftVel = Vector2.Lerp(driftVel, want, 0.03f);
            Projectile.position += driftVel * env;
            Projectile.position.Y += MathF.Sin(LifeTimer * 0.035f + seed) * 0.35f;

            //卷吸:柱域内非 Boss 敌人被拽向柱轴并卷起(服务端/单人裁决)
            if (!VaultUtils.isClient && HitWindowOpen) {
                SuckNPCs();
                if (Main.GameUpdateCount % 30 == 0) {
                    Projectile.netUpdate = true;
                }
            }

            UpdateVisuals(env);
        }

        private NPC FindQuarry() {
            NPC best = null;
            float bestDist = 1000f * 1000f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>把柱域内的敌人拽向柱轴,顺带卷起;Boss 与免击退敌人不受</summary>
        private void SuckNPCs() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.boss || npc.knockBackResist <= 0f) {
                    continue;
                }
                float dx = npc.Center.X - Projectile.Center.X;
                float dy = npc.Center.Y - Projectile.Center.Y;
                if (Math.Abs(dx) > ColumnWidth * 1.6f || Math.Abs(dy) > ColumnHeight * 0.7f) {
                    continue;
                }
                float pull = 0.22f * npc.knockBackResist;
                npc.velocity.X -= Math.Sign(dx) * pull;
                //卷起:只在下坠或缓升时给升力,不无限抬升
                if (npc.velocity.Y > -3f) {
                    npc.velocity.Y -= 0.14f * npc.knockBackResist;
                }
                if (Main.GameUpdateCount % 12 == 0) {
                    npc.netUpdate = true;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 300);
            if (VaultUtils.isServer || !Main.rand.NextBool(3)) {
                return;
            }
            EverdeepVFX.SplashBurst(target.Center, new Vector2(0, -6f), 0.5f);
        }

        /// <summary>出生拍:巨柱破水而起,一圈冲击水环+水花+屏震</summary>
        private void SpawnBeat() {
            SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.75f, Pitch = -0.35f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_BookStaffTwisterLoop with {
                Volume = 0.8f,
                Pitch = -0.2f,
            }, Projectile.Center);

            if (Main.LocalPlayer.Distance(Projectile.Center) < 1300f) {
                Main.LocalPlayer.CWR().GetScreenShake(5f);
            }

            //破水拍:向四周甩水
            for (int i = 0; i < 22; i++) {
                float ang = MathHelper.TwoPi * i / 22f;
                Vector2 dir = ang.ToRotationVector2();
                EverdeepVFX.ShedDroplet(Projectile.Center + dir * Main.rand.NextFloat(10f, 50f)
                    , dir * Main.rand.NextFloat(3f, 9f) - Vector2.UnitY * Main.rand.NextFloat(1f, 4f)
                    , Main.rand.NextFloat(0.9f, 1.4f));
            }
        }

        /// <summary>柱身常驻演出:基座泡沫/柱身甩滴/顶冠散逸/游动的深渊剪影</summary>
        private void UpdateVisuals(float env) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);

            //基座泡沫翻涌
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_OceanCurrentFoam>(
                    bottom + new Vector2(Main.rand.NextFloat(-ColumnWidth * 0.55f, ColumnWidth * 0.55f), -10f)
                    , new Vector2(driftVel.X * 0.8f, -Main.rand.NextFloat(0.5f, 1.4f))
                    , EverdeepVFX.AbyssFoam * (0.55f * env), Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(24, 40), Main.rand.NextFloat(0.02f, 0.05f));
            }
            //柱身离心甩滴
            if (Main.rand.NextBool(2)) {
                float h = Main.rand.NextFloat(0.1f, 0.95f);
                Vector2 pos = bottom - new Vector2(0, Projectile.height * h)
                    + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * ColumnWidth * (1f - h * 0.4f), 0);
                Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f), -Main.rand.NextFloat(1f, 4f));
                EverdeepVFX.ShedDroplet(pos, vel, Main.rand.NextFloat(0.7f, 1.1f) * env);
            }
            //顶冠散逸
            if (Main.rand.NextBool(4)) {
                Vector2 top = bottom - new Vector2(0, Projectile.height * Main.rand.NextFloat(0.85f, 1.02f));
                float sign = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_OceanCurrentFoam>(
                    top + new Vector2(sign * ColumnWidth * Main.rand.NextFloat(0.1f, 0.4f), 0)
                    , new Vector2(sign * Main.rand.NextFloat(1.5f, 3.5f), -Main.rand.NextFloat(0.8f, 2f))
                    , EverdeepVFX.AbyssFoam * (0.5f * env), Main.rand.NextFloat(0.07f, 0.11f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(0.03f, 0.06f));
            }
            //深渊剪影:偶发的鱼影与生物光尘在柱内上旋游动
            if (Main.rand.NextBool(9)) {
                bool fish = Main.rand.NextBool(3);
                float h = Main.rand.NextFloat(0.15f, 0.85f);
                Vector2 pos = bottom - new Vector2(0, Projectile.height * h)
                    + new Vector2(Main.rand.NextFloat(-0.35f, 0.35f) * ColumnWidth, 0);
                Vector2 vel = new(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1.5f, 3.5f));
                PRTLoader.NewParticle<PRT_OceanCurrentMarineMote>(pos, vel
                    , fish ? EverdeepVFX.AbyssDeep * 0.9f : EverdeepVFX.AbyssGlow
                    , Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(fish, Main.rand.Next(40, 70));
            }
            //风声水声
            if ((int)LifeTimer % 40 == 0 && env > 0.5f) {
                SoundEngine.PlaySound(SoundID.DD2_BookStaffTwisterLoop with {
                    Volume = 0.55f,
                    Pitch = -0.25f,
                    MaxInstances = 3,
                }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, EverdeepVFX.AbyssGlow.ToVector3() * 0.55f * env);
        }

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }

            Effect effect = EffectLoader.FishronTornado?.Value;
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            //quad 大幅宽于名义柱径:撕裂轮廓与离体飞沫全留在画布内侧(合同同鲨鱼龙卷)
            float drawW = ColumnWidth * 3.0f;
            float drawH = ColumnHeight * 1.30f;
            Vector2 drawCenter = bottom - new Vector2(0, drawH * 0.5f);

            if (effect == null || noiseTex == null) {
                DrawSpriteFallback(env, bottom, drawW, drawH);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            //浓度增益:shader 内 saturate 收顶,>1 只抬中低密度区,巨柱要读得厚实
            effect.Parameters["uIntensity"]?.SetValue(env * 1.35f);
            effect.Parameters["uGrade"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uDeepColor"]?.SetValue(EverdeepVFX.AbyssDeep.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(EverdeepVFX.AbyssBlue.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(EverdeepVFX.AbyssFoam.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
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

        /// <summary>着色器缺失兜底:旋筒贴图堆叠</summary>
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
                Color c = Color.Lerp(EverdeepVFX.AbyssDeep, EverdeepVFX.AbyssBlue, t);
                c = new Color(c.R, c.G, c.B, 0) * (env * 0.72f);
                Main.EntitySpriteDraw(cyclone, pos, null, c, rot, cyclone.Size() / 2f,
                    new Vector2(w, w * 0.6f), SpriteEffects.None, 0);
            }
        }
        #endregion

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //收场:柱身塌落成一阵水
            for (int i = 0; i < 14; i++) {
                float h = Main.rand.NextFloat(0f, 0.9f);
                Vector2 pos = new(Projectile.Center.X + Main.rand.NextFloat(-0.4f, 0.4f) * ColumnWidth
                    , Projectile.position.Y + Projectile.height * (1f - h));
                EverdeepVFX.ShedDroplet(pos, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(1f, 4f))
                    , Main.rand.NextFloat(0.8f, 1.2f));
            }
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.4f }, Projectile.Center);
        }
    }
}
