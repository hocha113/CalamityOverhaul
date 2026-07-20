using CalamityOverhaul.Content.Wraiths.Core;
using System;

namespace CalamityOverhaul.Content.Wraiths.Definitions
{
    //首批六只厉鬼的正典名录（焦黑枯手已毕业至 GhostHands/）：自点鬼簿演示名录迁入，
    //键即当年的演示键，沿用保证存档连续。在册者均为纯数据鬼（ActorType=null，
    //不显形、不参与调度），文案在 Wraiths.hjson，谁先获得实体/能力，谁就从这里毕业成独立文件夹

    /// <summary>无面女：「借颜」</summary>
    internal sealed class NoFace : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 10;
    }

    /// <summary>提灯童子：「引路」</summary>
    internal sealed class LanternBoy : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 20;
    }

    /// <summary>绯嫁：「迎亲」</summary>
    internal sealed class CrimsonBride : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 30;
    }

    /// <summary>替死簿：「替死」</summary>
    internal sealed class StandIn : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 40;
    }

    /// <summary>无头人影：「拼肢」</summary>
    internal sealed class HeadlessShade : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 50;
    }

    /// <summary>井中鸣：生来封印，名讳可见而来历赋力不可示人</summary>
    internal sealed class WellThing : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 70;
        public override WraithBindState InitialBindState => WraithBindState.Sealed;
    }
}
