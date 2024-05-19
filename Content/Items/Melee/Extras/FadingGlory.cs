using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class FadingGlory : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "FadingGlory";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 6));
        }
        public override void SetDefaults() {
            Item.width = Item.height = 35;
            Item.damage = 882;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<FadingGloryRapier>();
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

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }
}
