using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    internal enum OniGhostState : byte
    {
        Ready,
        Archive,
    }

    internal sealed class OniGhostEntry
    {
        public string Key;
        public Func<string> Name;
        public Func<string> Origin;
        public Func<string> Power;
        /// <summary>该鬼复苏槽 0..1，满格即夺身</summary>
        public float Revival;
        /// <summary>单次役使推进的复苏量</summary>
        public float RevivalCost;
        public float ErosionCost;
        public OniGhostState State;
        public bool CanEquip;

        public bool HasName => Name != null;
        public bool IsArchive => State == OniGhostState.Archive;
        public bool HasEyes => CanEquip;
        /// <summary>复苏危险区（≥0.7）：UI 危态反馈统一读这里</summary>
        public bool InDanger => CanEquip
            && Revival >= Content.Wraiths.Runtime.WraithPlayer.RevivalDangerLine;
        /// <summary>再役使一次即满格夺身</summary>
        public bool NextUseFills => CanEquip && RevivalCost > 0f
            && Revival + RevivalCost >= 1f - 0.0001f;
    }

    internal interface IOniGhostSource
    {
        IReadOnlyList<OniGhostEntry> Entries { get; }
        /// <summary>三个结印槽的 Key（含空槽），长度恒为 <see cref="OniRegistry.SlotCount"/></summary>
        IReadOnlyList<string> SlotKeys { get; }
        float Erosion { get; }
        bool TrySetSlot(Item sourceItem, int slot, string key, Action<bool> completed);
    }

    internal static class OniRegistry
    {
        /// <summary>结印槽位数，与 <see cref="Content.Wraiths.Runtime.WraithPlayer.SlotCount"/> 同口径</summary>
        public const int SlotCount = 3;

        private static readonly string[] emptySlots = [string.Empty, string.Empty, string.Empty];
        private static IOniGhostSource source;

        public static void SetSource(IOniGhostSource value) => source = value;

        public static IReadOnlyList<OniGhostEntry> Entries => source?.Entries ?? Array.Empty<OniGhostEntry>();
        public static IReadOnlyList<string> SlotKeys => source?.SlotKeys ?? emptySlots;
        public static float Erosion => source?.Erosion ?? 0f;

        public static string SlotKey(int slot) {
            IReadOnlyList<string> slots = SlotKeys;
            return slot >= 0 && slot < slots.Count ? slots[slot] ?? string.Empty : string.Empty;
        }

        public static OniGhostEntry EntryOf(string key) {
            if (string.IsNullOrEmpty(key)) {
                return null;
            }
            foreach (OniGhostEntry entry in Entries) {
                if (entry.Key == key) {
                    return entry;
                }
            }
            return null;
        }

        public static OniGhostEntry SlotEntry(int slot) => EntryOf(SlotKey(slot));

        /// <summary>该鬼所在槽号；不在盘上返回 -1。</summary>
        public static int SlotOf(string key) {
            if (string.IsNullOrEmpty(key)) {
                return -1;
            }
            IReadOnlyList<string> slots = SlotKeys;
            for (int i = 0; i < slots.Count; i++) {
                if (slots[i] == key) {
                    return i;
                }
            }
            return -1;
        }

        public static bool IsEquipped(string key) => SlotOf(key) >= 0;

        /// <summary>第一个空槽；满盘返回 -1。</summary>
        public static int FirstFreeSlot() {
            IReadOnlyList<string> slots = SlotKeys;
            for (int i = 0; i < slots.Count; i++) {
                if (string.IsNullOrEmpty(slots[i])) {
                    return i;
                }
            }
            return -1;
        }

        public static int EquippedCount {
            get {
                int count = 0;
                IReadOnlyList<string> slots = SlotKeys;
                for (int i = 0; i < slots.Count; i++) {
                    if (!string.IsNullOrEmpty(slots[i])) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>盘上任一只进了危险区。</summary>
        public static bool IsEquippedInDanger {
            get {
                for (int i = 0; i < SlotCount; i++) {
                    if (SlotEntry(i)?.InDanger == true) {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool TrySetSlot(Item sourceItem, int slot, string key,
            Action<bool> completed = null)
            => source?.TrySetSlot(sourceItem, slot, key, completed) == true;
    }
}
