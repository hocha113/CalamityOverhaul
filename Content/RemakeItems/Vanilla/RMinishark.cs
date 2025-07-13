using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RMinishark : CWRItemOverride
    {
        public override int TargetID => ItemID.Minishark;
        
        public override void SetDefaults(Item item) => item.SetCartridgeGun<MinisharkHeldProj>(160);
    }
}
