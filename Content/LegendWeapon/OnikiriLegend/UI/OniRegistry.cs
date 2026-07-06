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

    /// <summary>簿面数据源。玩法层实现本接口后经 <see cref="OniRegistry.SetSource"/> 挂接，未挂接时走演示名录</summary>
    internal interface IOniGhostSource
    {
        IReadOnlyList<OniGhostEntry> Entries { get; }
    }

    /// <summary>
    /// 点鬼簿数据入口。当前为作者钦定的演示名录（6 条：2 稳固 / 1 躁动 / 1 封印 / 2 未知），
    /// 玩法数据层就绪后用 <see cref="SetSource"/> 替换即可，三屏 UI 只读本类
    /// </summary>
    internal static class OniRegistry
    {
        private static IOniGhostSource source;
        private static readonly List<OniGhostEntry> demoEntries = [];

        /// <summary>挂接真实玩法数据源；传 null 回落演示名录</summary>
        public static void SetSource(IOniGhostSource s) => source = s;

        public static IReadOnlyList<OniGhostEntry> Entries => source?.Entries ?? demoEntries;

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

        /// <summary>由 <see cref="OniRegisterUI.SetStaticDefaults"/> 在本地化注册完成后调用，构建演示名录</summary>
        internal static void BuildDemoEntries() {
            demoEntries.Clear();
            demoEntries.Add(new OniGhostEntry {
                Key = "NoFace",
                Name = () => OniRegisterUI.Ghost1Name.Value,
                Origin = () => OniRegisterUI.Ghost1Origin.Value,
                Power = () => OniRegisterUI.Ghost1Power.Value,
                Mastery = 0.86f,
                State = OniGhostState.Engraved,
            });
            demoEntries.Add(new OniGhostEntry {
                Key = "LanternBoy",
                Name = () => OniRegisterUI.Ghost2Name.Value,
                Origin = () => OniRegisterUI.Ghost2Origin.Value,
                Power = () => OniRegisterUI.Ghost2Power.Value,
                Mastery = 0.58f,
                State = OniGhostState.Engraved,
            });
            demoEntries.Add(new OniGhostEntry {
                Key = "CrimsonBride",
                Name = () => OniRegisterUI.Ghost3Name.Value,
                Origin = () => OniRegisterUI.Ghost3Origin.Value,
                Power = () => OniRegisterUI.Ghost3Power.Value,
                Mastery = 0.16f,
                State = OniGhostState.Restless,
            });
            demoEntries.Add(new OniGhostEntry {
                Key = "WellThing",
                Name = () => OniRegisterUI.Ghost4Name.Value,
                Origin = () => OniRegisterUI.SealedOriginHint.Value,
                Power = () => OniRegisterUI.SealedPowerHint.Value,
                Mastery = 0f,
                State = OniGhostState.Sealed,
            });
            demoEntries.Add(new OniGhostEntry { Key = "Unknown0", State = OniGhostState.Unknown });
            demoEntries.Add(new OniGhostEntry { Key = "Unknown1", State = OniGhostState.Unknown });
        }
    }
}
