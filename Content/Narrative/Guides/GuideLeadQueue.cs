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
    /// 高优先级未就绪也占位压制低优先级；持有者仍占位则不抢占；饥饿保底约 3 分钟放弃卡住的占位者
    /// </summary>
    internal class GuideLeadQueue : ModSystem
    {
        private static readonly List<IGuideLead> leads = [];
        private static IGuideLead holder;
        //饥饿计时连续性
        private static IGuideLead blocker;
        private static int starveTimer;
        //防同刻重复累加饥饿计时
        private static uint lastPumpTick = uint.MaxValue;

        //约3分钟；对话/过场时低优先级本就未就绪，不计入
        private const int StarveTimeout = 60 * 60 * 3;

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

        private static void ResetRuntime() {
            holder = null;
            blocker = null;
            starveTimer = 0;
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
            //不再占位则释放
            if (holder != null && !holder.GuideReserving) {
                holder = null;
            }
            if (holder != null) {
                //未就绪又饿死已就绪低优先级，超时放弃
                if (!holder.GuideReady && HasLowerReadyThan(holder)) {
                    if (blocker != holder) {
                        blocker = holder;
                        starveTimer = 0;
                    }
                    if (++starveTimer >= StarveTimeout) {
                        holder.OnGuideAbandoned();
                        holder = null;
                        blocker = null;
                        starveTimer = 0;
                    }
                    return;
                }
                //展示中不抢占
                blocker = null;
                starveTimer = 0;
                return;
            }

            IGuideLead top = HighestReserver();
            if (top == null) {
                blocker = null;
                starveTimer = 0;
                return;
            }
            if (top.GuideReady) {
                holder = top;
                blocker = null;
                starveTimer = 0;
                return;
            }

            //仅有更低优先级已就绪被压制时才累饥饿
            if (!HasLowerReadyThan(top)) {
                blocker = null;
                starveTimer = 0;
                return;
            }
            if (blocker != top) {
                blocker = top;
                starveTimer = 0;
            }
            if (++starveTimer >= StarveTimeout) {
                top.OnGuideAbandoned();
                blocker = null;
                starveTimer = 0;
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

        private static bool HasLowerReadyThan(IGuideLead top) {
            foreach (IGuideLead lead in leads) {
                if (lead != top && lead.GuidePriority > top.GuidePriority && lead.GuideReady) {
                    return true;
                }
            }
            return false;
        }
    }
}
