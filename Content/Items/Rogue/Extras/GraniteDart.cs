using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class GraniteDart : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/GraniteDart";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.damage = 12;
            Item.knockBack = 2.2f;
            Item.shootSpeed = 5f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<GraniteDartHeld>();
        }
    }

    internal class GraniteDartHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/GraniteDart";
        public override void SetThrowable() {
            OffsetRoting = 45 * CWRUtils.atoR;
        }

        public override void OnThrowing() {
            base.OnThrowing();
        }

        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (++Projectile.ai[2] > 36) {
                Projectile.velocity *= 0.92f;
                Projectile.velocity.Y += 0.65f;
            }
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 8;
            Projectile.velocity = UnitToMouseV * 17.5f;
            Projectile.tileCollide = true;
            Projectile.extraUpdates += 1;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * 0.6f;
            Projectile.alpha -= 15;
            return false;
        }
    }
}
