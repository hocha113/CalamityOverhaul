using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>铭刻条目状态</summary>
    internal enum OniGhostState : byte
    {
        /// <summary>已铭刻且驾驭稳固：字迹干透，朱印完整</summary>
        Engraved,
        /// <summary>躁动：驾驭度低，笔画向下洇血，朱印开裂</summary>
        Restless,
        /// <summary>封印中：名讳被封印札糊住</summary>
        Sealed,
        /// <summary>未知：铭位空悬，一段墨涂</summary>
        Unknown,
    }

    /// <summary>
    /// 点鬼簿单条铭刻。文本走 <see cref="Func{String}"/> 惰性取值，
    /// 保证本地化在语言切换后仍取到当前语言
    /// </summary>
    internal sealed class OniGhostEntry
    {
        /// <summary>稳定键（存档/玩法挂接用）</summary>
        public string Key;
        /// <summary>鬼名</summary>
        public Func<string> Name;
        /// <summary>来历残句（规则怪谈短文案）</summary>
        public Func<string> Origin;
        /// <summary>赋予的力</summary>
        public Func<string> Power;
        /// <summary>驾驭度 0~1</summary>
        public float Mastery;
        /// <summary>条目状态</summary>
        public OniGhostState State;

        /// <summary>有可显示的名讳（封印中的鬼有名但被糊住，未知铭位无名）</summary>
        public bool HasName => State != OniGhostState.Unknown && Name != null;
        /// <summary>影绘细节板是否点出鬼火之眼（封印/未知不点眼）</summary>
        public bool HasEyes => State == OniGhostState.Engraved || State == OniGhostState.Restless;
    }

    /// <summary>簿面数据源。常规实现为 <see cref="OniWraithSource"/>（厉鬼框架适配器），经 <see cref="OniRegistry.SetSource"/> 挂接</summary>
    internal interface IOniGhostSource
    {
        IReadOnlyList<OniGhostEntry> Entries { get; }
    }

    /// <summary>
    /// 点鬼簿数据入口，只读聚合。数据自 <see cref="IOniGhostSource"/> 来，
    /// 未挂接时为空簿；三屏 UI 只读本类
    /// </summary>
    internal static class OniRegistry
    {
        private static IOniGhostSource source;

        /// <summary>挂接数据源；传 null 回落空簿</summary>
        public static void SetSource(IOniGhostSource s) => source = s;

        public static IReadOnlyList<OniGhostEntry> Entries => source?.Entries ?? Array.Empty<OniGhostEntry>();

        /// <summary>总驾驭度：已铭刻(含躁动)条目的驾驭均值，HUD 墨批与危态判定用。空簿返回 0</summary>
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

        /// <summary>危态：存在躁动之鬼，或总驾驭度过低——封印札焦边、绯月竖瞳都吃这个判定</summary>
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
