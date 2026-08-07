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
        string EquippedKey { get; }
        float Erosion { get; }
        bool TrySetEquipped(Item sourceItem, string key, Action<bool> completed);
    }

    internal static class OniRegistry
    {
        private static IOniGhostSource source;

        public static void SetSource(IOniGhostSource value) => source = value;

        public static IReadOnlyList<OniGhostEntry> Entries => source?.Entries ?? Array.Empty<OniGhostEntry>();
        public static string EquippedKey => source?.EquippedKey ?? string.Empty;
        public static float Erosion => source?.Erosion ?? 0f;

        public static OniGhostEntry EquippedEntry {
            get {
                string key = EquippedKey;
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
        }

        public static bool IsEquippedInDanger => EquippedEntry?.InDanger == true;

        public static bool TrySetEquipped(Item sourceItem, string key, Action<bool> completed = null)
            => source?.TrySetEquipped(sourceItem, key, completed) == true;
    }
}
