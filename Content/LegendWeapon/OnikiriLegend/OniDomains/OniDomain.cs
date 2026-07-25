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

        public const int EyeEmergeFrames = 26;      //闭眼轮廓浮现，灵体汇聚

        public const int EyeOpenFrames = 12;        //眼睑猛然撑开

        public const int EyeBurstFrames = 10;       //勾玉加速至虹膜闪白

        public const int OpenSpreadFrames = 92;     //墨浪爆扩全屏（爆冲→滞行→吞没三段）

        //Flipping 时序

        public const int PreSilenceToUra = 55;      //入里前死寂

        public const int PreSilenceToOmote = 20;    //回表前停顿

        public const int FlashFrames = 7;           //负片闪

        public const int PeelFrames = 60;           //纸层剥落

        public const int SettleFrames = 18;         //落定

        //Closing 时序、眼睛重现→墨水吸回→阖眼

        public const int CloseEyeFrames = 16;       //眼睛重现

        public const int CloseRetractFrames = 84;   //墨水吸回眼中（扫入→滞行→吸尽三段）

        public const int CloseBlinkFrames = 14;     //阖眼收尾

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

        /// <summary>本地里世界平滑系数 0~1，驱动光照/天空/装饰</summary>
        public static float LocalUraSmooth => Local?.UraSmooth ?? 0f;

        /// <summary>开域，仅 Closed 可用</summary>
        public static bool Open(Player player) => player.GetModPlayer<OniDomainPlayer>().OpenDomain();

        /// <summary>收域，Opening/Omote/Ura 可用</summary>
        public static bool Close(Player player) => player.GetModPlayer<OniDomainPlayer>().CloseDomain();

        /// <summary>表里翻转，Omote/Ura 稳态可用，方向自动</summary>
        public static bool Flip(Player player) => player.GetModPlayer<OniDomainPlayer>().FlipDomain();

        /// <summary>关→开，开→关</summary>
        public static bool Toggle(Player player) {
            OniDomainPlayer odp = player.GetModPlayer<OniDomainPlayer>();
            return odp.Phase == OniDomainPhase.Closed ? odp.OpenDomain() : odp.CloseDomain();
        }

        public static OniDomainPhase GetPhase(Player player) => player.GetModPlayer<OniDomainPlayer>().Phase;

        /// <summary>开阖命令。返回是否受理；<paramref name="busy"/></summary>
        internal static bool TryToggle(Player player, out bool busy) {
            busy = false;
            OniDomainPlayer odp = player.GetModPlayer<OniDomainPlayer>();
            switch (odp.Phase) {
                case OniDomainPhase.Closed:
                    return odp.OpenDomain();
                case OniDomainPhase.Flipping:
                    //翻转仪式不可打断

                    busy = true;
                    return false;
                case OniDomainPhase.Closing:
                    //已在收,冗余按键静默

                    return false;
                default:
                    //Opening/Omote/Ura 均可收

                    return odp.CloseDomain();
            }
        }

        /// <summary>表里翻转命令。阖着时先展开到表世界(保证一键到位的手感)； <paramref</summary>
        internal static bool TryFlip(Player player, out bool busy) {
            busy = false;
            OniDomainPlayer odp = player.GetModPlayer<OniDomainPlayer>();
            switch (odp.Phase) {
                case OniDomainPhase.Closed:
                    return odp.OpenDomain();
                case OniDomainPhase.Omote:
                case OniDomainPhase.Ura:
                    return odp.FlipDomain();
                case OniDomainPhase.Flipping:
                    //已在翻,静默

                    return false;
                default:
                    //开域/收域仪式中,翻不动

                    busy = true;
                    return false;
            }
        }
    }
}
