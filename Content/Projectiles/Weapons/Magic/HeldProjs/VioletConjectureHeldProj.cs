using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content.Items.Magic.Extras;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class VioletConjectureHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "VioletConjecture";
        public override int targetCayItem => ModContent.ItemType<VioletConjecture>();
        public override int targetCWRItem => ModContent.ItemType<VioletConjecture>();
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
            Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedByRandom(1.6f), AmmoTypes
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }

    internal class VioletConjectureOrb : ModProjectile
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
                , 68, Projectile.velocity.X, Projectile.velocity.Y, 55);
            dust.noGravity = true;
            dust.shader = EffectsRegistry.InShootGlowShader;
            dust.shader.UseColor(Color.DarkBlue);
            Projectile.ai[0] += Main.rand.Next(3);
            if (Projectile.ai[0] > 20) {
                Projectile.velocity = Projectile.velocity.RotatedByRandom(0.6f);
                Projectile.velocity *= 1.15f;
                Projectile.tileCollide = !Projectile.tileCollide;
                Projectile.ai[0] = 0;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity.RotatedByRandom(0.1f) * 1.01f;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }
    }
}
