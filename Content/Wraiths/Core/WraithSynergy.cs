using CalamityOverhaul.Content.Wraiths.Marks;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>交互规则的效果通道：声明这条规则作用在哪类点位上。</summary>
    internal enum WraithSynergyChannel : byte
    {
        /// <summary>对带状态猎物的伤害倍率，结算处走 <see cref="WraithSynergy.Factor"/></summary>
        DamageAmp,
        /// <summary>索敌加权或优先级</summary>
        TargetBias,
        /// <summary>可达性/行为开关：行为分支留在模块内，触发条件走规则查询</summary>
        ReachRule,
        /// <summary>结算在框架其他层（WraithPlayer 的顶劫、状态元数据的滞时），规则仅作 UI 声明</summary>
        PlayerChannel,
    }

    /// <summary>
    /// 一条灵异叠加规则：某鬼声明"我对带某状态的猎物（或盘上发某状态的同伴）有什么反应"。<br/>
    /// 规则绑定状态语义而非具体某只鬼，任何发同一状态的新鬼自动接入既有反应。<br/>
    /// 文案与行为同源：结印盘边名从这些声明推导，不再另维护一张配对表
    /// </summary>
    internal sealed class WraithSynergyRule
    {
        /// <summary>规则名，调试与日志用</summary>
        public string Id { get; init; }
        /// <summary>规则归属（消费方）的鬼 Key，注册时由框架回填</summary>
        public string OwnerKey { get; internal set; }
        /// <summary>触发状态；仅 <see cref="WildcardPartner"/> 规则可为 None</summary>
        public WraithMark Trigger { get; init; }
        public WraithSynergyChannel Channel { get; init; }
        /// <summary>量级曲线：印记强度 0..1 → 倍率；null 视作恒 1</summary>
        public Func<float, float> Magnitude { get; init; }
        /// <summary>板级规则：看盘上有谁发射该状态，而非猎物身上的印（枯手雨中加手位）</summary>
        public bool BoardScope { get; init; }
        /// <summary>通配伙伴：与任意同盘鬼的边都亮这条规则（喜堂/顶劫）</summary>
        public bool WildcardPartner { get; init; }
        /// <summary>边名；同名规则在 UI 上归为一组。用委托取值，避开本地化加载时序</summary>
        public Func<LocalizedText> Name { get; init; }
        /// <summary>边说明；一组同名规则挂一条即可，其余留空</summary>
        public Func<LocalizedText> Note { get; init; }
        /// <summary>UI 取名优先级，大者先</summary>
        public int UiPriority { get; init; }
    }

    /// <summary>
    /// 灵异叠加注册表与查询面：收集各鬼声明的发射状态与消费规则，
    /// 行为结算与结印盘边名推导共用这一份事实
    /// </summary>
    internal static class WraithSynergy
    {
        private static readonly List<WraithSynergyRule> rules = [];
        private static bool collected;

        /// <summary>惰性收集：首次查询才遍历，确保全部定义与本地化已就绪。</summary>
        private static List<WraithSynergyRule> Rules {
            get {
                if (!collected) {
                    collected = true;
                    foreach (WraithDefinition definition in WraithRegistry.Usable) {
                        foreach (WraithSynergyRule rule in definition.BuildSynergyRules()) {
                            if (rule == null) {
                                continue;
                            }
                            rule.OwnerKey = definition.Key;
                            rules.Add(rule);
                        }
                    }
                }
                return rules;
            }
        }

        internal static void Unload() {
            rules.Clear();
            collected = false;
        }

        //==== 行为查询 ====

        /// <summary>猎物身上是否有触发该规则的状态（施加者隔离）。</summary>
        internal static bool TriggersOn(WraithSynergyRule rule, NPC npc, int owner)
            => rule != null && rule.Trigger != WraithMark.None
                && WraithMarks.Has(npc, rule.Trigger, owner);

        /// <summary>规则量级：触发时按印记强度走曲线；未触发返回 1（乘法恒等）。</summary>
        internal static float Factor(WraithSynergyRule rule, NPC npc, int owner) {
            if (!TriggersOn(rule, npc, owner) || rule.Magnitude == null) {
                return 1f;
            }
            float power = MathHelper.Clamp(WraithMarks.PowerOf(npc, rule.Trigger, owner), 0f, 1f);
            return rule.Magnitude(power);
        }

        /// <summary>盘上全部役鬼的发射状态并集；空盘为 None。</summary>
        internal static WraithMark ResolveBoardEmits(WraithPlayer wraithPlayer) {
            WraithMark emits = WraithMark.None;
            if (wraithPlayer == null) {
                return emits;
            }
            foreach (string key in wraithPlayer.EquippedKeys) {
                if (WraithRegistry.TryGetUsable(key, out WraithDefinition definition)) {
                    emits |= definition.Emits;
                }
            }
            return emits;
        }

        //==== UI 推导 ====

        /// <summary>
        /// 两只鬼之间那条边的名字与说明，从双方声明推导：
        /// 一方的规则被另一方的发射触发（或通配）即亮；
        /// 全不沾则落到「相唤」，它们仍然在互相催醒，边不该是死的
        /// </summary>
        internal static (LocalizedText Name, LocalizedText Note) EdgePair(string keyA, string keyB) {
            if (string.IsNullOrEmpty(keyA) || string.IsNullOrEmpty(keyB) || keyA == keyB
                || !WraithRegistry.TryGetUsable(keyA, out WraithDefinition a)
                || !WraithRegistry.TryGetUsable(keyB, out WraithDefinition b)) {
                return (null, null);
            }
            WraithSynergyRule best = null;
            foreach (WraithSynergyRule rule in Rules) {
                if (Connects(rule, a, b) && (best == null || rule.UiPriority > best.UiPriority)) {
                    best = rule;
                }
            }
            if (best == null) {
                return (WraithCovenText.CallName, WraithCovenText.CallNote);
            }
            LocalizedText name = best.Name?.Invoke();
            LocalizedText note = best.Note?.Invoke();
            if (note == null && name != null) {
                //说明挂在同名组的某一条上，补找
                foreach (WraithSynergyRule rule in Rules) {
                    if (rule != best && rule.Name?.Invoke() == name && Connects(rule, a, b)) {
                        note = rule.Note?.Invoke();
                        if (note != null) {
                            break;
                        }
                    }
                }
            }
            return (name, note);
        }

        /// <summary>该规则是否连接这条边：属于一端，且被另一端的发射触发或通配。</summary>
        private static bool Connects(WraithSynergyRule rule, WraithDefinition a, WraithDefinition b) {
            WraithDefinition partner;
            if (rule.OwnerKey == a.Key) {
                partner = b;
            }
            else if (rule.OwnerKey == b.Key) {
                partner = a;
            }
            else {
                return false;
            }
            return rule.WildcardPartner
                || rule.Trigger != WraithMark.None && (partner.Emits & rule.Trigger) != 0;
        }
    }
}
