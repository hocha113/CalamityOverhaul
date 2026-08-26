using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 夜明灾变「极光帷幕」：光标上空展开 500px 水平极光帘（EmpressAurora.fx 成品复用，
    /// 帘心上下偏摆与判定同源），帘内敌持续受创，爆发段每 40t 自帘心垂落 4 道光矛。<br/>
    /// 蓄势 30t 细丝渐亮 / 爆发 160t 满幕 / 余韵 100t 光尘飘落
    /// </summary>
    internal class GsAuroraCurtainDirector : GsCataclysmDirectorProj, IPrimitiveDrawable
    {
        public override int OmenTicks => 30;
        public override int MainTicks => 160;
        public override int AftermathTicks => 100;

        /// <summary>帘水平半宽（总宽 500px）</summary>
        private const float HalfWidth = 250f;
        /// <summary>帘竖直视觉半厚</summary>
        private const float VisualHalfThick = 110f;
        /// <summary>帘心判定半厚（与 shader 亮带同源）</summary>
        private const float CoreHalfThick = 30f;

        protected override int HitTickRate => 12;

        protected override float TickDamageMul => 0.35f;

        /// <summary>偏摆相位种子（identity 定相，各端一致）</summary>
        private float SwayPhase => Projectile.identity * 0.7331f % 10f;

        /// <summary>帘心竖直偏摆：与 EmpressAurora.fx 内置幅度同比率（190px 基准折算），判定同源</summary>
        private float SwayOffset(float xNorm) {
            float t = Timer * 0.016f + SwayPhase;
            return (float)(Math.Sin(t + xNorm * 2.6f) * 46f + Math.Sin(t * 1.7f + xNorm * 5.2f) * 22f)
                * (VisualHalfThick / 190f);
        }

        /// <summary>帘体强度包络：蓄势细丝、爆发满幕、余韵渐熄（相位连续无跳变）</summary>
        private float Envelope() {
            int e = Elapsed;
            if (e < OmenTicks) {
                return VaultUtils.EaseOutQuad(e / (float)OmenTicks) * 0.38f;
            }
            if (e < OmenTicks + MainTicks) {
                float rise = MathHelper.Clamp((e - OmenTicks) / 22f, 0f, 1f);
                return MathHelper.Lerp(0.38f, 1f, VaultUtils.EaseOutQuad(rise));
            }
            float fade = (e - OmenTicks - MainTicks) / (float)AftermathTicks;
            return MathHelper.Clamp(1f - fade, 0f, 1f);
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.55f, Pitch = 0.45f }, Projectile.Center);
            }
            CurtainAmbience(0.4f);
        }

        protected override void MainUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.75f, Pitch = 0.3f }, Projectile.Center);
            }
            CurtainAmbience(1f);

            //每 40t 垂落一排光矛：帘宽四等分中点 + 少量抖动（owner 掷，随生成包过线）
            if (t % 40 == 20 && OwnerSide) {
                for (int i = 0; i < 4; i++) {
                    float xNorm = -0.75f + i * 0.5f;
                    float x = Projectile.Center.X + xNorm * HalfWidth + Main.rand.NextFloat(-26f, 26f);
                    float clampedNorm = MathHelper.Clamp((x - Projectile.Center.X) / HalfWidth, -1f, 1f);
                    Vector2 spawn = new(x, Projectile.Center.Y + SwayOffset(clampedNorm));
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawn, new Vector2(0f, 2.2f),
                        ModContent.ProjectileType<GsAuroraLanceProj>(), ScaledDamage(1f),
                        Projectile.knockBack, Projectile.owner, clampedNorm);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.5f, Pitch = 0.35f }, Projectile.Center);
                }
            }
        }

        protected override void AftermathUpdate(int t) => CurtainAmbience(0.7f);

        /// <summary>沿帘照明与光尘（粒子约 1/3 帧，守预算）</summary>
        private void CurtainAmbience(float density) {
            float env = Envelope();
            if (env <= 0.05f) {
                return;
            }
            for (int i = -3; i <= 3; i++) {
                float xNorm = i / 3f;
                Vector2 pos = Projectile.Center + new Vector2(xNorm * HalfWidth * 0.8f, SwayOffset(xNorm));
                Lighting.AddLight(pos, Main.hslToRgb((0.42f + xNorm * 0.16f + 1f) % 1f, 0.85f, 0.5f).ToVector3() * 0.45f * env);
            }
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                float xNorm = Main.rand.NextFloat(-1f, 1f);
                Vector2 pos = Projectile.Center + new Vector2(xNorm * HalfWidth, SwayOffset(xNorm) + Main.rand.NextFloat(-30f, 30f));
                PRTLoader.NewParticle<PRT_Sparkle>(pos, new Vector2(0f, Main.rand.NextFloat(0.4f, 1.1f) * density),
                    Main.hslToRgb(Main.rand.NextFloat(0.36f, 0.78f), 0.85f, 0.62f), Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.hslToRgb(0.45f, 0.8f, 0.55f), 32);
            }
        }

        /// <summary>爆发段才有判定；帘心亮带：按目标水平位置取当前偏摆做窄带判定，羽化端无判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != 1) {
                return false;
            }
            float dx = targetHitbox.Center.X - Projectile.Center.X;
            if (Math.Abs(dx) > HalfWidth * 0.82f) {
                return false;
            }
            float xNorm = MathHelper.Clamp(dx / HalfWidth, -1f, 1f);
            float coreY = Projectile.Center.Y + SwayOffset(xNorm);
            return Math.Abs(targetHitbox.Center.Y - coreY) < CoreHalfThick + targetHitbox.Height * 0.5f;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            float env = Envelope();
            if (env <= 0.01f) {
                return;
            }
            Effect effect = EffectLoader.EmpressAurora?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env);
            effect.Parameters["uPhase"]?.SetValue(SwayPhase);
            //帘心偏摆相位与判定同源，亮心画在真实危险区
            effect.Parameters["uSwayTime"]?.SetValue(Timer * 0.016f + SwayPhase);
            effect.Parameters["uCoreRatio"]?.SetValue(CoreHalfThick / VisualHalfThick);
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            //水平帘：uv.y 沿帘长（左右端羽化），uv.x 沿横截（帘心亮带上下偏摆）
            Vector2 left = Projectile.Center - new Vector2(HalfWidth, 0f);
            Vector2 right = Projectile.Center + new Vector2(HalfWidth, 0f);
            Vector2 thick = new(0f, VisualHalfThick);
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((left - thick).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((left + thick).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((right - thick).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((right + thick).ToVector3(), Color.White, new Vector2(1f, 1f));
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 极光光矛：自帘心垂落加速，穿透 3 目标，落地散光尘。
    /// ai[0]=出生位帘向归一（-1~1，定矛体色相）
    /// </summary>
    internal class GsAuroraLanceProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicCataclysm";

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        [VaultLoaden(CWRConstant.Masking + "StarTexture_White")]
        internal static Asset<Texture2D> StarTex = null;

        internal static readonly Color AuroraGreen = new(96, 255, 180);
        internal static readonly Color AuroraViolet = new(185, 130, 255);

        private Color LanceColor => Color.Lerp(AuroraGreen, AuroraViolet, (Projectile.ai[0] + 1f) * 0.5f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 110;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, LanceColor.ToVector3() * 0.4f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 8 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, -Projectile.velocity * 0.05f,
                    LanceColor, Main.rand.NextFloat(0.28f, 0.45f))?.Configure(LanceColor, 20);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.5f, 1.6f)),
                    LanceColor, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(LanceColor, 26);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = GlowTex?.Value;
            Texture2D star = StarTex?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float len = MathHelper.Clamp(speed * 11f, 46f, 150f);
            Color outer = LanceColor with { A = 0 };
            Color core = Color.White with { A = 0 };
            //矛体双层拉伸条带（尾拖在速度反向）
            Main.EntitySpriteDraw(glow, drawPos, null, outer * 0.55f, Projectile.rotation,
                new Vector2(glow.Width * 0.82f, glow.Height * 0.5f),
                new Vector2(len / glow.Width, 15f / glow.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core * 0.85f, Projectile.rotation,
                new Vector2(glow.Width * 0.82f, glow.Height * 0.5f),
                new Vector2(len * 0.72f / glow.Width, 7f / glow.Height), SpriteEffects.None, 0);
            //矛头星芒
            float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.83f);
            Main.EntitySpriteDraw(star, drawPos, null, outer * (0.8f * pulse), Projectile.rotation,
                star.Size() * 0.5f, 0.26f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
