using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class NeutronScythe : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override string Texture => CWRConstant.Item + "Rogue/NeutronScythe";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 18));
        }
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 322;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.shootSpeed = 6;
            Item.value = Item.buyPrice(12, 73, 75, 0);
            Item.rare = ItemRarityID.LightPurple;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.shoot = ModContent.ProjectileType<NeutronScytheHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems21;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 22;
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 26;

        public override void AddRecipes() {
            CreateRecipe()
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
