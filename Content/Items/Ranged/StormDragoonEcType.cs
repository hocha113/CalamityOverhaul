using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 风暴龙骑士
    /// </summary>
    internal class StormDragoonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StormDragoon";
        public override void SetDefaults() {
            Item.SetCalamitySD<StormDragoon>();
            Item.SetCartridgeGun<StormDragoonHeldProj>(225);
        }
    }
}
