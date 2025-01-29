using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.GunCustomization
{
    internal class GlobalBow : GlobalRanged
    {
        public override void PostModifyBow(BaseBow bow) {
            if (bow.TargetID == ItemID.PlatinumBow
                || bow.TargetID == ItemID.RichMahoganyBow
                || bow.TargetID == ItemID.Shadewood
                || bow.TargetID == ItemID.SilverBow
                || bow.TargetID == ItemID.TinBow
                || bow.TargetID == ItemID.TungstenBow
                || bow.TargetID == ItemID.WoodenBow
                || bow.TargetID == ItemID.PalmWoodBow
                || bow.TargetID == ItemID.PearlwoodBow
                || bow.TargetID == ItemID.IronBow
                || bow.TargetID == ItemID.LeadBow
                || bow.TargetID == ItemID.GoldBow
                || bow.TargetID == ItemID.EbonwoodBow
                || bow.TargetID == ItemID.CopperBow
                || bow.TargetID == ItemID.BorealWoodBow) {
                bow.InOwner_HandState_AlwaysSetInFireRoding = true;
                bow.BowstringData.DeductRectangle = new Rectangle(2, 6, 2, 20);
            }
        }
    }
}
