using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Extras;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using Terraria.ID;
using CalamityOverhaul.Common.Effects;
using System;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class PalustrisHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "Palustris";
        public override int targetCayItem => ModContent.ItemType<Palustris>();
        public override int targetCWRItem => ModContent.ItemType<Palustris>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 3;
            HandFireDistance = 20;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedByRandom(0.6f), AmmoTypes
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }

    internal class PalustrisOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 622;
            Projectile.ignoreWater = true;
            Projectile.light = 0.6f;
            Projectile.friendly = true;
        }

        public override void AI() {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                , DustID.SnowBlock, Projectile.velocity.X, Projectile.velocity.Y, 55);
            dust.noGravity = true;
            dust.shader = EffectsRegistry.InShootGlowShader;
            dust.shader.UseColor(Color.Blue);
            Projectile.ai[0] += Main.rand.Next(2);
            if (Projectile.ai[0] > 20) {
                Projectile.velocity = Projectile.velocity.RotatedByRandom(1.6f);
                Projectile.velocity *= 1.05f;
                Projectile.tileCollide = !Projectile.tileCollide;
                Projectile.ai[0] = 0;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = new Vector2(0, -6);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            float angle = Main.rand.NextFloat(MathF.PI * 2f);
            int numSpikes = 3;
            float spikeAmplitude = 12f;
            float scale = Main.rand.NextFloat(1f, 1.35f);
            for (float spikeAngle = 0f; spikeAngle < MathF.PI * 2f; spikeAngle += 0.25f) {
                Vector2 offset = spikeAngle.ToRotationVector2() * (2f +
                    (float)(Math.Sin(angle + spikeAngle * numSpikes) + 1.0) * spikeAmplitude) * 0.15f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, offset, 0, default, scale);
                dust.shader = EffectsRegistry.StreamerDustShader;
                dust.noGravity = true;
                dust.scale += 0.5f;
            }
        }
    }
}
