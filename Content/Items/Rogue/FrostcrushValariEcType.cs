using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class FrostcrushValariEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Rogue + "FrostcrushValari";
        public override void SetDefaults() {
            Item.SetItemCopySD<FrostcrushValari>();
            Item.shoot = ModContent.ProjectileType<FrostcrushValariHeld>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 16;
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 6;
    }
}
