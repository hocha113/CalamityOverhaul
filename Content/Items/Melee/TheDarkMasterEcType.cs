using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class TheDarkMasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheDarkMaster";
        public override void SetDefaults() {
            Item.SetCalamitySD<TheDarkMaster>();
            Item.shoot = ModContent.ProjectileType<TheDarkMasterRapier>();
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.shootSpeed = 5f;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
        }

        public override bool CanUseItem(Player player) {
            if (player.CWR().NoSemberCloneSpanTime > 0) {
                player.CWR().NoSemberCloneSpanTime--;
            }
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }
    }
}
