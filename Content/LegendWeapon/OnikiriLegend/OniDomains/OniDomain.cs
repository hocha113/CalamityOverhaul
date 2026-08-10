using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains
{
    /// <summary>鬼域阶段</summary>
    public enum OniDomainPhase : byte
    {
        Closed,
        /// <summary>一刀开域，墨水浸染</summary>
        Opening,
        /// <summary>表世界、泛黄和纸</summary>
        Omote,
        /// <summary>表里翻转、死寂→负片→纸层剥落</summary>
        Flipping,
        /// <summary>里世界、水墨阴间</summary>
        Ura,
        /// <summary>收域、墨水退回裂口</summary>
        Closing
    }

    /// <summary>鬼切领域弹幕</summary>
    public static class OniDomain
    {
        //Opening 时序、鬼眼浮现→睁眼→勾玉狂旋→爆域

        public const int EyeEmergeFrames = 22;      //闭眼轮廓浮现，灵体汇聚

        public const int EyeOpenFrames = 10;        //眼睑猛然撑开

        public const int EyeBurstFrames = 8;        //勾玉加速至虹膜闪白

        public const int OpenSpreadFrames = 54;     //墨浪爆扩全屏（爆冲→滞行→吞没三段）

        //Flipping 时序

        public const int PreSilenceToUra = 55;      //入里前死寂

        public const int PreSilenceToOmote = 20;    //回表前停顿

        public const int FlashFrames = 7;           //负片闪

        public const int PeelFrames = 60;           //纸层剥落

        public const int SettleFrames = 18;         //落定

        //Closing 时序、眼睛重现→墨水吸回→阖眼

        public const int CloseEyeFrames = 10;       //眼睛重现

        public const int CloseRetractFrames = 52;   //墨水吸回眼中（扫入→滞行→吸尽三段）

        public const int CloseBlinkFrames = 12;     //阖眼收尾

        /// <summary>本地玩家域状态，服务器与主菜单返回 null</summary>
        public static OniDomainPlayer Local {
            get {
                if (Main.dedServ || Main.gameMenu) {
                    return null;
                }
                Player player = Main.LocalPlayer;
                if (player == null || !player.active) {
                    return null;
                }
                return player.GetModPlayer<OniDomainPlayer>();
            }
        }

        /// <summary>
        /// 本机屏幕上正在生效的那个域：自己的优先，否则取范围内最近的他人领域。
        /// 世界级表现（天空/调色/光照/装饰/音效）一律读它，HUD 与面影仍读 <see cref="Local"/>
        /// </summary>
        public static OniDomainPlayer Viewed { get; private set; }

        /// <summary>观看半径。域是屏幕级效果，施术者进了视野一圈才把人卷进去</summary>
        private static float ViewRange
            => MathF.Max(Main.screenWidth, Main.screenHeight) * 0.75f + 480f;

        /// <summary>已在观看的那份放宽半径，人在边界来回走不会闪断</summary>
        private const float ViewRangeHysteresis = 1.35f;

        private static int viewedIndex = -1;

        /// <summary>观看域平滑系数 0~1，驱动光照/天空/装饰</summary>
        public static float ViewedUraSmooth => Viewed?.UraSmooth ?? 0f;

        /// <summary>逐帧重选主导域，须在推进各玩家状态机之前调用</summary>
        internal static void RefreshViewed() {
            Viewed = null;
            Player local = Main.dedServ || Main.gameMenu ? null : Main.LocalPlayer;
            if (local?.active != true) {
                viewedIndex = -1;
                return;
            }

            OniDomainPlayer own = local.GetModPlayer<OniDomainPlayer>();
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
                    || !other.TryGetModPlayer(out OniDomainPlayer domain)
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

        /// <summary>各端逐帧推进全体活跃玩家的域，远端形态由施术者的转播兜住</summary>
        internal static void UpdateAll() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true
                    && player.TryGetModPlayer(out OniDomainPlayer domain)) {
                    domain.UpdateLocal();
                }
            }
        }

        /// <summary>开域。Closed 全新开域，Closing 中途反悔从当前覆盖续开</summary>
        public static bool Open(Player player) => player.GetModPlayer<OniDomainPlayer>().OpenDomain();

        /// <summary>收域，Opening/Omote/Ura 可用；开域中途收则从当前覆盖原路吸回</summary>
        public static bool Close(Player player) => player.GetModPlayer<OniDomainPlayer>().CloseDomain();

        /// <summary>表里翻转，Omote/Ura 稳态可用，方向自动</summary>
        public static bool Flip(Player player) => player.GetModPlayer<OniDomainPlayer>().FlipDomain();

        /// <summary>关→开，开→关，收域中→续开</summary>
        public static bool Toggle(Player player) {
            OniDomainPlayer odp = player.GetModPlayer<OniDomainPlayer>();
            return odp.Phase == OniDomainPhase.Closed || odp.Phase == OniDomainPhase.Closing
                ? odp.OpenDomain() : odp.CloseDomain();
        }

        public static OniDomainPhase GetPhase(Player player) => player.GetModPlayer<OniDomainPlayer>().Phase;

        /// <summary>开阖命令。返回是否受理；<paramref name="busy"/></summary>
        internal static bool TryToggle(Player player, out bool busy,
            OnikiriDomainCommandSource source = OnikiriDomainCommandSource.Keybind) {
            busy = false;
            OniDomainPlayer odp = player.GetModPlayer<OniDomainPlayer>();
            bool accepted;
            switch (odp.Phase) {
                case OniDomainPhase.Closed:
                    //两套全屏世界改写不叠加：本人血湖领域活跃时鬼域不开

                    if (player.GetModPlayer<KikasaDomainPlayer>().AnyActive) {
                        busy = true;
                        accepted = false;
                        break;
                    }
                    accepted = odp.OpenDomain();
                    break;
                case OniDomainPhase.Flipping:
                    //翻转仪式不可打断

                    busy = true;
                    accepted = false;
                    break;
                case OniDomainPhase.Closing:
                    //收到一半再按=反悔续开

                    accepted = odp.OpenDomain();
                    break;
                default:
                    //Opening/Omote/Ura 均可收，开到一半收=原路吸回

                    accepted = odp.CloseDomain();
                    break;
            }
            if (accepted) {
                OnikiriTutorialEvents.FireDomainCommandAccepted(player,
                    OnikiriDomainCommandKind.Toggle, source);
            }
            return accepted;
        }

        /// <summary>表里翻转命令。阖着时先展开到表世界(保证一键到位的手感)； <paramref</summary>
        internal static bool TryFlip(Player player, out bool busy,
            OnikiriDomainCommandSource source = OnikiriDomainCommandSource.Keybind) {
            busy = false;
            OniDomainPlayer odp = player.GetModPlayer<OniDomainPlayer>();
            bool accepted;
            switch (odp.Phase) {
                case OniDomainPhase.Closed:
                    //与血湖领域互斥，同 TryToggle

                    if (player.GetModPlayer<KikasaDomainPlayer>().AnyActive) {
                        busy = true;
                        accepted = false;
                        break;
                    }
                    accepted = odp.OpenDomain();
                    break;
                case OniDomainPhase.Omote:
                case OniDomainPhase.Ura:
                    accepted = odp.FlipDomain();
                    break;
                case OniDomainPhase.Flipping:
                    //已在翻,静默

                    accepted = false;
                    break;
                default:
                    //开域/收域仪式中,翻不动

                    busy = true;
                    accepted = false;
                    break;
            }
            if (accepted) {
                OnikiriTutorialEvents.FireDomainCommandAccepted(player,
                    OnikiriDomainCommandKind.Flip, source);
            }
            return accepted;
        }
    }
}
