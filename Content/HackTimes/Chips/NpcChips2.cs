using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //生体族纹样语汇：躯体轮廓 + 神经节，纹样都绕着一个"活物"长。
    //本页是 2026-08 扩展批的六枚 Npc 协议芯片；坐标全部压在 ±0.80，
    //孤立点写成单点子路径（M x y），圆角一律用 Q 表达（解析器不吃 A 指令）

    /// <summary>代偿协议芯片。躯体右腹被导管抽走一段，导管另一头指回一个人形小记号，债主在你这边</summary>
    internal class CompensationProtocolChip : BaseHackProtocolChip<CompensationProtocol>
    {
        protected override string DiePath =>
            "M -0.72 -0.52 L -0.24 -0.52 M -0.72 0.52 L -0.24 0.52 "
            + "M -0.72 -0.52 Q -0.80 0 -0.72 0.52 "
            + "M -0.24 -0.52 Q -0.18 -0.30 -0.20 -0.12 M -0.20 0.12 Q -0.18 0.30 -0.24 0.52 "
            + "M -0.20 0 L 0.24 0 M 0.24 0 L 0.14 -0.08 M 0.24 0 L 0.14 0.08 "
            + "M 0.52 -0.34 M 0.52 -0.24 L 0.52 0.10 M 0.38 -0.10 L 0.66 -0.10 "
            + "M 0.52 0.10 L 0.40 0.34 M 0.52 0.10 L 0.64 0.34";
    }

    /// <summary>载荷改写芯片。躯体的枪口位置分出三道箭簇，箭簇全部实心，归属已经翻转</summary>
    internal class PayloadRewriteChip : BaseHackProtocolChip<PayloadRewrite>
    {
        protected override string DiePath =>
            "M -0.76 -0.50 L -0.36 -0.50 M -0.76 0.50 L -0.36 0.50 "
            + "M -0.76 -0.50 Q -0.80 0 -0.76 0.50 M -0.36 -0.50 Q -0.30 0 -0.36 0.50 "
            + "M -0.33 0 L -0.10 0 "
            + "M -0.10 0 L 0.04 -0.26 M -0.10 0 L 0.06 0 M -0.10 0 L 0.04 0.26 "
            + "M 0.06 -0.38 L 0.34 -0.50 L 0.26 -0.22 Z "
            + "M 0.10 -0.08 L 0.38 0 L 0.10 0.08 Z "
            + "M 0.06 0.38 L 0.34 0.50 L 0.26 0.22 Z";
    }

    /// <summary>相位偏移芯片。两个同形躯体错开半格叠印，实线一具、虚线一具</summary>
    internal class PhaseDesyncChip : BaseHackProtocolChip<PhaseDesync>
    {
        protected override string DiePath =>
            "M -0.66 -0.64 L -0.18 -0.64 M -0.66 0.16 L -0.18 0.16 "
            + "M -0.66 -0.64 Q -0.74 -0.24 -0.66 0.16 M -0.18 -0.64 Q -0.10 -0.24 -0.18 0.16 "
            + "M -0.26 -0.20 L -0.14 -0.20 M -0.04 -0.20 L 0.08 -0.20 M 0.16 -0.20 L 0.22 -0.20 "
            + "M -0.26 0.60 L -0.14 0.60 M -0.04 0.60 L 0.08 0.60 M 0.16 0.60 L 0.22 0.60 "
            + "M -0.26 -0.20 L -0.31 -0.02 M -0.32 0.12 L -0.31 0.28 M -0.29 0.42 L -0.26 0.60 "
            + "M 0.22 -0.20 L 0.27 -0.02 M 0.28 0.12 L 0.27 0.28 M 0.25 0.42 L 0.22 0.60";
    }

    /// <summary>活体电源芯片。躯体关进电池外框，两极从头尾引出</summary>
    internal class LiveCellTapChip : BaseHackProtocolChip<LiveCellTap>
    {
        protected override string DiePath =>
            "M -0.56 -0.44 L 0.56 -0.44 L 0.56 0.44 L -0.56 0.44 Z "
            + "M 0.56 -0.12 L 0.70 -0.12 L 0.70 0.12 L 0.56 0.12 "
            + "M -0.24 -0.26 L 0.12 -0.26 M -0.24 0.26 L 0.12 0.26 "
            + "M -0.24 -0.26 Q -0.34 0 -0.24 0.26 M 0.12 -0.26 Q 0.22 0 0.12 0.26 "
            + "M -0.06 -0.26 L -0.06 -0.62 M -0.18 -0.62 L 0.06 -0.62 "
            + "M -0.06 0.26 L -0.06 0.62 M -0.14 0.62 L 0.02 0.62";
    }

    /// <summary>固件回滚芯片。躯体外一圈刻度环，指针弧从第三格逆时针退回第一格</summary>
    internal class FirmwareRollbackChip : BaseHackProtocolChip<FirmwareRollback>
    {
        protected override string DiePath =>
            "M -0.14 -0.28 L 0.14 -0.28 M -0.14 0.28 L 0.14 0.28 "
            + "M -0.14 -0.28 Q -0.24 0 -0.14 0.28 M 0.14 -0.28 Q 0.24 0 0.14 0.28 "
            + "M 0 -0.56 L 0 -0.72 M 0.40 -0.40 L 0.51 -0.51 M 0.56 0 L 0.72 0 "
            + "M 0.40 0.40 L 0.51 0.51 M 0 0.56 L 0 0.72 M -0.40 0.40 L -0.51 0.51 "
            + "M -0.56 0 L -0.72 0 M -0.40 -0.40 L -0.51 -0.51 "
            + "M 0.48 -0.14 Q 0.42 -0.48 0.10 -0.56 "
            + "M 0.10 -0.56 L 0.24 -0.62 M 0.10 -0.56 L 0.20 -0.44";
    }

    /// <summary>躯壳征用芯片。躯体倒下一半，一根提线从上方勾住肩点，傀儡线</summary>
    internal class ShellRequisitionChip : BaseHackProtocolChip<ShellRequisition>
    {
        protected override string DiePath =>
            "M -0.66 0.30 L -0.02 0.14 M -0.72 0.52 L 0.02 0.36 M -0.66 0.30 L -0.72 0.52 "
            + "M 0.02 0.14 Q 0.22 0.10 0.20 0.28 Q 0.18 0.44 0.02 0.36 "
            + "M -0.40 0.44 L -0.56 0.66 M -0.56 0.66 L -0.38 0.72 "
            + "M -0.06 -0.76 L 0.26 -0.76 M 0.10 -0.76 L 0.10 0.02 "
            + "M 0.10 0.02 Q 0.10 0.16 -0.02 0.16";
    }
}
