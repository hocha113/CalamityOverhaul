using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class ForsakenSaberHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<ForsakenSaber>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "AegisBlade_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            distanceToOwner = 48;
            trailTopWidth = 30;
        }

        public override void Shoot() {

            //dust
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Teleporter);
            }

            Vector2 spreadVelocity = ShootVelocity.RotatedByRandom(MathHelper.ToRadians(15f)) * Main.rand.NextFloat(0.8f, 1.2f);
            Projectile.NewProjectile(Source, ShootSpanPos, spreadVelocity, ModContent.ProjectileType<SandBlade>(), Projectile.damage / 2, Projectile.knockBack * 0.5f, Owner.whoAmI);

            // One at the cursor
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ModContent.ProjectileType<SandBlade>(), Projectile.damage / 2, Projectile.knockBack * 0.5f, Owner.whoAmI);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        }
    }
}
