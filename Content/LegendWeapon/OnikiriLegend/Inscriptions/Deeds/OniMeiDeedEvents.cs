using CalamityOverhaul.Content.Narrative.Common;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    /// <summary>
    /// 致命帧记号。StrikeNPC 在 checkDead 之前调 HitEffect，那一刻 life&lt;=0 是"这一下打死了"的唯一可靠信号：
    /// 有死亡演出的 Boss（神王/普罗维登斯/至尊灾厄一类）会在 CheckDead 里把 life 拉回 1 并留在场上，
    /// 等招式 OnHitNPC 再看时已看不出死没死。体节命中由基类归到 realLife 头上
    /// </summary>
    internal sealed class OniMeiDeedDeathMark : DeathTrackingNPC
    {
        /// <summary>各槽位最近一次致命帧（GameUpdateCount + 1，0 = 未记）</summary>
        private static readonly uint[] lethalTick = new uint[Main.maxNPCs + 1];

        public override void OnNPCDeath(NPC npc) {
            if (Main.dedServ || npc.whoAmI < 0 || npc.whoAmI >= lethalTick.Length) {
                return;
            }
            lethalTick[npc.whoAmI] = Main.GameUpdateCount + 1;
        }

        /// <summary>该 NPC（或其 realLife 头）在本帧挨了致命一击</summary>
        internal static bool DiedThisTick(NPC npc) {
            if (npc == null) {
                return false;
            }
            uint now = Main.GameUpdateCount + 1;
            if (npc.whoAmI >= 0 && npc.whoAmI < lethalTick.Length && lethalTick[npc.whoAmI] == now) {
                return true;
            }
            int rl = npc.realLife;
            return rl >= 0 && rl < Main.maxNPCs && lethalTick[rl] == now;
        }
    }

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

        /// <summary>
        /// 这一击之后目标算不算已了结：已 inactive、血已归零，或本帧刚挨致命一击
        /// （死亡演出型 Boss 血被拉回也算）。招式 OnHitNPC 里的"击杀"分支一律用它，别再手写 active/life
        /// </summary>
        internal static bool StruckDead(NPC target)
            => target != null
            && (!target.active || target.life <= 0 || OniMeiDeedDeathMark.DiedThisTick(target));

        /// <summary>
        /// 了结主体：蠕虫体节归 realLife 头（头刚死已 inactive 也照归，否则每节各报一次、千手会把体节当成不同首领），
        /// 其余走效果主体解析
        /// </summary>
        private static NPC ResolveKillRoot(NPC killed) {
            int rl = killed.realLife;
            if (rl >= 0 && rl < Main.maxNPCs && rl != killed.whoAmI) {
                return Main.npc[rl];
            }
            return OniMeiCombat.ResolveEffectRoot(killed) ?? killed;
        }

        //==================== 信道入口 ====================

        /// <summary>以鬼切招式了结一个目标（owner 端，招式的 OnHitNPC 内调用）</summary>
        internal static void NotifyKill(Player player, NPC killed, OniMeiDeedKillSource source) {
            OnikiriPlayer onikiri = ResolveHolder(player);
            if (onikiri == null || killed == null) {
                return;
            }
            //只认主体的死：独立血量的体节死了、共享血池的头还活着，不算了结那条虫
            NPC root = ResolveKillRoot(killed);
            if (!StruckDead(root)) {
                return;
            }
            if (!onikiri.DeedTracker.TryClaimKill(root.whoAmI, root.type)) {
                return;
            }
            Dispatch(onikiri, OniMeiDeedChannel.Kill,
                new OniMeiDeedContext(player, onikiri.DeedTracker, root, 1, source));
        }

        /// <summary>
        /// 带动作快照的弹幕命中（<see cref="OniMeiActionContext.OnHitNPC"/> 兜底）：
        /// 环斩/刀痕/铭刻附属这类没自行接线的鬼切弹幕落下致命一击时，按动作种类归到对应了结源。
        /// 招式本体已上报过的同一主体由 <see cref="OniMeiDeedTracker.TryClaimKill"/> 去重
        /// </summary>
        internal static void NotifyContextStrike(Projectile projectile, NPC target, OniMeiActionKind kind) {
            if (projectile == null || projectile.owner != Main.myPlayer || !StruckDead(target)) {
                return;
            }
            NotifyKill(Main.player[projectile.owner], target, KillSourceOf(kind));
        }

        private static OniMeiDeedKillSource KillSourceOf(OniMeiActionKind kind) => kind switch {
            OniMeiActionKind.Combo => OniMeiDeedKillSource.Combo,
            OniMeiActionKind.Zanshin => OniMeiDeedKillSource.Zanshin,
            OniMeiActionKind.Annihilate => OniMeiDeedKillSource.Annihilate,
            OniMeiActionKind.Finale or OniMeiActionKind.FinaleCut => OniMeiDeedKillSource.Finale,
            OniMeiActionKind.FlashMark => OniMeiDeedKillSource.FlashMark,
            _ => OniMeiDeedKillSource.Secondary,
        };

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

        /// <summary>樱流巡航帧：先推账本再分发（雨程按秒累计，不要求一口气飞完）</summary>
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
