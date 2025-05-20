using CalamityMod.Events;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class DeadLaser : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder2;
        private const float timeLeft = 900;
        private bool onSound;
        private Trail Trail;
        private const int MaxPos = 22;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 33;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = (int)timeLeft;
            Projectile.extraUpdates = 2;
            if (BossRushEvent.BossRushActive || Main.zenithWorld || Main.getGoodWorld) {
                Projectile.extraUpdates += 1;
            }
            Projectile.tileCollide = false;
            Projectile.maxPenetrate = Projectile.penetrate = 1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (!onSound) {
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.3f }, Projectile.Center);
                onSound = true;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());
            if (Projectile.Opacity < 0) {
                Projectile.Opacity += 0.1f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
            for (int i = 0; i < 13; i++) {
                BasePRT spark = new PRT_Spark(Projectile.Center
                    , CWRUtils.randVr(13, 23), false, 26, Main.rand.NextFloat(2.6f, 2.8f), Color.Gold);
                PRTLoader.AddParticle(spark);
            }
            Projectile.timeLeft = 30;
            Projectile.netUpdate = true;
        }

        public float GetWidthFunc(float completionRatio) {
            float sengs = 1f;
            if (Projectile.timeLeft < (timeLeft / 3f)) {
                sengs = Projectile.timeLeft / (timeLeft / 3f);
            }
            return (float)Math.Sin(completionRatio * Math.PI) * 15f * sengs;
        }

        public Color GetColorFunc(Vector2 _) => Color.DarkRed * Projectile.Opacity;

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
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "SlashFlatBlurHVMirror"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Placeholder_White.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "BloodRed_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
