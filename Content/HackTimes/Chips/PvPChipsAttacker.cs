using CalamityOverhaul.Content.HackTimes.PvP.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //PvP 攻击方结算簇的五枚芯片（2026-08 芯片档）。
    //纹样语汇：人形轮廓 + 抽取/引信记号——每张 die 都围着"一个被动手脚的玩家"长。
    //坐标压 ±0.80、圆角用 Q（解析器不吃 A）。die 字符串的唯一正本放在协议类的
    //Die 常量上（PlayerHackDef 的 HUD 效果卡与芯片图标共用一份，登记器后写覆盖，
    //两处引用同一常量就不存在覆盖分歧），芯片侧只引用不复制

    /// <summary>内存烧蚀芯片。躯体旁一列内存格，顶格断裂上蹿火舌——先烧掉对面的弹药库</summary>
    internal class MemoryScorchChip : BaseHackProtocolChip<MemoryScorch>
    {
        protected override string DiePath => MemoryScorch.Die;
    }

    /// <summary>增益抽取芯片。躯体里的增益箭头被导管吸出，落在管口重新立起——增益换了主人</summary>
    internal class BuffSiphonChip : BaseHackProtocolChip<BuffSiphon>
    {
        protected override string DiePath => BuffSiphon.Die;
    }

    /// <summary>战术榨取芯片。躯体着一圈瞄准刻线，命中处的数据流注进电量格——人形电池</summary>
    internal class CombatSiphonChip : BaseHackProtocolChip<CombatSiphon>
    {
        protected override string DiePath => CombatSiphon.Die;
    }

    /// <summary>弹道倒戈芯片。躯体射出的弹道半途画一个大回环，箭头调头指回躯体——枪口认主</summary>
    internal class BallisticTurncoatChip : BaseHackProtocolChip<BallisticTurncoat>
    {
        protected override string DiePath => BallisticTurncoat.Die;
    }

    /// <summary>熔断标记芯片。躯体胸口盘着引信卷，火星顺线上蹿，表盘刻线在数拍子——四秒后见</summary>
    internal class MeltdownBrandChip : BaseHackProtocolChip<MeltdownBrand>
    {
        protected override string DiePath => MeltdownBrand.Die;
    }
}
