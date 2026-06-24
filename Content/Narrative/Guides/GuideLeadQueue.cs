using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Guides
{
    /// <summary>
    /// 教学引导排队参与者。所有"首次教学引导"统一登记到 <see cref="GuideLeadQueue"/>，
    /// 由队列仲裁同一时刻只展示一个，按优先级排序，彼此无需相互引用——各自老实排队即可
    /// </summary>
    internal interface IGuideLead
    {
        /// <summary>排队优先级，数值越小越先展示</summary>
        int GuidePriority { get; }

        /// <summary>
        /// 是否占位：本引导尚未完成且其"会话前提"已成立（哪怕还没准备好展示）
        /// 占位会压制更低优先级的引导抢先，从而保证顺序，即使本引导此刻还不能展示
        /// </summary>
        bool GuideReserving { get; }

        /// <summary>是否可立即展示：占位之上，前置全部满足且无对话/过场等干扰</summary>
        bool GuideReady { get; }

        /// <summary>因饥饿保底被队列放弃时调用：应令自身停止占位，避免长期死锁</summary>
        void OnGuideAbandoned();
    }

    /// <summary>
    /// 教学引导统一队列：同一时刻至多一个引导持有"展示权"
    /// <para>· 高优先级即便尚未就绪也会占位、压制低优先级，保证既定顺序；</para>
    /// <para>· 持有者只要仍在占位就不会被抢占（不打断进行中的引导）；</para>
    /// <para>· 饥饿保底：当高优先级占位者长期未就绪、且确有更低优先级引导已就绪被压制时，
    /// 超时后放弃该占位者，避免无限等待。</para>
    /// </summary>
    internal class GuideLeadQueue : ModSystem
    {
        private static readonly List<IGuideLead> leads = [];
        private static IGuideLead holder;
        //当前因未就绪而压制队列的高优先级占位者，用于判断饥饿计时是否连续
        private static IGuideLead blocker;
        private static int starveTimer;
        //本刻是否已仲裁过，避免一刻内多次查询导致饥饿计时被重复累加
        private static uint lastPumpTick = uint.MaxValue;

        //饥饿保底超时：约 3 分钟。正常对话/过场期间低优先级引导本就未就绪，不会计入此计时
        private const int StarveTimeout = 60 * 60 * 3;

        /// <summary>登记一个引导参与者（各引导在 SetStaticDefaults 中调用）</summary>
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

        //每刻仲裁一次，确保 UpdateUI 与 ModifyInterfaceLayers 看到一致的持有者
        public override void UpdateUI(Microsoft.Xna.Framework.GameTime gameTime) => PumpOncePerTick();

        /// <summary>某引导查询自己当前是否持有展示权</summary>
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
            //当前持有者不再占位（完成/放弃/前提消失）→ 释放展示权
            if (holder != null && !holder.GuideReserving) {
                holder = null;
            }
            //有人正在展示且仍占位 → 不抢占，安心等其结束
            if (holder != null) {
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
            //最高占位者已就绪 → 授予展示权
            if (top.GuideReady) {
                holder = top;
                blocker = null;
                starveTimer = 0;
                return;
            }

            //最高占位者尚未就绪：仅当确有更低优先级引导已就绪被压制时，才累计饥饿保底，
            //否则只是安静等待它就绪（例如初遇演出期间，低优先级引导本就未就绪）
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
