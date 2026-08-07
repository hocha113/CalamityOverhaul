using CalamityOverhaul.Content.Wraiths.Abilities;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Deaths;
using CalamityOverhaul.Content.Wraiths.Deaths.Performances;

namespace CalamityOverhaul.Content.Wraiths.Definitions
{
    internal sealed class LanternBoy : WraithDefinition
    {
        public override int SortOrder => 20;
        internal override ushort NetworkId => 1;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.LanternBoy;
        internal override float RevivalCost => 0.04f;
        internal override float ErosionCost => 0.01f;
        internal override WraithPassiveAbility CreateAbility() => new LanternBoyAbility();
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new LanternSeizurePerformance();
    }

    internal sealed class CrimsonBride : WraithDefinition
    {
        public override int SortOrder => 30;
        internal override ushort NetworkId => 2;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.CrimsonBride;
        internal override float RevivalCost => 0.25f;
        internal override float ErosionCost => 0.22f;
    }

    internal sealed class ScapeGhost : WraithDefinition
    {
        public override int SortOrder => 40;
        internal override ushort NetworkId => 3;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.ScapeGhost;
        internal override float RevivalCost => 0.25f;
        internal override float ErosionCost => 0.30f;
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new ScapeSeizurePerformance();
    }

    internal sealed class HeadlessShade : WraithDefinition
    {
        public override int SortOrder => 50;
        internal override ushort NetworkId => 4;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.HeadlessShade;
        internal override float RevivalCost => 0.09f;
        internal override float ErosionCost => 0.025f;
        internal override WraithPassiveAbility CreateAbility() => new HeadlessShadeAbility();
        internal override WraithDeathPerformance CreateDeathPerformance()
            => new ShadeSeizurePerformance();
    }

    internal sealed class GhostHand : WraithDefinition
    {
        public override int SortOrder => 60;
        internal override ushort NetworkId => 5;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.GhostHand;
        internal override float RevivalCost => 0.10f;
        internal override float ErosionCost => 0.035f;
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
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.GhostRain;
        internal override float RevivalCost => 0.25f;
        internal override float ErosionCost => 0.18f;
        internal override WraithPassiveAbility CreateAbility() => new GhostRainAbility();
    }
}
