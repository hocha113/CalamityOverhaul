using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.ImproveGame
{
    internal class FishingEventSystem : ICWRLoader
    {
        void ICWRLoader.SetupData() {
            if (!ModLoader.TryGetMod("ImproveGame", out Mod improveGame))
                return;

            //定义委托签名并转换
            improveGame.Call("RegisterFishingEvent", (Delegate)OnFishingCallback);
        }

        private void OnFishingCallback(TileEntity fisher, FishingAttempt fishingAttempt, Player player, ref int itemType, ref int itemStack, ref bool cancel) {
            //直接通过 ref 参数修改
            if (itemType == HalibutOverride.ID && player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                itemType = ItemID.Bass;//钓鱼机在这种情况下只会钓到鲈鱼
            }
        }
    }
}
