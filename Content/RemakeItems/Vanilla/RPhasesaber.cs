using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RPhasesaber : ItemOverride
    {
        public override int TargetID => ItemID.WhitePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber2 : ItemOverride
    {
        public override int TargetID => ItemID.PurplePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber3 : ItemOverride
    {
        public override int TargetID => ItemID.OrangePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber4 : ItemOverride
    {
        public override int TargetID => ItemID.BluePhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber5 : ItemOverride
    {
        public override int TargetID => ItemID.GreenPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber6 : ItemOverride
    {
        public override int TargetID => ItemID.RedPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }

    internal class RPhasesaber7 : ItemOverride
    {
        public override int TargetID => ItemID.YellowPhasesaber;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => PhasebladeHeld.Set(item);
    }
}
