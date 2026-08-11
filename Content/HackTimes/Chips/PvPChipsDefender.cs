using CalamityOverhaul.Content.HackTimes.PvP.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //PvP「防守方本机结算」簇纹样语汇：人形轮廓（头 + 梯形躯干，靠左立）代表防守方，
    //从人形引出的线路被拦腰切断、断口带一道斜切划痕；右半边是各协议的受害面主题。
    //孤立点写单点子路径（M x y），零长 M..L 会被按线段丢掉。
    //DiePath 与协议侧 GlyphDiePath 共用同一份常量：登记键同为协议类名、内容一致，
    //背包芯片图标与被骇 HUD 效果卡取到的是同一枚晶粒纹

    /// <summary>地图熄灭芯片。人形的寻路线被切断，右侧地图页打着死叉</summary>
    internal class MapBlackoutChip : BaseHackProtocolChip<MapBlackout>
    {
        protected override string DiePath => MapBlackout.Die;
    }

    /// <summary>信道乱码芯片。人形口部的声波弧外圈断裂，文本行碎成断划与噪点</summary>
    internal class ChannelScrambleChip : BaseHackProtocolChip<ChannelScramble>
    {
        protected override string DiePath => ChannelScramble.Die;
    }

    /// <summary>冷却注入芯片。人形出手臂线被切断，右侧表盘的分针垂着走不动</summary>
    internal class CooldownInjectChip : BaseHackProtocolChip<CooldownInject>
    {
        protected override string DiePath => CooldownInject.Die;
    }

    /// <summary>隐身剥离芯片。人形左侧的斗篷幕线被切断，右侧浮出带曝光射线的轮廓回声</summary>
    internal class StealthStripChip : BaseHackProtocolChip<StealthStrip>
    {
        protected override string DiePath => StealthStrip.Die;
    }

    /// <summary>义体离线芯片。脊柱总线上的义体引线被切断成游离节点，右下压一枚离线叉</summary>
    internal class CyberwareOfflineChip : BaseHackProtocolChip<CyberwareOffline>
    {
        protected override string DiePath => CyberwareOffline.Die;
    }
}
