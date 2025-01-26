using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.GunCustomization
{
    internal class GlobalBow : GlobalRanged
    {
        public override void PostModifyBow(BaseBow bow) {
            if (bow.targetCayItem == ItemID.PlatinumBow
                || bow.targetCayItem == ItemID.RichMahoganyBow
                || bow.targetCayItem == ItemID.Shadewood
                || bow.targetCayItem == ItemID.SilverBow
                || bow.targetCayItem == ItemID.TinBow
                || bow.targetCayItem == ItemID.TungstenBow
                || bow.targetCayItem == ItemID.WoodenBow
                || bow.targetCayItem == ItemID.PalmWoodBow
                || bow.targetCayItem == ItemID.PearlwoodBow
                || bow.targetCayItem == ItemID.IronBow
                || bow.targetCayItem == ItemID.LeadBow
                || bow.targetCayItem == ItemID.GoldBow
                || bow.targetCayItem == ItemID.EbonwoodBow
                || bow.targetCayItem == ItemID.CopperBow
                || bow.targetCayItem == ItemID.BorealWoodBow) {
                bow.InOwner_HandState_AlwaysSetInFireRoding = true;
                bow.BowstringData.DeductRectangle = new Rectangle(2, 6, 2, 20);
            }
        }
    }
}
