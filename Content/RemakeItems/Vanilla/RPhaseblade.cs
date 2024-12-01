using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RPhaseblade : BaseRItem
    {
        public override int TargetID => ItemID.WhitePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade2 : BaseRItem
    {
        public override int TargetID => ItemID.PurplePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade3 : BaseRItem
    {
        public override int TargetID => ItemID.OrangePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade4 : BaseRItem
    {
        public override int TargetID => ItemID.BluePhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade5 : BaseRItem
    {
        public override int TargetID => ItemID.GreenPhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade6 : BaseRItem
    {
        public override int TargetID => ItemID.RedPhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhaseblade7 : BaseRItem
    {
        public override int TargetID => ItemID.YellowPhaseblade;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }
}
