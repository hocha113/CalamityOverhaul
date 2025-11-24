using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RPhasesaber : CWRItemOverride
    {
        public override int TargetID => ItemID.WhitePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber2 : CWRItemOverride
    {
        public override int TargetID => ItemID.PurplePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber3 : CWRItemOverride
    {
        public override int TargetID => ItemID.OrangePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber4 : CWRItemOverride
    {
        public override int TargetID => ItemID.BluePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber5 : CWRItemOverride
    {
        public override int TargetID => ItemID.GreenPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber6 : CWRItemOverride
    {
        public override int TargetID => ItemID.RedPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber7 : CWRItemOverride
    {
        public override int TargetID => ItemID.YellowPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }
}
