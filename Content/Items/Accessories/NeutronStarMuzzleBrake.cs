using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Accessories;
using CalamityMod.Projectiles.Typeless;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class NeutronStarMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "MuzzleBrakeIV";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 6));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(180, 22, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_NeutronStarMuzzleBrake;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            CWRPlayer modplayer = player.CWR();
            modplayer.LoadMuzzleBrakeLevel = 4;
            modplayer.PressureIncrease = 0;
            CalamityPlayer calPlayer = player.Calamity();
            calPlayer.rangedAmmoCost *= 0.8f;
            calPlayer.deadshotBrooch = true;
            calPlayer.dynamoStemCells = true;
            calPlayer.MiniSwarmers = true;
            calPlayer.eleResist = true;
            player.moveSpeed += 0.25f;
            player.magicQuiver = true;
            player.GetDamage<RangedDamageClass>() += 1f;
            player.GetCritChance<RangedDamageClass>() += 100f;
            player.aggro -= 1200;

            calPlayer.voidField = true;
            if (player.whoAmI == Main.myPlayer) {
                var source = player.GetSource_Accessory(Item);
                if (player.ownedProjectileCounts[ModContent.ProjectileType<VoidFieldGenerator>()] < 4) {
                    for (int v = 0; v < 4; v++) {
                        Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero
                            , ModContent.ProjectileType<VoidFieldGenerator>(), 0, 0f, Main.myPlayer, v);
                    }
                }
            }
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<ElementMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<PrecisionMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<SimpleMuzzleBrake>();
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient<ElementMuzzleBrake>()
                .AddIngredient<ElementalQuiver>()
                .AddIngredient<DaawnlightSpiritOrigin>()
                .AddIngredient<QuiverofNihility>()
                .AddIngredient<NeutronStarIngot>(12)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
