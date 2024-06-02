using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class ApoctolithEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Rogue + "Apoctolith";
        public override void SetDefaults() {
            Item.SetCalamitySD<Apoctolith>();
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<ApoctolithHeld>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 20;
    }
}
