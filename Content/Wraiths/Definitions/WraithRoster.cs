using CalamityOverhaul.Content.Wraiths.Abilities;
using CalamityOverhaul.Content.Wraiths.Core;

namespace CalamityOverhaul.Content.Wraiths.Definitions
{
    internal sealed class NoFace : WraithDefinition
    {
        public override int SortOrder => 10;
        internal override ushort NetworkId => 0;
    }

    internal sealed class LanternBoy : WraithDefinition
    {
        public override int SortOrder => 20;
        internal override ushort NetworkId => 1;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.LanternBoy;
        internal override float MasteryCost => 0.05f;
        internal override float ErosionCost => 0.01f;
        internal override WraithPassiveAbility CreateAbility() => new LanternBoyAbility();
    }

    internal sealed class CrimsonBride : WraithDefinition
    {
        public override int SortOrder => 30;
        internal override ushort NetworkId => 2;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.CrimsonBride;
        internal override float MasteryCost => 0.40f;
        internal override float ErosionCost => 0.22f;
    }

    internal sealed class ScapeGhost : WraithDefinition
    {
        public override int SortOrder => 40;
        internal override ushort NetworkId => 3;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.ScapeGhost;
        internal override float MasteryCost => 0.45f;
        internal override float ErosionCost => 0.30f;
    }

    internal sealed class HeadlessShade : WraithDefinition
    {
        public override int SortOrder => 50;
        internal override ushort NetworkId => 4;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.HeadlessShade;
        internal override float MasteryCost => 0.12f;
        internal override float ErosionCost => 0.025f;
        internal override WraithPassiveAbility CreateAbility() => new HeadlessShadeAbility();
    }

    internal sealed class GhostHand : WraithDefinition
    {
        public override int SortOrder => 60;
        internal override ushort NetworkId => 5;
        internal override WraithCatalogState CatalogState => WraithCatalogState.Usable;
        internal override WraithAbilityKind AbilityKind => WraithAbilityKind.GhostHand;
        internal override float MasteryCost => 0.14f;
        internal override float ErosionCost => 0.035f;
        internal override WraithPassiveAbility CreateAbility() => new GhostHandAbility();
    }

    internal sealed class WellThing : WraithDefinition
    {
        public override int SortOrder => 70;
        internal override ushort NetworkId => 6;
        internal override WraithCatalogState CatalogState => WraithCatalogState.SealedArchive;
    }
}
