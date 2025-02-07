using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BarrenBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Item_Ranged + "BarrenBow";
        public override int TargetID => ModContent.ItemType<BarrenBow>();
        public override void SetRangedProperty() {
            DrawArrowMode = -18;
            HandFireDistanceX = 16;
            //InOwner_HandState_AlwaysSetInFireRoding = true;
            BowstringData.DeductRectangle = new Rectangle(18, 22, 2, 38);
            BowstringData.TopBowOffset = new Vector2(18, 20);
            BowstringData.BottomBowOffset = new Vector2(18, 12);
        }
        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.BarrenBow;
        }
    }
}
