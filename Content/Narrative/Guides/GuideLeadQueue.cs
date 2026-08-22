using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Guides
{
    /// <summary>教学引导排队参与者，登记到 <see cref="GuideLeadQueue"/>，同时刻只展示一个</summary>
    internal interface IGuideLead
    {
        /// <summary>越小越先展示</summary>
        int GuidePriority { get; }

        /// <summary>未完成且会话前提已成立则占位，压制更低优先级</summary>
        bool GuideReserving { get; }

        /// <summary>可立即展示，前置齐且无对话/过场干扰</summary>
        bool GuideReady { get; }

        /// <summary>饥饿保底放弃时停占位，防死锁</summary>
        void OnGuideAbandoned();
    }

    /// <summary>
    /// 教学引导队列，同时刻至多一个持有展示权。<br/>
    /// 就绪的持有者展示中不抢占；未就绪的占位立刻把展示权让给已经能讲的人，
    /// 不走三分钟饿死，饿死会误触 <see cref="IGuideLead.OnGuideAbandoned"/>（义体/鬼伞会记成看过）。<br/>
    /// 玩家显式点重开走 <see cref="ForceHold"/>，连就绪的持有者也让位，且不当成放弃
    /// </summary>
    internal class GuideLeadQueue : ModSystem
    {
        private static readonly List<IGuideLead> leads = [];
        private static IGuideLead holder;
        //防同刻重复泵
        private static uint lastPumpTick = uint.MaxValue;

        /// <summary>SetStaticDefaults 里登记</summary>
        public static void Register(IGuideLead lead) {
            if (lead != null && !leads.Contains(lead)) {
                leads.Add(lead);
            }
        }

        public override void Unload() {
            leads.Clear();
            ResetRuntime();
        }

        public override void OnWorldUnload() => ResetRuntime();

        //每刻一次，UpdateUI 与 ModifyInterfaceLayers 同持有者
        public override void UpdateUI(Microsoft.Xna.Framework.GameTime gameTime) => PumpOncePerTick();

        /// <summary>是否持有展示权</summary>
        public static bool IsHolder(IGuideLead lead) {
            if (lead == null) {
                return false;
            }
            PumpOncePerTick();
            return holder == lead;
        }

        /// <summary>
        /// 玩家显式要求开讲：立刻把展示权交给指定引导。<br/>
        /// 不调用 <see cref="IGuideLead.OnGuideAbandoned"/>：被挤掉的只是让位，不是被判定放弃
        /// </summary>
        public static void ForceHold(IGuideLead lead) {
            if (lead == null) {
                return;
            }
            holder = lead;
        }

        private static void ResetRuntime() {
            holder = null;
            lastPumpTick = uint.MaxValue;
        }

        private static void PumpOncePerTick() {
            if (Main.gameMenu) {
                ResetRuntime();
                return;
            }
            if (lastPumpTick == Main.GameUpdateCount) {
                return;
            }
            lastPumpTick = Main.GameUpdateCount;
            Pump();
        }

        private static void Pump() {
            if (holder != null && !holder.GuideReserving) {
                holder = null;
            }

            //占着坑但讲不了：让给已经能讲的人。书一摊开鬼切/比目鱼往往仍占位却不 Ready，
            //旧逻辑会空等三分钟再 OnGuideAbandoned，任务书教程整段不可达
            if (holder != null && !holder.GuideReady) {
                IGuideLead ready = HighestReady(except: holder);
                if (ready != null) {
                    holder = ready;
                }
                return;
            }
            if (holder != null) {
                return;
            }

            IGuideLead top = HighestReserver();
            if (top == null) {
                return;
            }
            if (top.GuideReady) {
                holder = top;
                return;
            }
            IGuideLead readyNow = HighestReady();
            if (readyNow != null) {
                holder = readyNow;
            }
        }

        private static IGuideLead HighestReserver() {
            IGuideLead best = null;
            foreach (IGuideLead lead in leads) {
                if (!lead.GuideReserving) {
                    continue;
                }
                if (best == null || lead.GuidePriority < best.GuidePriority) {
                    best = lead;
                }
            }
            return best;
        }

        private static IGuideLead HighestReady(IGuideLead except = null) {
            IGuideLead best = null;
            foreach (IGuideLead lead in leads) {
                if (lead == except || !lead.GuideReady) {
                    continue;
                }
                if (best == null || lead.GuidePriority < best.GuidePriority) {
                    best = lead;
                }
            }
            return best;
        }
    }
}
