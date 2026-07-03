using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniDomains
{
    /// <summary>鬼域阶段</summary>
    public enum OniDomainPhase : byte
    {
        Closed,
        /// <summary>一刀开域，墨水浸染</summary>
        Opening,
        /// <summary>表世界：泛黄和纸</summary>
        Omote,
        /// <summary>表里翻转：死寂→负片→纸层剥落</summary>
        Flipping,
        /// <summary>里世界：水墨阴间</summary>
        Ura,
        /// <summary>收域：墨水退回裂口</summary>
        Closing
    }

    /// <summary>
    /// 鬼域对外接口与时序常量，触发交给武器/测试物品调用
    /// <br/>领域无边界，全屏视觉只响应本地玩家自身的域
    /// </summary>
    public static class OniDomain
    {
        //Opening 时序
        public const int SlashRevealFrames = 36;    //裂口白线浮现
        public const int OpenSpreadFrames = 130;    //墨水浸染全屏
        //Flipping 时序
        public const int PreSilenceToUra = 92;      //入里前死寂
        public const int PreSilenceToOmote = 36;    //回表前停顿
        public const int FlashFrames = 7;           //负片闪
        public const int PeelFrames = 86;           //纸层剥落
        public const int SettleFrames = 30;         //落定
        //Closing 时序
        public const int CloseFrames = 132;         //墨水退潮

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
    }
}
