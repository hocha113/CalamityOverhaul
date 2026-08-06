using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    /// <summary>
    /// 刀縁事件总线。所有入口统一先核「手中是鬼切」与「本机 owner」，
    /// 再按信道分发；判定失败一律静默，不得改动招式本身的任何行为
    /// </summary>
    internal static class OniMeiDeedEvents
    {
        /// <summary>本机 owner 且手中确是鬼切时取出资源层；否则 null</summary>
        private static OnikiriPlayer ResolveHolder(Player player) {
            if (player == null || Main.dedServ || player.whoAmI != Main.myPlayer || player.dead
                || !player.TryGetModPlayer(out OnikiriPlayer onikiri)) {
                return null;
            }
            Item item = player.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<OnikiriItem>()
                ? onikiri
                : null;
        }

        //==================== 信道入口 ====================

        /// <summary>以鬼切招式了结一个目标（owner 端，招式的 OnHitNPC 内调用）</summary>
        internal static void NotifyKill(Player player, NPC killed, OniMeiDeedKillSource source) {
            OnikiriPlayer onikiri = ResolveHolder(player);
            if (onikiri == null || killed == null) {
                return;
            }
            NPC root = OniMeiCombat.ResolveEffectRoot(killed) ?? killed;
            if (root.active && root.life > 0) {
                return;
            }
            if (!onikiri.DeedTracker.TryClaimKill(root.whoAmI, root.type)) {
                return;
            }
            Dispatch(onikiri, OniMeiDeedChannel.Kill,
                new OniMeiDeedContext(player, onikiri.DeedTracker, root, 1, source));
        }

        /// <summary>一次疾走的穿身结算（穿过的不同主体数）</summary>
        internal static void NotifyDashPierce(Player player, int rootCount) {
            OnikiriPlayer onikiri = ResolveHolder(player);
            if (onikiri == null || rootCount <= 0) {
                return;
            }
            Dispatch(onikiri, OniMeiDeedChannel.DashPierce,
                new OniMeiDeedContext(player, onikiri.DeedTracker, amount: rootCount));
        }

        /// <summary>斩断一张面影纸型</summary>
        internal static void NotifyOmokageSever(Player player) {
            OnikiriPlayer onikiri = ResolveHolder(player);
            if (onikiri == null) {
                return;
            }
            Dispatch(onikiri, OniMeiDeedChannel.OmokageSever,
                new OniMeiDeedContext(player, onikiri.DeedTracker, amount: 1));
        }

        /// <summary>樱流巡航帧：先推账本再分发</summary>
        internal static void NotifySakuraTick(Player player) {
            OnikiriPlayer onikiri = ResolveHolder(player);
            if (onikiri == null) {
                return;
            }
            onikiri.DeedTracker.TickSakuraFlight(Main.raining);
            Dispatch(onikiri, OniMeiDeedChannel.SakuraTick,
                new OniMeiDeedContext(player, onikiri.DeedTracker));
        }

        /// <summary>持刀逐帧（缩放帧）：先推账本再分发</summary>
        internal static void NotifyHeldTick(Player player, bool holding) {
            if (player == null || Main.dedServ || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out OnikiriPlayer onikiri)) {
                return;
            }
            onikiri.DeedTracker.Tick(player, holding);
            if (!holding) {
                return;
            }
            Dispatch(onikiri, OniMeiDeedChannel.HeldTick,
                new OniMeiDeedContext(player, onikiri.DeedTracker));
        }

        //==================== 账本脉冲（不直接结縁） ====================

        internal static void NotifySakuraEnd(Player player)
            => ResolveHolder(player)?.DeedTracker.EndSakuraFlight();

        /// <summary>受伤：断静止/立定连续条件，但静止段记一笔"挨过打"</summary>
        internal static void NotifyHurt(Player player) {
            if (player != null && !Main.dedServ && player.whoAmI == Main.myPlayer
                && player.TryGetModPlayer(out OnikiriPlayer onikiri)) {
                onikiri.DeedTracker.NotifyHurt();
            }
        }

        internal static void NotifyBladeHit(Player player)
            => ResolveHolder(player)?.DeedTracker.NotifyBladeHit();

        internal static void NotifyExecutionSpent(Player player)
            => ResolveHolder(player)?.DeedTracker.NotifyExecutionSpent();

        //==================== 分发 ====================

        private static void Dispatch(OnikiriPlayer onikiri, OniMeiDeedChannel channel,
            in OniMeiDeedContext context) {
            List<OniMeiDeed> bucket = OniMeiDeedRegistry.OfChannel(channel);
            if (bucket.Count == 0) {
                return;
            }
            OniMeiDeedProgress progress = onikiri.Deeds;
            bool settledAny = false;
            foreach (OniMeiDeed deed in bucket) {
                if (progress.IsSettled(context.Player, deed)) {
                    continue;
                }
                int amount = deed.Test(in context);
                if (amount <= 0) {
                    continue;
                }
                if (progress.Advance(context.Player, deed, amount, deed.MarkOf(in context))) {
                    OniMeiOwned.Unlock(context.Player, deed.MeiKey);
                    OniMeiDeedRite.GrantRubbing(context.Player, deed.MeiKey);
                    settledAny = true;
                }
            }
            //只在结縁那一帧推快照：进度本身服务器不做校验，逐帧同步纯属浪费
            if (settledAny) {
                OnikiriNet.SendDeedSnapshot(context.Player);
            }
        }
    }
}
