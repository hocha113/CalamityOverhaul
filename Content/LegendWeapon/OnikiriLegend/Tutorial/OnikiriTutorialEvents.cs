using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    internal enum OnikiriDomainCommandKind : byte
    {
        Toggle,
        Flip,
    }

    internal enum OnikiriDomainCommandSource : byte
    {
        Keybind,
        HudLeft,
        HudRight,
        HudMiddle,
        TutorialFallback,
        TutorialAssist,
    }

    /// <summary>
    /// 鬼切教程轻量语义事件总线。
    /// 由招式模块在正式成功结算点调用 Fire*，教程状态机订阅消费；正式战斗逻辑不反向依赖本类。
    /// 世界切换时务必调用 <see cref="ClearAll"/> 避免跨存档订阅泄漏。
    /// </summary>
    internal static class OnikiriTutorialEvents
    {
        //====事件声明====

        /// <summary>五拍连斩某拍首次命中目标（beat=0..4，target=被命中的NPC）</summary>
        internal static event Action<int, NPC> OnComboBeatHit;

        /// <summary>疾走扫掠穿身：OniFlashStep.MarkSweep 首次经过目标时</summary>
        internal static event Action<NPC> OnDashSweep;

        /// <summary>疾走墨痕纳刀结算：JudgmentFrame 且 marked.Count > 0</summary>
        internal static event Action OnDashJudged;

        /// <summary>樱流化身起飞成功（OniSakuraFlight.Fire 返回非 null）</summary>
        internal static event Action OnSakuraStarted;

        /// <summary>樱流化身操控交还（ReleaseOwner 帧，开放残心窗口）</summary>
        internal static event Action OnSakuraReleased;

        /// <summary>残心斩首次命中目标（OnikiriPlayer.OnZanshinHit grantResources=true）</summary>
        internal static event Action<NPC> OnZanshinHit;

        /// <summary>灭世一闪弹幕成功触发（FireExecutionAnnihilate 返回 true）</summary>
        internal static event Action OnExecutionAnnihilate;

        /// <summary>终结乱舞弹幕成功触发（FireExecutionFinale 返回 true）</summary>
        internal static event Action<NPC> OnExecutionFinale;

        /// <summary>鬼域命令已被正式状态机受理</summary>
        internal static event Action<Player, OnikiriDomainCommandKind, OnikiriDomainCommandSource> OnDomainCommandAccepted;

        /// <summary>鬼域相位稳态落定</summary>
        internal static event Action<Player, OniDomainPhase> OnDomainPhaseSettled;

        /// <summary>肢解落刀成功（OniSeverStrike.StrikeFrame 且 struck && !whiffed）</summary>
        internal static event Action<Player, NPC> OnDismemberLanded;

        //====触发器（供招式模块调用）====

        internal static void FireComboBeatHit(int beatIndex, NPC target)
            => OnComboBeatHit?.Invoke(beatIndex, target);

        internal static void FireDashSweep(NPC target)
            => OnDashSweep?.Invoke(target);

        internal static void FireDashJudged()
            => OnDashJudged?.Invoke();

        internal static void FireSakuraStarted()
            => OnSakuraStarted?.Invoke();

        internal static void FireSakuraReleased()
            => OnSakuraReleased?.Invoke();

        internal static void FireZanshinHit(NPC target)
            => OnZanshinHit?.Invoke(target);

        internal static void FireExecutionAnnihilate()
            => OnExecutionAnnihilate?.Invoke();

        internal static void FireExecutionFinale(NPC target)
            => OnExecutionFinale?.Invoke(target);

        internal static void FireDomainCommandAccepted(Player player, OnikiriDomainCommandKind kind,
            OnikiriDomainCommandSource source)
            => OnDomainCommandAccepted?.Invoke(player, kind, source);

        internal static void FireDomainPhaseSettled(Player player, OniDomainPhase phase)
            => OnDomainPhaseSettled?.Invoke(player, phase);

        internal static void FireDomainPhaseSettled(OniDomainPhase phase) {
            if (!Main.dedServ && Main.LocalPlayer?.active == true) {
                FireDomainPhaseSettled(Main.LocalPlayer, phase);
            }
        }

        internal static void FireDismemberLanded(Player player, NPC target)
            => OnDismemberLanded?.Invoke(player, target);

        internal static void FireDismemberLanded(NPC target) {
            if (!Main.dedServ && Main.LocalPlayer?.active == true) {
                FireDismemberLanded(Main.LocalPlayer, target);
            }
        }

        //====清理====

        /// <summary>世界切换/卸载时清空所有订阅，防跨存档事件泄漏</summary>
        internal static void ClearAll()
        {
            OnComboBeatHit = null;
            OnDashSweep = null;
            OnDashJudged = null;
            OnSakuraStarted = null;
            OnSakuraReleased = null;
            OnZanshinHit = null;
            OnExecutionAnnihilate = null;
            OnExecutionFinale = null;
            OnDomainCommandAccepted = null;
            OnDomainPhaseSettled = null;
            OnDismemberLanded = null;
        }
    }
}
