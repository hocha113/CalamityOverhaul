using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PhangasmBowHeldProj : BaseBow
    {
        public override string Texture => "CalamityMod/Items/Weapons/Ranged/Phangasm";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Phangasm>();
        public override int targetCWRItem => ModContent.ItemType<PhangasmEcType>();
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }
        public override void PostInOwner() {
            base.PostInOwner();
        }
        public override void BowShoot() {
            for (int i = 0; i < 5; i++) {
                int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.03f, 0.03f)) * Main.rand.NextFloat(0.8f, 1.12f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                Main.projectile[proj].noDropItem = true;
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Phantom;
            }
        }
    }
}
