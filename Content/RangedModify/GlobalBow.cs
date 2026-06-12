using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RangedModify
{
    internal static class GlobalBow
    {
        /// <summary>
        /// 该物品作为弓是否活跃？
        /// </summary>
        public static bool IsBow {
            get {
                Item handItem = Main.LocalPlayer.GetItem();
                if (handItem == null) {
                    return false;
                }
                if (handItem.ammo != AmmoID.None) {
                    return false;
                }
                return CWRLoad.ItemIsBow[handItem.type] || CWRLoad.ItemIsCrossBow[handItem.type] || handItem.useAmmo == AmmoID.Arrow;
            }
        }
        /// <summary>
        /// 该物品作为弓是否活跃？
        /// </summary>
        public static bool IsArrow {
            get {
                Item handItem = Main.LocalPlayer.GetItem();
                if (handItem == null || handItem.type == ItemID.None) {
                    return false;
                }
                return handItem.ammo == AmmoID.Arrow;
            }
        }

    }
}
