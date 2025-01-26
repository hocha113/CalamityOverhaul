using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NettlevineGreatbowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NettlevineGreatbow";
        public override int targetCayItem => ModContent.ItemType<NettlevineGreatbow>();
        public override int targetCWRItem => ModContent.ItemType<NettlevineGreatbowEcType>();
        private int nettlevineIndex;
        public override void SetRangedProperty() {
            BowArrowDrawNum = 4;
            HandFireDistance = 20;
            DrawArrowMode = -22;
            BowstringData.DeductRectangle = new Rectangle(6, 16, 2, 34);
        }
        public override void BowShoot() {
            if (AmmoTypes == ModContent.ProjectileType<VanquisherArrowProj>()) {
                WeaponDamage = (int)(WeaponDamage * 0.7f);
            }
            for (int i = 0; i < 4; i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.GetNormalVector() * (-2 + i) * 10
                    , ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.NettlevineGreat;
                Main.projectile[proj].SetArrowRot();
            }

            nettlevineIndex++;
            if (nettlevineIndex > 4) {
                Projectile.NewProjectile(Owner.FromObjectGetParent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<TarragonArrowOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                nettlevineIndex = 0;
            }
        }
    }
}
