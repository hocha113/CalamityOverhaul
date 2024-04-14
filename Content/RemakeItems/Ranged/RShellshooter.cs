using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RShellshooter : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Shellshooter>();
        public override int ProtogenesisID => ModContent.ItemType<ShellshooterEcType>();
        public override string TargetToolTipItemName => "ShellshooterEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<ShellshooterHeldProj>();
    }
}
