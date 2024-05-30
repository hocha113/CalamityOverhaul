using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Terraria.Audio;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class MarbleDagger : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/MarbleDagger";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 36;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.damage = 12;
            Item.knockBack = 2.2f;
            Item.shootSpeed = 13f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<MarbleDaggerHeld>();
        }
    }

    internal class MarbleDaggerHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/MarbleDagger";
        public override void SetThrowable() {
        }

        public override void OnThrowing() {
            base.OnThrowing();
        }

        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[2] > 0 && Projectile.timeLeft < 100) {
                Projectile.velocity.Y += 0.1f;
            }
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 8;
            Projectile.tileCollide = true;
            if (Projectile.ai[2] > 0) {
                Projectile.extraUpdates += 1;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[2] == 0) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center
                    , UnitToMouseV.RotatedBy(0.15f) * 6, Type, Projectile.damage
                    , Projectile.knockBack, Owner.whoAmI, 0, 0, 1);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center
                    , UnitToMouseV.RotatedBy(-0.15f) * 6, Type, Projectile.damage
                    , Projectile.knockBack, Owner.whoAmI, 0, 0, 1);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * 0.6f;
            Projectile.alpha -= 15;
            return false;
        }
    }
}
