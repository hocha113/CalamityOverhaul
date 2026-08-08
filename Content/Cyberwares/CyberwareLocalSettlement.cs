using CalamityOverhaul.Content.Cyberwares.Victors;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares
{
    /// <summary>
    /// 一次义体交互在请求方本机要落的背包改动，随待决请求一起留存，等权威端放行后兑现
    /// </summary>
    /// <param name="LoadoutSlot">目标装配槽</param>
    /// <param name="InventorySlot">安装时义体所在的背包格，其余为 -1</param>
    /// <param name="ItemType">安装时被装走的义体 / 购买时到手的义体</param>
    /// <param name="SwapBackType">回到背包的义体：安装时是被换下的旧件，卸载时是卸下的件</param>
    internal readonly record struct VictorLocalPlan(
        int LoadoutSlot,
        int InventorySlot,
        int ItemType,
        int SwapBackType);

    /// <summary>
    /// 义体交互的本机背包结算。非 ServerSideCharacter 的联机里玩家背包归自己管，
    /// 服务端发来的自身槽位同步会被原版直接丢弃，所以扣款与收货只能由请求方本机执行，
    /// 改完的槽位由原版每帧的 TrySyncingMyPlayer 回灌服务端
    /// </summary>
    internal static class CyberwareLocalSettlement
    {
        private const int MainInventorySlotCount = 50;

        /// <summary>主背包首个空格，没有则 -1</summary>
        internal static int FindEmptyMainSlot(Player player) {
            if (player?.inventory == null) {
                return -1;
            }
            int count = Math.Min(MainInventorySlotCount, player.inventory.Length);
            for (int i = 0; i < count; i++) {
                if (player.inventory[i] == null || player.inventory[i].IsAir) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 兑现本机改动；本机做不到时把裁决码降级，让 UI 走失败反馈
        /// </summary>
        internal static VictorRequestResult Settle(VictorRequestKind kind,
            in VictorLocalPlan plan, in VictorRequestResult result) {
            Player player = Main.LocalPlayer;
            if (VaultUtils.isServer || player?.active != true) {
                return Downgrade(result, VictorResultCode.InvalidPlayer);
            }

            return kind switch {
                VictorRequestKind.Install => SettleInstall(player, plan, result),
                VictorRequestKind.Uninstall => SettleUninstall(player, plan, result),
                VictorRequestKind.Purchase => SettlePurchase(player, plan, result),
                _ => Downgrade(result, VictorResultCode.InvalidPayload),
            };
        }

        /// <summary>装配表已由权威端改好，这里只负责取走背包里那件、把旧件放回原格</summary>
        private static VictorRequestResult SettleInstall(Player player,
            in VictorLocalPlan plan, in VictorRequestResult result) {
            int slot = FindSourceSlot(player, plan.InventorySlot, plan.ItemType);
            if (slot < 0) {
                //本机已经找不到那件义体：装配表改动无法撤回，只能放弃回收并留痕
                CWRMod.Instance.Logger.Warn(
                    $"Victor install settled without source item (type {plan.ItemType})");
                return result;
            }

            player.inventory[slot] = plan.SwapBackType > ItemID.None
                ? new Item(plan.SwapBackType)
                : new Item();
            return result;
        }

        private static VictorRequestResult SettleUninstall(Player player,
            in VictorLocalPlan plan, in VictorRequestResult result) {
            if (plan.SwapBackType <= ItemID.None
                || plan.SwapBackType >= ItemLoader.ItemCount) {
                return result;
            }
            GiveItem(player, plan.SwapBackType);
            return result;
        }

        private static VictorRequestResult SettlePurchase(Player player,
            in VictorLocalPlan plan, in VictorRequestResult result) {
            long price = result.AuthorityPrice;
            if (plan.ItemType <= ItemID.None
                || plan.ItemType >= ItemLoader.ItemCount || price <= 0L) {
                return Downgrade(result, VictorResultCode.InvalidPayload);
            }
            if (!player.CanAfford(price)) {
                return Downgrade(result, VictorResultCode.InsufficientFunds);
            }

            int destination = FindEmptyMainSlot(player);
            if (destination < 0) {
                return Downgrade(result, VictorResultCode.InventoryFull);
            }
            Item purchased = new(plan.ItemType);
            if (purchased.IsAir) {
                return Downgrade(result, VictorResultCode.InvalidPayload);
            }

            //先占位再扣款：找零会挑空格落脚，占住目标格免得货被顶掉
            player.inventory[destination] = purchased;
            bool paid;
            try {
                paid = player.BuyItem(price);
            } catch (Exception ex) {
                paid = false;
                CWRMod.Instance.Logger.Error(
                    $"Victor purchase payment failed: {ex.Message}");
            }
            if (!paid) {
                //CanAfford 已经过了，这里只会是找零无处安放
                player.inventory[destination] = new Item();
                return Downgrade(result, VictorResultCode.InventoryFull);
            }
            return result;
        }

        /// <summary>优先原格，被挪动过则按类型全背包找一遍</summary>
        private static int FindSourceSlot(Player player, int preferredSlot,
            int itemType) {
            if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount) {
                return -1;
            }
            int count = Math.Min(Main.InventorySlotsTotal, player.inventory.Length);
            if (preferredSlot >= 0 && preferredSlot < count
                && player.inventory[preferredSlot]?.type == itemType) {
                return preferredSlot;
            }
            for (int i = 0; i < count; i++) {
                if (player.inventory[i]?.type == itemType) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>没空格就掉地上，义体不能凭空消失</summary>
        private static void GiveItem(Player player, int itemType) {
            int destination = FindEmptyMainSlot(player);
            if (destination >= 0) {
                player.inventory[destination] = new Item(itemType);
                return;
            }
            player.QuickSpawnItem(player.GetSource_Misc("CWR_VictorClinic"),
                itemType);
        }

        private static VictorRequestResult Downgrade(in VictorRequestResult result,
            VictorResultCode code)
            => result with { Code = code };
    }
}
