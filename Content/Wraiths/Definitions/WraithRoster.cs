using CalamityOverhaul.Content.Wraiths.Core;
using System;

namespace CalamityOverhaul.Content.Wraiths.Definitions
{
    //正典名录占位，纯数据鬼（ActorType=null）；键沿用演示档保证存档连续

    /// <summary>无面女</summary>
    internal sealed class NoFace : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 10;
    }

    /// <summary>提灯童子</summary>
    internal sealed class LanternBoy : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 20;
    }

    /// <summary>绯嫁</summary>
    internal sealed class CrimsonBride : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 30;
    }

    /// <summary>替死簿</summary>
    internal sealed class StandIn : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 40;
    }

    /// <summary>无头人影</summary>
    internal sealed class HeadlessShade : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 50;
    }

    /// <summary>井中鸣，生来封印</summary>
    internal sealed class WellThing : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 70;
        public override WraithBindState InitialBindState => WraithBindState.Sealed;
    }
}
