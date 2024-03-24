using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NettlevineGreatbowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NettlevineGreatbow";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.NettlevineGreatbow>();
        public override int targetCWRItem => ModContent.ItemType<NettlevineGreatbowEcType>();
        private int nettlevineIndex;
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }
        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.NettlevineGreat;

            nettlevineIndex++;
            if (nettlevineIndex > 4) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<TarragonArrowOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                nettlevineIndex = 0;
            }
        }
    }
}
