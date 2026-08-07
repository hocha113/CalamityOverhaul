using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.NeutronWands
{
    /// <summary>
    /// 星震：被磁制动压满的壳层崩裂，缠绕的磁层瞬间重联并整体外抛。
    /// 借 NeutronPulsar.fx 的 Field 技术画外扩磁层，本体即是被吹开的磁笼。
    /// </summary>
    internal class NeutronStarquake : ModProjectile, IPrimitiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Life = 34;
        private const float BaseReach = 250f;

        private static readonly Vector3 ColHot = new(0.82f, 0.88f, 1f);
        private static readonly Vector3 ColMain = new(0.54f, 0.31f, 1f);
        private static readonly Vector3 ColBeam = new(0.47f, 0.71f, 1f);
        private static readonly Vector3 ColDeep = new(0.12f, 0.10f, 0.50f);

        /// <summary>蓄力强度 0~1，随生成包同步</summary>
        public ref float Power => ref Projectile.ai[0];

        private int Age => Life - Projectile.timeLeft;
        private float Progress => MathHelper.Clamp(Age / (float)Life, 0f, 1f);
        /// <summary>冲击前沿世界半径</summary>
        private float Reach => (BaseReach + Power * 330f) * VaultUtils.EaseOutCubic(Progress);
        private float Fade => 1f - VaultUtils.EaseInQuad(MathHelper.Clamp((Progress - 0.35f) / 0.65f, 0f, 1f));
        private float Seed => Projectile.identity * 0.211f % 1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 1240;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.ArmorPenetration = 100;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Age == 0) {
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1f, Pitch = -0.5f }, Projectile.Center);
                SpawnBirthParticles();
            }

            //重联把周围敌方弹幕一并撕掉
            EatHostileProjectiles();
            EmitFront();

            Lighting.AddLight(Projectile.Center, ColHot * (1.4f * Fade));
        }

        private void SpawnBirthParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            //2 帧过曝白点，随后交给磁层
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero
                , NeutronPulsar.ParticleHot, 0.3f)?.Configure(0.3f, 2.6f + Power * 1.4f, 20);

            int spokes = (int)(10 + Power * 8);
            for (int i = 0; i < spokes; i++) {
                float ang = MathHelper.TwoPi * i / spokes + Seed;
                Vector2 dir = ang.ToRotationVector2();
                //重联丝：沿断开的力线整束甩出
                for (int j = 0; j < 4; j++) {
                    PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center + dir * (18f + j * 13f)
                        , dir * Main.rand.NextFloat(9f, 20f) * (0.7f + Power * 0.7f)
                        , Color.Lerp(NeutronPulsar.ParticleHot, NeutronPulsar.ParticleViolet, j / 4f)
                        , Main.rand.NextFloat(0.4f, 0.85f))
                        ?.Configure(Main.rand.Next(18, 32), Main.rand.NextFloat(-0.45f, 0.45f));
                }
            }
        }

        /// <summary>前沿掠过时持续撒星，让冲击面看得见位置</summary>
        private void EmitFront() {
            if (VaultUtils.isServer || Fade <= 0.05f) {
                return;
            }
            int count = Age < 6 ? 6 : 2;
            for (int i = 0; i < count; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dir = ang.ToRotationVector2();
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + dir * Reach * Main.rand.NextFloat(0.86f, 1.04f)
                    , dir * Main.rand.NextFloat(2f, 7f)
                    , Color.Lerp(NeutronPulsar.ParticleBlue, NeutronPulsar.ParticleHot, Main.rand.NextFloat(0.6f))
                    , Main.rand.NextFloat(0.5f, 1f))
                    ?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        private void EatHostileProjectiles() {
            float radius = Reach * 0.7f;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.damage <= 0) {
                    continue;
                }
                if (Vector2.Distance(proj.Center, Projectile.Center) < radius) {
                    proj.Kill();
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float reach = Reach;
            if (reach < 12f) {
                return false;
            }
            //只判前沿一圈，中心真空不重复打
            if (!VaultUtils.CircleIntersectsRectangle(Projectile.Center, reach, targetHitbox)) {
                return false;
            }
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, reach * 0.45f, targetHitbox)
                ? Age < 8
                : true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VoidErosion>(), 1800);

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center + Main.rand.NextVector2Circular(16f, 16f)
                    , Main.rand.NextVector2Circular(8f, 8f)
                    , Color.Lerp(NeutronPulsar.ParticleHot, NeutronPulsar.ParticleViolet, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.6f, 1.2f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        public void Warp() {
            if (Fade <= 0.02f) {
                return;
            }
            float size = Math.Max(Reach * 2.4f, 120f);
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size
                , 0.5f * Fade + Power * 0.2f, Progress, 0f, "ShockwaveRing");
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer || Fade <= 0.02f) {
                return;
            }

            Effect effect = EffectLoader.NeutronPulsar?.Value;
            Texture2D cells = CWRAsset.Extra_193?.Value;
            Texture2D quad = VaultAsset.placeholder2?.Value;
            if (effect == null || cells == null || quad == null) {
                return;
            }

            //磁笼被整体吹开：quad 随前沿扩张，环与力线一起外扩
            float side = Math.Max(Reach * 2.9f, 90f);

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSpin"]?.SetValue(Seed * MathHelper.TwoPi + Progress * 1.4f);
            effect.Parameters["uSpinRate"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uFade"]?.SetValue(Fade * (1.15f + Power * 0.5f));
            effect.Parameters["uRadius"]?.SetValue(0.085f);
            effect.Parameters["uQuake"]?.SetValue(1f - Progress * 0.6f);
            effect.Parameters["uGlitch"]?.SetValue(1f);
            effect.Parameters["uMagAngle"]?.SetValue(Seed * MathHelper.TwoPi);
            effect.Parameters["uSquash"]?.SetValue(1f);
            effect.Parameters["uMotAngle"]?.SetValue(0f);
            effect.Parameters["uColHot"]?.SetValue(ColHot);
            effect.Parameters["uColMain"]?.SetValue(ColMain);
            effect.Parameters["uColBeam"]?.SetValue(ColBeam);
            effect.Parameters["uColDeep"]?.SetValue(ColDeep);
            effect.Parameters["uCellTex"]?.SetValue(cells);

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = quad.Size() * 0.5f;
            Vector2 scale = new(side / quad.Width, side / quad.Height);

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Field"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(quad, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            sb.End();
        }
    }
}
