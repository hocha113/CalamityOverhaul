using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Accessories;
using CalamityMod.Projectiles.Typeless;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System.Buffers;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class NeutronStarMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item + "MuzzleBrakeIV";
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 6));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(180, 22, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems23;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            CWRPlayer modplayer = player.CWR();
            modplayer.LoadMuzzleBrake = true;
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
                .AddIngredient<BlackMatterStick>(20)
                .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                    amount = 0;
                })
                .AddOnCraftCallback(CWRRecipes.SpawnAction)
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
