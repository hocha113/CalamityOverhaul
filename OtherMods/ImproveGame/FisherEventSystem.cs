using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.OtherMods.ImproveGame
{
    internal class FisherEventSystem : ModSystem
    {
        /// <summary>
        /// 用于记录是否已经钓过比目鱼
        /// </summary>
        private static bool HasCaughtHalibut;

        public override void PostSetupContent() {
            if (!ModLoader.TryGetMod("ImproveGame", out Mod improveGame))
                return;

            //定义委托签名并转换
            improveGame.Call("RegisterFishingEvent", (Delegate)OnFishingCallback);
        }

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(HasCaughtHalibut)] = HasCaughtHalibut;
        }

        public override void LoadWorldData(TagCompound tag) {
            HasCaughtHalibut = false;
            if (tag.TryGet(nameof(HasCaughtHalibut), out bool hasCaughtHalibut)) {
                HasCaughtHalibut = hasCaughtHalibut;
            }
        }

        private void OnFishingCallback(TileEntity fisher, FishingAttempt fishingAttempt, Player player, ref int itemType, ref int itemStack, ref bool cancel) {
            //直接通过 ref 参数修改
            if (itemType == HalibutOverride.ID) {
                if (HasCaughtHalibut) {
                    itemType = ItemID.Bass;//钓鱼机在这种情况下只会钓到鲈鱼
                }
                else {
                    HasCaughtHalibut = true;//设置为已经钓过比目鱼
                }
            }
        }
    }
}
