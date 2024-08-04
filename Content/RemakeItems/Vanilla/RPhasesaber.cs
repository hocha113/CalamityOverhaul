using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RPhasesaber : BaseRItem
    {
        public override int TargetID => ItemID.WhitePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PhasebladeHeld>();
    }

    internal class RPhasesaber2 : BaseRItem
    {
        public override int TargetID => ItemID.PurplePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PhasebladeHeld>();
    }

    internal class RPhasesaber3 : BaseRItem
    {
        public override int TargetID => ItemID.OrangePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PhasebladeHeld>();
    }

    internal class RPhasesaber4 : BaseRItem
    {
        public override int TargetID => ItemID.BluePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PhasebladeHeld>();
    }

    internal class RPhasesaber5 : BaseRItem
    {
        public override int TargetID => ItemID.GreenPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PhasebladeHeld>();
    }

    internal class RPhasesaber6 : BaseRItem
    {
        public override int TargetID => ItemID.RedPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PhasebladeHeld>();
    }

    internal class RPhasesaber7 : BaseRItem
    {
        public override int TargetID => ItemID.YellowPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PhasebladeHeld>();
    }
}
