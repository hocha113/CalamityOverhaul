using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class BarrenBow : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "BarrenBow";
        public override void SetDefaults() {
            Item.damage = 28;
            Item.width = 32;
            Item.height = 58;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shootSpeed = 12f;
            Item.UseSound = SoundID.Item5;
            Item.useAmmo = AmmoID.Arrow;
            Item.value = Terraria.Item.buyPrice(0, 2, 15, 0);
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.CWR().hasHeldNoCanUseBool = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<BarrenBowHeldProj>();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.LightShard, 2)
                .AddIngredient(ItemID.AntlionMandible, 5)
                .AddIngredient(ItemID.HellwingBow)
                .AddIngredient<LunarianBow>()
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
