using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAstralBlade : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<AstralBladeHeld>();
        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += 10;
            return false;
        }
    }
}
