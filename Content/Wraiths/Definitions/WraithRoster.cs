using CalamityOverhaul.Content.Wraiths.Abilities;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Deaths;
using CalamityOverhaul.Content.Wraiths.Deaths.Performances;
using CalamityOverhaul.Content.Wraiths.Marks;

namespace CalamityOverhaul.Content.Wraiths.Definitions
{
    internal sealed class LanternBoy : WraithDefinition
    {
        public override int SortOrder => 20;
        internal override ushort NetworkId => 1;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override float RevivalCost => 0.02f;
        internal override float ErosionCost => 0.01f;
        internal override WraithMark Emits => WraithMark.Lit;
        internal override WraithSynergyRule[] BuildSynergyRules()
            => [LanternBoyAbility.GripSlash];
        internal override WraithPassiveAbility CreateAbility() => new LanternBoyAbility();
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new LanternSeizurePerformance();
    }

    internal sealed class CrimsonBride : WraithDefinition
    {
        public override int SortOrder => 30;
        internal override ushort NetworkId => 2;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override float RevivalCost => 0.125f;
        internal override float ErosionCost => 0.22f;
        internal override WraithMark Emits => WraithMark.Betrothed;
        //「喜堂」的冻结效果由缚印的 Timelock 状态属性兑现（WraithStateDef），
        //这条规则只声明边：跟谁同盘，谁的印都停在喜堂里
        internal override WraithSynergyRule[] BuildSynergyRules() => [
            new WraithSynergyRule {
                Id = "CrimsonBride.HallTimelock",
                Channel = WraithSynergyChannel.PlayerChannel,
                WildcardPartner = true,
                Name = () => WraithCovenText.BrideName,
                Note = () => WraithCovenText.BrideNote,
                UiPriority = 10,
            },
        ];
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new BrideSeizurePerformance();
    }

    internal sealed class ScapeGhost : WraithDefinition
    {
        public override int SortOrder => 40;
        internal override ushort NetworkId => 3;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override float RevivalCost => 0.125f;
        internal override float ErosionCost => 0.30f;
        //「顶劫」的泄压结算在 WraithPlayer.RelieveCovenRevival，这条规则只声明边
        internal override WraithSynergyRule[] BuildSynergyRules() => [
            new WraithSynergyRule {
                Id = "ScapeGhost.CovenRelief",
                Channel = WraithSynergyChannel.PlayerChannel,
                WildcardPartner = true,
                Name = () => WraithCovenText.ScapeName,
                Note = () => WraithCovenText.ScapeNote,
                UiPriority = 5,
            },
        ];
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new ScapeSeizurePerformance();
    }

    internal sealed class HeadlessShade : WraithDefinition
    {
        public override int SortOrder => 50;
        internal override ushort NetworkId => 4;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override float RevivalCost => 0.045f;
        internal override float ErosionCost => 0.025f;
        internal override WraithMark Emits => WraithMark.Severed;
        internal override WraithSynergyRule[] BuildSynergyRules()
            => [HeadlessShadeAbility.PinnedHunt];
        internal override WraithPassiveAbility CreateAbility() => new HeadlessShadeAbility();
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new ShadeSeizurePerformance();
    }

    internal sealed class GhostHand : WraithDefinition
    {
        public override int SortOrder => 60;
        internal override ushort NetworkId => 5;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override float RevivalCost => 0.05f;
        internal override float ErosionCost => 0.035f;
        internal override WraithMark Emits => WraithMark.Gripped;
        internal override WraithSynergyRule[] BuildSynergyRules() => [
            GhostHandAbility.RainReach,
            GhostHandAbility.RainCrush,
            GhostHandAbility.RainHandCap,
            GhostHandAbility.LitSeek,
        ];
        internal override WraithPassiveAbility CreateAbility() => new GhostHandAbility();
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new HandSeizurePerformance();
    }

    //网络编号 0 沿用原无面女残卷腾出的位；6 为井中鸣退役占位，不再复用
    internal sealed class GhostRain : WraithDefinition
    {
        public override int SortOrder => 70;
        internal override ushort NetworkId => 0;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override float RevivalCost => 0.125f;
        internal override float ErosionCost => 0.18f;
        internal override WraithMark Emits => WraithMark.Soaked;
        internal override WraithSynergyRule[] BuildSynergyRules()
            => [GhostRainAbility.WetBlade];
        internal override WraithPassiveAbility CreateAbility() => new GhostRainAbility();
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new RainSeizurePerformance();
    }
}
