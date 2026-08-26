using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RAMSystems
{
    internal abstract class BaseRamUpgradeChip : ModItem
    {
        protected abstract RamUpgradeKind UpgradeKind { get; }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(gold: 1);
            Item.UseSound = CWRSound.ChipSet;
            Item.autoReuse = true;
        }

        public override bool CanUseItem(Player player)
            => RamSystem.CanUseUpgrade(player, UpgradeKind)
                && !RamSystem.HasPendingUpgrade(player);

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server) {
                return false;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return RamNet.SendUpgradeRequest(player, UpgradeKind);
            }
            if (!RamSystem.TryUseUpgrade(player, UpgradeKind)) {
                return false;
            }
            SoundEngine.PlaySound(SoundID.ResearchComplete, player.Center);
            return true;
        }

        /// <summary>联机下这里不扣，等权威端回执放行后由本机扣，被拒的请求不损失芯片</summary>
        public override bool ConsumeItem(Player player)
            => Main.netMode == NetmodeID.SinglePlayer;

        internal static void HandleRequestResult(Player player,
            in RamRequestResult result) {
            if (player == null || player.whoAmI != Main.myPlayer
                || !RamNet.TryGetUpgradeKind(result.OperationId, out RamUpgradeKind kind)) {
                return;
            }

            bool located = player.GetModPlayer<RAMPlayer>()
                .TryTakePendingUpgrade(result.RequestId, out int inventorySlot);
            if (result.ResultCode == (byte)RamUpgradeResultCode.Success) {
                //回执迟到导致登记已被超时清掉时，仍按操作类型找一张扣掉
                ConsumeChipLocally(player, kind, located ? inventorySlot : -1);
                SoundEngine.PlaySound(SoundID.ResearchComplete, player.Center);
                return;
            }
            RamSystem.NotifyInsufficient();
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Pitch = -0.5f,
                Volume = 0.5f,
            }, player.Center);
        }

        private static void ConsumeChipLocally(Player player, RamUpgradeKind kind,
            int preferredSlot) {
            int chipType = RamNet.GetUpgradeChipType(kind);
            int slot = FindChipSlot(player, preferredSlot, chipType);
            if (slot < 0) {
                //开箱左键拿起后直接使用:芯片挂在鼠标上(槽位 58),
                //背包扫描(0~57)永远找不到,不扣即无限使用(反馈十二·#45/#86)
                if (TryConsumeMouseChip(player, chipType)) {
                    return;
                }
                //一个 RTT 内芯片被丢弃或存走：升级已在权威端落账，无法回滚，只留痕
                CWRMod.Instance.Logger.Warn(
                    $"RAM upgrade settled without chip (type {chipType})");
                return;
            }

            Item chip = player.inventory[slot];
            chip.stack--;
            if (chip.stack <= 0) {
                chip.TurnToAir();
            }
        }

        /// <summary>从鼠标物品上扣一张芯片；槽位 58 与 Main.mouseItem 可能是两个引用，两边都要对齐</summary>
        private static bool TryConsumeMouseChip(Player player, int chipType) {
            const int MouseSlot = 58;
            Item mouse = Main.mouseItem;
            Item mirror = player.inventory.Length > MouseSlot ? player.inventory[MouseSlot] : null;
            Item target = IsChipStack(mouse, chipType) ? mouse
                : IsChipStack(mirror, chipType) ? mirror : null;
            if (target == null) {
                return false;
            }
            target.stack--;
            if (target.stack <= 0) {
                target.TurnToAir();
            }
            //另一份引用若不是同一实例则同步数量,防双扣或残影
            Item other = ReferenceEquals(target, mouse) ? mirror : mouse;
            if (other != null && !ReferenceEquals(other, target) && IsChipStack(other, chipType)) {
                other.stack = target.stack > 0 ? target.stack : 0;
                if (other.stack <= 0) {
                    other.TurnToAir();
                }
            }
            return true;
        }

        /// <summary>优先请求时的原格，被挪动过则按类型全背包找一遍</summary>
        private static int FindChipSlot(Player player, int preferredSlot, int chipType) {
            if (chipType <= ItemID.None || player.inventory == null) {
                return -1;
            }
            int count = Math.Min(Main.InventorySlotsTotal, player.inventory.Length);
            if (preferredSlot >= 0 && preferredSlot < count
                && IsChipStack(player.inventory[preferredSlot], chipType)) {
                return preferredSlot;
            }
            for (int i = 0; i < count; i++) {
                if (IsChipStack(player.inventory[i], chipType)) {
                    return i;
                }
            }
            return -1;
        }

        private static bool IsChipStack(Item item, int chipType)
            => item != null && !item.IsAir && item.type == chipType && item.stack > 0;
    }
}
