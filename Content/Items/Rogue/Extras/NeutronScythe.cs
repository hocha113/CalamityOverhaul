using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class NeutronScythe : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/NeutronScythe";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 13));
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
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.shoot = ModContent.ProjectileType<NeutronScytheHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_NeutronScythe;
            Item.CWR().GetMeleePrefix = Item.CWR().GetRangedPrefix = true;
        }

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 22;
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 26;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NeutronStarIngot>(11)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
