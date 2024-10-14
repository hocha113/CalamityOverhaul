using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class LucreciaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Lucrecia";
        public override void SetDefaults() {
            Item.SetItemCopySD<Lucrecia>();
            Item.shoot = ModContent.ProjectileType<LucreciaRapier>();
            Item.useTime = 60;
            Item.useAnimation = 60;
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
