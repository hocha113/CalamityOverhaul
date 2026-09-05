using CalamityOverhaul.Common;

namespace CalamityOverhaul.Content.LegendWeapon
{
    /// <summary>
    /// 传奇领域的旁观策略。他人领域对本机只有两种身份：
    /// <list type="bullet">
    /// <item>旁观：受客户端设置 <see cref="CWRClientConfig.HideOtherDomains"/> 屏蔽</item>
    /// <item>被触达：该域此刻在功能上作用于本机玩家（站上/没入其湖面、身处其鬼梦圆、被其沉人），不可屏蔽</item>
    /// </list>
    /// 屏蔽只是视觉上的：它只决定他人域能否入选各家门面的 <c>Viewed</c>（世界级表现的唯一闸门），
    /// 状态机推进、网络收发、服务器镜像与全部功能判定一律照旧。
    /// 鬼切 <c>OniDomain.RefreshViewed</c>、血湖 <c>KikasaDomain.RefreshViewed</c>、
    /// 两家大范围重启的 <c>LocallyViewed</c> 旁观分支都只从这里问"旁观他人是否放行"
    /// </summary>
    internal static class LegendDomainView
    {
        /// <summary>本机是否观看他人领域的旁观演出。设置项未就绪时按默认值屏蔽</summary>
        internal static bool SpectateOthers => !(CWRClientConfig.Instance?.HideOtherDomains ?? true);
    }
}
