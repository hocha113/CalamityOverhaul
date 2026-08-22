using System.Collections.Generic;
using System.Text;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    //TP机器留位登记表;登记模式镜像GaolBossRoomSiting.LastOrigin
    //ShouldSave=false回放制下每次生成重算,PlanAndBuild/看样入口开头Reset。
    //
    //2026-08-15:Piston与GearCrush两类不再只是剪影，运行时由
    //Machines\DungeonworldMachines读本表开动并致伤(见该文件)。
    //GearLarge/GearSmall(齿轮井背景)、BellGate、ElevatorStation仍是纯留位:
    //齿轮井是攀爬路线,在那里致伤不公平;后两者本就不是危险物
    internal enum L6SlotKind
    {
        /// <summary>大齿轮(齿轮井演出位,帧包络8x8,轴承座2x2 Cog已置于Frame中心)</summary>
        GearLarge,
        /// <summary>小齿轮(铸造大厅背景位,帧包络6x6,轴承座2x2 Cog)</summary>
        GearSmall,
        /// <summary>活塞推杆(机关走廊母题5,帧包络3x3,缸体Cog2x1在槽顶,头朝下捶向走廊)</summary>
        Piston,
        /// <summary>齿轮碾压(机关走廊母题6,帧包络=段宽x行走带,轮齿Cog朝下扫过行走面)</summary>
        GearCrush,
        /// <summary>钟声门(主控室→L7静默通路的门禁TP,Frame=落口两侧门柱+过梁包络)</summary>
        BellGate,
        /// <summary>电梯站(井站段prefab归公共构件波,此条仅登记建议锚点,零几何)</summary>
        ElevatorStation,
    }

    internal readonly struct L6MachineSlot(L6SlotKind kind, Rectangle frame, string note)
    {
        /// <summary>帧精确包络(tile坐标),资产波直写帧+AddInWorld时对位此矩形</summary>
        internal readonly Rectangle Frame = frame;
        internal readonly L6SlotKind Kind = kind;
        internal readonly string Note = note;
    }

    /// <summary>本次生成的L6机器槽位表,资产波对接与QA报告消费</summary>
    internal static class L6MachineSlots
    {
        internal static readonly List<L6MachineSlot> Slots = [];

        internal static void Reset() => Slots.Clear();

        internal static void Register(L6SlotKind kind, Rectangle frame, string note)
            => Slots.Add(new L6MachineSlot(kind, frame, note));

        /// <summary>逐条落日志(生成报告的一部分,§3.1-4计数报告纪律)</summary>
        internal static void LogAll() {
            var sb = new StringBuilder();
            sb.Append($"[L6MachineSlots] 留位登记 {Slots.Count} 条:");
            foreach (L6MachineSlot slot in Slots) {
                sb.Append($" {slot.Kind}@({slot.Frame.X},{slot.Frame.Y},{slot.Frame.Width}x{slot.Frame.Height})[{slot.Note}]");
            }
            CWRMod.Instance.Logger.Info(sb.ToString());
        }
    }
}
