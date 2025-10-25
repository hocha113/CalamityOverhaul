using CalamityMod.Rarities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    ///<summary>
    ///万魔殿
    ///</summary>
    internal class Pandemonium : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Pandemonium";

        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 25;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.knockBack = 5;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = ModContent.RarityType<Violet>();
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
            Item.shootSpeed = 10f;
            Item.channel = true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumChannel>()] == 0;
        }
    }
}
