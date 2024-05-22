using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class MirrorBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "MirrorBlade";
        public override void SetDefaults() {
            Item.SetCalamitySD<MirrorBlade>();
            Item.shoot = ModContent.ProjectileType<MirrorBladeRapier>();
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.shootSpeed = 5f;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }
    }
}
