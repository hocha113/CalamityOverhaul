using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    internal class NeutronWand : ModItem, ILoader
    {
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }
        public override string Texture => CWRConstant.Item_Magic + "NeutronWand";
        internal static int PType;
        void ILoader.SetupData() => PType = ModContent.ItemType<NeutronWand>();
        public override void SetStaticDefaults() => CWRUtils.SetAnimation(Type, 5, 10);
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 282;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 15;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(15, 3, 5, 0);
            Item.rare = ItemRarityID.Red;
            Item.shootSpeed = 15;
            Item.mana = 15;
            Item.crit = 6;
            Item.UseSound = SoundID.NPCDeath56;
            Item.SetHeldProj<NeutronWandHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems20;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<BlackMatterStick>(16)
                .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                    amount = 0;
                })
                .AddOnCraftCallback(CWRRecipes.SpawnAction)
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
