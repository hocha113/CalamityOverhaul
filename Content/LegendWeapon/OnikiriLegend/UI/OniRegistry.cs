using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    internal enum OniGhostState : byte
    {
        Ready,
        Dormant,
        Archive,
    }

    internal sealed class OniGhostEntry
    {
        public string Key;
        public Func<string> Name;
        public Func<string> Origin;
        public Func<string> Power;
        public float Mastery;
        public float MasteryCost;
        public float ErosionCost;
        public OniGhostState State;
        public bool CanEquip;

        public bool HasName => Name != null;
        public bool IsDormant => State == OniGhostState.Dormant;
        public bool IsArchive => State == OniGhostState.Archive;
        public bool HasEyes => CanEquip && !IsDormant;
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

        public static bool IsEquippedDormant => EquippedEntry?.IsDormant == true;

        public static bool TrySetEquipped(Item sourceItem, string key, Action<bool> completed = null)
            => source?.TrySetEquipped(sourceItem, key, completed) == true;
    }
}
