using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Victors
{
    /// <summary>
    /// 本地端 Victor 幸福度快照；GetShoppingSettings 每次调用都会分配临时集合，
    /// 这里按帧间隔缓存，供对话/诊所 UI 与展示价换算使用
    /// </summary>
    internal static class VictorMood
    {
        private const int RefreshIntervalFrames = 30;

        private static double cachedAdjustment = 1.0;
        private static string cachedReport = string.Empty;
        private static int cachedVictorWho = -1;
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
            cachedVictorWho = -1;
            cachedAdjustment = 1.0;
            cachedReport = string.Empty;
        }

        private static void Refresh() {
            if (Main.dedServ) {
                return;
            }
            int who = VictorSession.BoundWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                Invalidate();
                return;
            }
            NPC victor = Main.npc[who];
            if (victor?.active != true
                || victor.type != ModContent.NPCType<Victor>()) {
                Invalidate();
                return;
            }
            if (who == cachedVictorWho && Main.GameUpdateCount < nextRefreshFrame) {
                return;
            }

            ShoppingSettings settings = Main.ShopHelper
                .GetShoppingSettings(Main.LocalPlayer, victor);
            cachedAdjustment = settings.PriceAdjustment;
            cachedReport = settings.HappinessReport ?? string.Empty;
            cachedVictorWho = who;
            nextRefreshFrame = Main.GameUpdateCount + RefreshIntervalFrames;
        }
    }
}
