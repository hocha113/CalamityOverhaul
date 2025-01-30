using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RMinishark : ItemOverride
    {
        public override int TargetID => ItemID.Minishark;
        public override bool FormulaSubstitution => false;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<MinisharkHeldProj>(160);
    }
}
