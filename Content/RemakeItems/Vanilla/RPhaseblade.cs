using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RPhaseblade : CWRItemOverride
    {
        public override int TargetID => ItemID.WhitePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade2 : CWRItemOverride
    {
        public override int TargetID => ItemID.PurplePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade3 : CWRItemOverride
    {
        public override int TargetID => ItemID.OrangePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade4 : CWRItemOverride
    {
        public override int TargetID => ItemID.BluePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade5 : CWRItemOverride
    {
        public override int TargetID => ItemID.GreenPhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade6 : CWRItemOverride
    {
        public override int TargetID => ItemID.RedPhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade7 : CWRItemOverride
    {
        public override int TargetID => ItemID.YellowPhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }
}
