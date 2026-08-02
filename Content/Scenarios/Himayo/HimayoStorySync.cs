using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.Himayo.Gifts;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal enum HimayoGiftRepairResult
    {
        Success,
        InvalidPlayer,
        UnknownKey,
        NotCompleted,
    }

    /// <summary>
    /// 真夜进度同步与试炼发放门禁<br/>
    /// 正常 FirstMetHimayo.OnCompleted → PostFirstMetIsComplete<br/>
    /// 兜底 初遇已触发且叙事空闲视为播完、拔刀后硬倒计时到期强制解锁
    /// </summary>
    internal static class HimayoStorySync
    {
        private enum GiftPacketKind : byte
        {
            ReconcileRequest,
            EntitlementBatch,
        }

        /// <summary>拔刀后初遇未落幕约90s强制开试炼，叙事忙时不计</summary>
        public const int TrialUnlockSafetyDuration = 60 * 90;

        public static HimayoStoryData Story
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HimayoStoryData>();

        public static bool FirstMet => Story.FirstMet;

        public static void MarkFirstMet() => Story.FirstMet = true;

        /// <summary>初遇播完，试炼发放门禁</summary>
        public static bool PostFirstMetIsComplete => Story.PostFirstMetIsComplete;

        public static void MarkPostFirstMetComplete() {
            Story.PostFirstMetIsComplete = true;
            Story.TrialUnlockSafetyTicks = 0;
        }

        public static bool ToriiSwordTaken => Story.ToriiSwordTaken;

        public static void MarkToriiSwordTaken() {
            Story.ToriiSwordTaken = true;
            ArmTrialUnlockSafety();
        }

        /// <summary>武装硬倒计时，已完成或已在倒数则跳过</summary>
        public static void ArmTrialUnlockSafety() {
            if (Story.PostFirstMetIsComplete || Story.TrialUnlockSafetyTicks > 0) {
                return;
            }
            Story.TrialUnlockSafetyTicks = TrialUnlockSafetyDuration;
        }

        /// <summary>本地玩家推进倒计时，叙事忙或初遇在播时暂停</summary>
        public static void TickTrialUnlockSafety(Player player) {
            if (player == null || !player.active || player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            if (Story.PostFirstMetIsComplete) {
                Story.TrialUnlockSafetyTicks = 0;
                return;
            }
            //仅鸟居拔刀后硬倒计时，刷刀不开试炼
            if (!Story.ToriiSwordTaken) {
                return;
            }

            ArmTrialUnlockSafety();
            if (Story.TrialUnlockSafetyTicks <= 0) {
                return;
            }

            if (NarrativeTriggerGate.IsBusy || NarrativeRouter.IsActive<FirstMetHimayo>()) {
                return;
            }

            Story.TrialUnlockSafetyTicks--;
            if (Story.TrialUnlockSafetyTicks <= 0) {
                MarkPostFirstMetComplete();
            }
        }

        /// <summary>试炼委托可发门禁，任一兜底成功即可</summary>
        public static bool CanStartOnikiriTrialQuests(Player player) {
            if (player == null || !player.active || !player.HasItem(OnikiriOverride.ID)) {
                return false;
            }
            if (NarrativeTriggerGate.IsBusy) {
                return false;
            }

            if (Story.PostFirstMetIsComplete) {
                //教程门禁：新玩家须完成教程后方可接受试炼委托
                //旧存档兼容：若已有 Boss 礼物进度(教程功能上线前)，自动跳过
                var guide = player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
                if (guide.CompletedVersion < 1) {
                    if (GiftStory.EyeOfCthulhuGift || GiftStory.WallOfFleshGift) {
                        guide.CompletedVersion = 1;
                    }
                    else {
                        return false;
                    }
                }
                return true;
            }

            //软兜底，初遇已触发且未在播(含旧档缺PostFirstMet)
            if (Story.FirstMet && !NarrativeRouter.IsActive<FirstMetHimayo>()) {
                MarkPostFirstMetComplete();
                return true;
            }

            //硬兜底靠Tick，旧档已拔刀则武装倒计时
            if (Story.ToriiSwordTaken) {
                ArmTrialUnlockSafety();
            }

            return false;
        }

        public static HimayoGiftStoryData GiftStory
            => GetGift(Main.LocalPlayer);

        public static HimayoGiftStoryData GetGift(Player player)
            => player?.GetModPlayer<StoryPlayer>().Get<HimayoGiftStoryData>();

        public static bool IsGiftCompleted(Player player, string key) {
            HimayoGiftStoryData data = GetGift(player);
            return HimayoGiftCatalog.TryGet(key, out HimayoGiftEntry entry) && entry.IsCompleted(data);
        }

        public static bool TryEnqueueGift(Player player, string key) {
            if (!IsLocalOwner(player)) {
                return false;
            }

            HimayoGiftStoryData data = GetGift(player);
            if (data == null || !HimayoGiftCatalog.TryGet(key, out HimayoGiftEntry entry) || entry.IsCompleted(data)) {
                return false;
            }

            HimayoGiftCatalog.Sanitize(data);
            if (data.PendingGiftKeys.Contains(entry.MeiKey)) {
                return false;
            }
            data.PendingGiftKeys.Add(entry.MeiKey);
            HimayoGiftCatalog.Sanitize(data);
            return true;
        }

        public static void ApplyEntitlements(Player player, IReadOnlyList<string> keys) {
            if (!IsLocalOwner(player) || keys == null) {
                return;
            }
            int count = Math.Min(keys.Count, HimayoGiftCatalog.GiftCount);
            for (int i = 0; i < count; i++) {
                TryEnqueueGift(player, keys[i]);
            }
            HimayoGiftCatalog.Sanitize(GetGift(player));
        }

        public static bool TryGetNextPending(Player player, out HimayoGiftEntry entry) {
            HimayoGiftStoryData data = GetGift(player);
            HimayoGiftCatalog.Sanitize(data);
            if (data?.PendingGiftKeys != null && data.PendingGiftKeys.Count > 0) {
                return HimayoGiftCatalog.TryGet(data.PendingGiftKeys[0], out entry);
            }
            entry = null;
            return false;
        }

        public static bool CanReceiveGift(Player player, string key) {
            if (!IsLocalOwner(player) || !HimayoGiftCatalog.TryGet(key, out HimayoGiftEntry entry)) {
                return false;
            }
            int itemType = entry.RubbingItemType;
            if (itemType <= ItemID.None) {
                return false;
            }
            Item rubbing = new(itemType);
            return player.ItemSpace(rubbing).CanTakeItemToPersonalInventory;
        }

        public static bool TryClaimGift(Player player, string key) {
            if (!IsLocalOwner(player) || !HimayoGiftCatalog.TryGet(key, out HimayoGiftEntry entry)) {
                return false;
            }

            HimayoGiftStoryData data = GetGift(player);
            HimayoGiftCatalog.Sanitize(data);
            if (data == null || entry.IsCompleted(data) || !data.PendingGiftKeys.Contains(entry.MeiKey)
                || !CanReceiveGift(player, entry.MeiKey)) {
                return false;
            }

            Item rubbing = new(entry.RubbingItemType);
            Item overflow = player.GetItem(player.whoAmI, rubbing,
                GetItemSettings.ItemCreatedFromItemUsage);
            if (!overflow.IsAir) {
                return false;
            }

            OniMeiOwned.Unlock(player, entry.MeiKey);
            entry.SetCompleted(data, true);
            data.PendingGiftKeys.RemoveAll(pending => pending == entry.MeiKey);
            HimayoGiftCatalog.Sanitize(data);
            return true;
        }

        public static HimayoGiftRepairResult RepairGift(Player player, string key, out string canonicalKey) {
            canonicalKey = null;
            if (!IsLocalOwner(player)) {
                return HimayoGiftRepairResult.InvalidPlayer;
            }
            if (!HimayoGiftCatalog.TryResolveKey(key, out HimayoGiftEntry entry)) {
                return HimayoGiftRepairResult.UnknownKey;
            }

            HimayoGiftStoryData data = GetGift(player);
            canonicalKey = entry.MeiKey;
            if (data == null || !entry.IsCompleted(data)) {
                return HimayoGiftRepairResult.NotCompleted;
            }

            entry.SetCompleted(data, false);
            data.PendingGiftKeys.RemoveAll(pending => pending == entry.MeiKey);
            data.PendingGiftKeys.Add(entry.MeiKey);
            HimayoGiftCatalog.Sanitize(data);
            return HimayoGiftRepairResult.Success;
        }

        public static void RequestGiftReconcile() {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                ApplyEntitlements(Main.LocalPlayer, HimayoGiftCatalog.GetWorldEntitlementKeys());
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.HimayoGift);
            packet.Write((byte)GiftPacketKind.ReconcileRequest);
            packet.Send();
        }

        public static void SendWorldEntitlements(int toWho = -1) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }

            List<string> keys = HimayoGiftCatalog.GetWorldEntitlementKeys();
            int count = Math.Min(keys.Count, HimayoGiftCatalog.GiftCount);
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.HimayoGift);
            packet.Write((byte)GiftPacketKind.EntitlementBatch);
            packet.Write((byte)count);
            for (int i = 0; i < count; i++) {
                packet.Write(keys[i]);
            }
            packet.Send(toWho);
        }

        public static void HandleGiftPacket(BinaryReader reader, int whoAmI) {
            GiftPacketKind kind = (GiftPacketKind)reader.ReadByte();
            if (kind == GiftPacketKind.ReconcileRequest) {
                if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers) {
                    SendWorldEntitlements(whoAmI);
                }
                return;
            }

            if (kind != GiftPacketKind.EntitlementBatch) {
                return;
            }

            int declaredCount = reader.ReadByte();
            if (Main.netMode != NetmodeID.MultiplayerClient || declaredCount > HimayoGiftCatalog.GiftCount) {
                return;
            }

            List<string> keys = [];
            for (int i = 0; i < declaredCount; i++) {
                keys.Add(reader.ReadString());
            }
            ApplyEntitlements(Main.LocalPlayer, keys);
        }

        private static bool IsLocalOwner(Player player)
            => player != null && player.active && Main.netMode != NetmodeID.Server
            && player.whoAmI == Main.myPlayer;

        public static bool ReadGift(Func<HimayoGiftStoryData, bool> story, Func<HimayoGiftStoryData, bool> legacy) {
            if (story(GiftStory)) {
                return true;
            }

            return legacy(GiftStory);
        }

        public static void WriteGift(Action<HimayoGiftStoryData> story, Action<HimayoGiftStoryData> legacy) {
            story(GiftStory);
            legacy(GiftStory);
        }
    }
}
