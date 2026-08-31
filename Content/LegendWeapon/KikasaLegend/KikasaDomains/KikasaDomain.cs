using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>血湖领域阶段</summary>
    public enum KikasaDomainPhase : byte
    {
        Closed,
        /// <summary>浸润→撕开，旧世界如湿纸破裂，血湖自屏底涨起</summary>
        Opening,
        /// <summary>稳态、死寂的血湖与血暮天空；形态由 <see cref="KikasaDomainPlayer.IsRainForm"/> 区分</summary>
        Open,
        /// <summary>收域、水位退落，撕口自外向内长回</summary>
        Closing,
        /// <summary>鬼雨异化翻转：血湖沸腾变色→窥影→180°倒转→落定，结算帧切形态</summary>
        Flipping,
        /// <summary>鬼梦拉入：湖剧烈沸腾、镜中黑犬凝视→180°倒转，红闪结算切进梦侧</summary>
        DreamPull,
        /// <summary>身处鬼梦：红天村落、湖水不见；物品封禁，左键唤犬</summary>
        Dreaming,
        /// <summary>鬼梦归返：湖水自屏底涌回→倒转，暖白闪落定回血湖</summary>
        DreamReturn
    }

    /// <summary>鬼伞血湖领域。架构与 <see cref="OniDomain"/> 同构：门面+观看选择，权威在 <see cref="KikasaDomainPlayer"/></summary>
    public static class KikasaDomain
    {
        //Opening 时序、浸润→撕开；血湖上涨与撕开同窗推进

        public const int SoakFrames = 20;        //湿渍晕开，画面微暗

        public const int TearFrames = 50;        //纸层撕开全屏（爆冲→滞行→吞没三段）

        public const int RiseStartFrame = 10;    //血湖起涨帧（浸润中段水已在屏下涌动）

        public const int RiseFrames = 54;        //血湖从屏底涨到脚下的帧数

        //Closing 时序、水退与纸合共用一段

        public const int CloseFrames = 46;       //撕口自外向内长回

        public const int DrainFrames = 40;       //血湖退落帧数

        //Flipping 时序：开场沸腾+压镜 2026-08 缩 40%（90→54），窥影/倒转/落定时长不动、整体前移

        public const int FlipBoilEnd = 54;       //血湖沸腾变色+压镜（原 90）

        public const int FlipBoilRamp = 34;      //猛地变色陡坡（原 56，同比压缩）

        public const int FlipDwellEnd = 94;      //窥影驻留段（时长仍 40）

        public const int FlipRollEnd = 184;      //180°倒转段（时长仍 90，含反向蓄势）

        public const int FlipTotalFrames = 216;  //落定收尾（时长仍 32）

        /// <summary>翻转结算帧：倒转段时间过半（曲线上 θ≈60°），近全白的白闪掩护下切换形态</summary>
        public const int FlipCommitFrame = 139;

        /// <summary>本地玩家域状态，服务器与主菜单返回 null</summary>
        public static KikasaDomainPlayer Local {
            get {
                if (Main.dedServ || Main.gameMenu) {
                    return null;
                }
                Player player = Main.LocalPlayer;
                if (player == null || !player.active) {
                    return null;
                }
                return player.GetModPlayer<KikasaDomainPlayer>();
            }
        }

        /// <summary>本机屏幕上正在生效的那个域：自己的优先，否则取范围内最近的他人领域</summary>
        public static KikasaDomainPlayer Viewed { get; private set; }

        /// <summary>观看半径。域是屏幕级效果，施术者进了视野一圈才把人卷进去</summary>
        private static float ViewRange
            => MathF.Max(Main.screenWidth, Main.screenHeight) * 0.75f + 480f;

        /// <summary>已在观看的那份放宽半径，人在边界来回走不会闪断</summary>
        private const float ViewRangeHysteresis = 1.35f;

        private static int viewedIndex = -1;

        /// <summary>观看域在场平滑系数 0~1，驱动光照/滤镜/天空</summary>
        public static float ViewedPresence => Viewed?.PresenceSmooth ?? 0f;

        /// <summary>观看域的鬼雨异化混合 0~1，驱动全部血系表现的冷化</summary>
        public static float ViewedRainBlend => Viewed?.RainBlend ?? 0f;

        /// <summary>观看域的鬼梦在场 0~1，驱动梦空/压光与湖面表现关停的交叉渐变</summary>
        public static float ViewedDreamBlend => Viewed?.DreamBlend ?? 0f;

        /// <summary>血湖空气冷调。默认关=原血暮红罩；开=滤镜/Grade暗部/Unify轻罩/低画质回退走瘀青靛</summary>
        public static bool CoolAirGrade => false;

        /// <summary>血系表现色随观看域的鬼雨异化冷化</summary>
        public static Color CoolTint(Color blood, Color rain)
            => Color.Lerp(blood, rain, ViewedRainBlend);

        /// <summary>逐帧重选主导域，须在推进各玩家状态机之前调用</summary>
        internal static void RefreshViewed() {
            Viewed = null;
            Player local = Main.dedServ || Main.gameMenu ? null : Main.LocalPlayer;
            if (local?.active != true) {
                viewedIndex = -1;
                return;
            }

            KikasaDomainPlayer own = local.GetModPlayer<KikasaDomainPlayer>();
            if (own.AnyActive) {
                Viewed = own;
                viewedIndex = local.whoAmI;
                return;
            }

            float range = ViewRange;
            float nearest = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player other = Main.player[i];
                if (i == local.whoAmI || other?.active != true
                    || !other.TryGetModPlayer(out KikasaDomainPlayer domain)
                    || !domain.AnyActive) {
                    continue;
                }
                float limit = i == viewedIndex ? range * ViewRangeHysteresis : range;
                float distance = Vector2.Distance(other.Center, local.Center);
                if (distance > limit || distance >= nearest) {
                    continue;
                }
                nearest = distance;
                nearestIndex = i;
                Viewed = domain;
            }
            viewedIndex = nearestIndex;
        }

        /// <summary>各端逐帧推进全体活跃玩家的域，远端形态由施术者的转播兜住。
        /// 服务器同样推进（2026-08 起持有快照镜像，NPC 减速等权威判定要读湖态）：
        /// 状态机是 PhaseTimer+同步玩家态的确定性函数，FX 全在 IsLocalVisual 门后，服务器恒 false</summary>
        internal static void UpdateAll() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true
                    && player.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                    domain.UpdateLocal();
                }
            }
        }

        /// <summary>开域。Closed 全新开域，Closing 中途反悔从当前覆盖续开</summary>
        public static bool Open(Player player) => player.GetModPlayer<KikasaDomainPlayer>().OpenDomain();

        /// <summary>收域，Opening/Open 可用；开域中途收则从当前覆盖原路合回</summary>
        public static bool Close(Player player) => player.GetModPlayer<KikasaDomainPlayer>().CloseDomain();

        public static KikasaDomainPhase GetPhase(Player player) => player.GetModPlayer<KikasaDomainPlayer>().Phase;

        /// <summary>开阖命令。返回是否受理；busy=此刻不受理但域并非闲置（含与鬼切领域互斥）</summary>
        internal static bool TryToggle(Player player, out bool busy) {
            busy = false;
            KikasaDomainPlayer kdp = player.GetModPlayer<KikasaDomainPlayer>();
            switch (kdp.Phase) {
                case KikasaDomainPhase.Closed:
                case KikasaDomainPhase.Closing:
                    //两套全屏世界改写不叠加：本人鬼切领域活跃时血湖不开
                    if (player.GetModPlayer<OniDomainPlayer>().AnyActive) {
                        busy = true;
                        return false;
                    }
                    return kdp.OpenDomain();
                case KikasaDomainPhase.Flipping:
                case KikasaDomainPhase.DreamPull:
                case KikasaDomainPhase.Dreaming:
                case KikasaDomainPhase.DreamReturn:
                    //倒转/鬼梦进行中收不了域，世界正翻在半空或困在梦里
                    busy = true;
                    return false;
                default:
                    //Opening/Open 均可收，开到一半收=原路合回
                    return kdp.CloseDomain();
            }
        }

        /// <summary>
        /// 鬼雨异化命令，语义对标鬼切表里翻转：域关着时直接开域（血湖形态），
        /// Open 稳态开始异化翻转（血湖↔鬼雨双向同一套演出）；其余阶段 busy 不受理
        /// </summary>
        internal static bool TryMutate(Player player, out bool busy) {
            busy = false;
            KikasaDomainPlayer kdp = player.GetModPlayer<KikasaDomainPlayer>();
            switch (kdp.Phase) {
                case KikasaDomainPhase.Closed:
                case KikasaDomainPhase.Closing:
                    if (player.GetModPlayer<OniDomainPlayer>().AnyActive) {
                        busy = true;
                        return false;
                    }
                    return kdp.OpenDomain();
                case KikasaDomainPhase.Open:
                    return kdp.FlipDomain(out busy);
                default:
                    busy = true;
                    return false;
            }
        }

        /// <summary>
        /// 鬼梦拉入/归返命令：Open 稳态 + 满水位 → 拉入（无资源门）；
        /// Dreaming → 归返；其余阶段 busy 不受理。域关着时不代开，入梦要先有湖。
        /// 倒影醒睡随满水自动走（KikasaDomainPlayer.UpdateReflectionGate），纯氛围指示，
        /// 拉入受理帧会强制点醒它（镜中黑犬是演出主角）
        /// </summary>
        internal static bool TryDreamPull(Player player, out bool busy) {
            busy = false;
            KikasaDomainPlayer kdp = player.GetModPlayer<KikasaDomainPlayer>();
            return kdp.PullDream(out busy);
        }
    }
}
