using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles
{
    /// <summary>激光眼高速激光弹，替代原版<see cref="ProjectileID.DeathLaser"/>青紫渐变拖尾+发光内核+命中迸发<see cref="PRT_TwinsSpark"/>ai[1]=1强化弹(狂暴/大招)，更快更亮</summary>
    internal class RetinazerLaser : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        private const float maxTimeLeft = 720;
        private bool onSound;
        private Trail Trail;
        private const int MaxPos = 22;
        internal static Color CoreColor => new(120, 200, 255);
        internal static Color GlowColor => new(150, 110, 255);
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 33;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = (int)maxTimeLeft;
            Projectile.extraUpdates = 1;
            if (CWRRef.GetBossRushActive() || Main.zenithWorld || Main.getGoodWorld) {
                Projectile.extraUpdates += 1;
            }
            Projectile.tileCollide = false;
            Projectile.maxPenetrate = Projectile.penetrate = 1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private bool Empowered => Projectile.ai[1] == 1f;

        public override void AI() {
            if (!onSound) {
                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.45f, Volume = 0.8f }, Projectile.Center);
                if (Empowered) {
                    Projectile.extraUpdates += 1;
                }
                onSound = true;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, CoreColor.ToVector3() * 0.6f);

            //飞行中的能量逸散粒子(低频，避免铺满屏幕)
            if (!VaultUtils.isServer && Main.rand.NextBool(14)) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center, Projectile.velocity * 0.1f
                    + Main.rand.NextVector2Circular(0.5f, 0.5f), Color.White, 0.8f)?.Configure(14, 0);
            }

            if (Projectile.Opacity < 1f) {
                Projectile.Opacity += 0.1f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            SpawnImpactBurst();
            Projectile.timeLeft = 20;
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            if (timeLeft > 30) {
                SpawnImpactBurst();
            }
        }

        private void SpawnImpactBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center
                    , VaultUtils.RandVr(6, 14), Color.White, Main.rand.NextFloat(1.2f, 2f))?.Configure(20, 0);
            }
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, GlowColor, 0.12f)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.5f, 14);
        }

        public float GetWidthFunc(float completionRatio) {
            float sengs = Empowered ? 1.45f : 1f;
            if (Projectile.timeLeft < maxTimeLeft / 3f) {
                sengs *= Projectile.timeLeft / (maxTimeLeft / 3f);
            }
            return (float)Math.Sin(completionRatio * Math.PI) * 14f * sengs;
        }

        public Color GetColorFunc(Vector2 _) => Color.Lerp(CoreColor, GlowColor, 0.4f) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            //发光弹头:外层紫晕+内核白光(A=0加色观感)
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;
            float coreScale = (Empowered ? 0.62f : 0.45f) * Projectile.Opacity;
            Main.EntitySpriteDraw(glow, drawPos, null, GlowColor with { A = 0 } * 0.85f * Projectile.Opacity,
                0f, origin, coreScale * 1.8f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, Color.White with { A = 0 } * Projectile.Opacity,
                0f, origin, coreScale * 0.8f, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Vector2[] newPoss = new Vector2[MaxPos];
            Trail ??= new Trail(newPoss, GetWidthFunc, GetColorFunc);
            Vector2 norlVer = Projectile.velocity.UnitVector();
            for (int i = 0; i < MaxPos; i++) {
                newPoss[i] = Projectile.Center + norlVer * i * 10 - norlVer * 200;
            }
            Trail.TrailPositions = newPoss;

            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRAsset.SlashFlatBlurHVMirror.Value);
            effect.Parameters["uFlow"].SetValue(VaultAsset.placeholder2.Value);
            effect.Parameters["uGradient"].SetValue(CWRAsset.AbsoluteZero_Bar.Value);
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
