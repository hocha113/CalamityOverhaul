using CalamityOverhaul.Content.Wraiths.Abilities;
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

    /// <summary>替死鬼；纯被动，无主动技能</summary>
    internal sealed class ScapeGhost : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 40;
        public override bool IsDebugContent => true;
    }

    /// <summary>无头人影</summary>
    internal sealed class HeadlessShade : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 50;
    }

    /// <summary>焦黑枯手。Key 不可改；借力「攥」走 GhostHandAbility</summary>
    internal sealed class GhostHand : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 60;

        public override int HitboxWidth => 52;
        public override int HitboxHeight => 40;

        public override int MaterializeFrames => 255;
        public override int DematerializeFrames => 45;
        public override int PresentDurationLimit => 60 * 240;
        public override int HaltWindowTicks => 600;

        public override Color BaseColor => new(30, 26, 24);
        public override Color EyeColor => new(214, 92, 32);

        public override WraithAbility CreateAbility() => new GhostHandAbility();
    }

    /// <summary>井中鸣，生来封印</summary>
    internal sealed class WellThing : WraithDefinition
    {
        public override Type ActorType => null;
        public override int SortOrder => 70;
        public override WraithBindState InitialBindState => WraithBindState.Sealed;
    }
}
