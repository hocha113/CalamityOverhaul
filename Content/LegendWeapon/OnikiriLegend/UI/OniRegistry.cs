using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>铭刻条目状态</summary>
    internal enum OniGhostState : byte
    {
        /// <summary>稳固,干墨完章</summary>
        Engraved,
        /// <summary>躁动,洇血裂章</summary>
        Restless,
        /// <summary>封印,札糊名讳</summary>
        Sealed,
        /// <summary>未知,墨涂空栏</summary>
        Unknown,
    }

    /// <summary>簿条目,文本 <see cref="Func{String}"/> 惰性取当前语言</summary>
    internal sealed class OniGhostEntry
    {
        /// <summary>稳定键(存档/挂接)</summary>
        public string Key;
        /// <summary>鬼名</summary>
        public Func<string> Name;
        /// <summary>来历残句</summary>
        public Func<string> Origin;
        /// <summary>赋予的力</summary>
        public Func<string> Power;
        /// <summary>驾驭度 0~1</summary>
        public float Mastery;
        /// <summary>条目状态</summary>
        public OniGhostState State;

        /// <summary>有可显示名讳(封印有名糊住,未知无名)</summary>
        public bool HasName => State != OniGhostState.Unknown && Name != null;
        /// <summary>细节板是否点眼(封印/未知否)</summary>
        public bool HasEyes => State == OniGhostState.Engraved || State == OniGhostState.Restless;
    }

    /// <summary>簿面源,常为 <see cref="OniWraithSource"/>,经 <see cref="OniRegistry.SetSource"/> 挂</summary>
    internal interface IOniGhostSource
    {
        IReadOnlyList<OniGhostEntry> Entries { get; }
    }

    /// <summary>点鬼簿入口,只读聚合,未挂接为空簿</summary>
    internal static class OniRegistry
    {
        private static IOniGhostSource source;

        /// <summary>挂源,null 空簿</summary>
        public static void SetSource(IOniGhostSource s) => source = s;

        public static IReadOnlyList<OniGhostEntry> Entries => source?.Entries ?? Array.Empty<OniGhostEntry>();

        /// <summary>总驾驭度,已铭刻均值,空簿 0</summary>
        public static float TotalMastery {
            get {
                float sum = 0f;
                int count = 0;
                foreach (OniGhostEntry e in Entries) {
                    if (e.State == OniGhostState.Engraved || e.State == OniGhostState.Restless) {
                        sum += e.Mastery;
                        count++;
                    }
                }
                return count > 0 ? sum / count : 0f;
            }
        }

        /// <summary>危态,有躁动或总驾驭过低</summary>
        public static bool InDanger {
            get {
                foreach (OniGhostEntry e in Entries) {
                    if (e.State == OniGhostState.Restless) {
                        return true;
                    }
                }
                return TotalMastery < 0.35f && Entries.Count > 0;
            }
        }
    }
}
