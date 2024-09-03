using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class StormSaberHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<StormSaber>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 14;
            drawTrailTopWidth = 20;
            Length = 50;
        }

        public override void Shoot() {
            Vector2 vec = ShootVelocity * 2f;
            Projectile.NewProjectile(Source, ShootSpanPos.X, ShootSpanPos.Y, vec.X, vec.Y, ModContent.ProjectileType<StormBeam>(), (int)(Projectile.damage * 0.8), Projectile.knockBack, Owner.whoAmI, 0f, 0f);

            Vector2 spawnPos = new Vector2(Owner.MountedCenter.X + Main.rand.Next(-200, 201), Owner.MountedCenter.Y - 600f);
            Vector2 targetPos = Main.MouseWorld + new Vector2(Main.rand.Next(-30, 31), Main.rand.Next(-30, 31));
            Vector2 vec2 = targetPos - spawnPos;
            vec2.Normalize();
            vec2 *= 13f;

            Projectile.NewProjectile(Source, spawnPos, vec2, ModContent.ProjectileType<StormBeam>(), (int)(Projectile.damage * 0.6), Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(Source, ShootSpanPos.X, ShootSpanPos.Y, ShootVelocity.X, ShootVelocity.Y, ModContent.ProjectileType<StormBeam>(), (int)(Projectile.damage * 0.8), Projectile.knockBack, Owner.whoAmI, 0f, 0f);

            Vector2 spawnPos = new Vector2(Owner.MountedCenter.X + Main.rand.Next(-200, 201), Owner.MountedCenter.Y - 600f);
            Vector2 targetPos = Main.MouseWorld + new Vector2(Main.rand.Next(-30, 31), Main.rand.Next(-30, 31));

            Vector2 vec2 = targetPos - spawnPos;
            vec2.Normalize();
            vec2 *= 13f;

            Projectile.NewProjectile(Source, spawnPos, vec2, ModContent.ProjectileType<StormBeam>(), (int)(Projectile.damage * 0.6), Projectile.knockBack, Owner.whoAmI);
        }
    }
}
