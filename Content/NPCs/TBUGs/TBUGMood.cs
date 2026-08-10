using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>
    /// 本地端 TBUG 幸福度快照；GetShoppingSettings 每次调用都会分配临时集合，
    /// 这里按帧间隔缓存，供对话/商店 UI 与展示价换算使用
    /// </summary>
    internal static class TBUGMood
    {
        private const int RefreshIntervalFrames = 30;

        private static double cachedAdjustment = 1.0;
        private static string cachedReport = string.Empty;
        private static int cachedWho = -1;
        private static uint nextRefreshFrame;

        /// <summary>幸福度购物系数（原版 clamp 0.75~1.5；无有效交互对象时为 1）</summary>
        internal static double PriceAdjustment {
            get {
                Refresh();
                return cachedAdjustment;
            }
        }

        /// <summary>心情报告全文（无有效交互对象时为空串）</summary>
        internal static string Report {
            get {
                Refresh();
                return cachedReport;
            }
        }

        internal static void Invalidate() {
            cachedWho = -1;
            cachedAdjustment = 1.0;
            cachedReport = string.Empty;
        }

        private static void Refresh() {
            if (Main.dedServ) {
                return;
            }
            int who = TBUGSession.BoundWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                Invalidate();
                return;
            }
            NPC tbug = Main.npc[who];
            if (tbug?.active != true
                || tbug.type != ModContent.NPCType<TBUG>()) {
                Invalidate();
                return;
            }
            if (who == cachedWho && Main.GameUpdateCount < nextRefreshFrame) {
                return;
            }

            ShoppingSettings settings = Main.ShopHelper
                .GetShoppingSettings(Main.LocalPlayer, tbug);
            cachedAdjustment = settings.PriceAdjustment;
            cachedReport = settings.HappinessReport ?? string.Empty;
            cachedWho = who;
            nextRefreshFrame = Main.GameUpdateCount + RefreshIntervalFrames;
        }
    }
}
