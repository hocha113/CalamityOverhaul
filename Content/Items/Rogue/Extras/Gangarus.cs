using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.GangarusProjectiles;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class Gangarus : ModItem, ILoader
    {
        public static SoundStyle BelCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 3.5f };
        public static SoundStyle AT = new("CalamityOverhaul/Assets/Sounds/AT") { Volume = 1.5f };
        public static Asset<Texture2D> GangarusAsset;
        public static Asset<Texture2D> EvaAsset;
        public int ChargeGrade;
        public override string Texture => CWRConstant.Item + "Rogue/Gangarus";
        public override bool IsLoadingEnabled(Mod mod) => !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        void ILoader.LoadAsset() {
            GangarusAsset = CWRUtils.GetT2DAsset(CWRConstant.Item + "Rogue/Gangarus");
            EvaAsset = CWRUtils.GetT2DAsset(CWRConstant.Item + "Rogue/Gangarus3");
        }
        void ILoader.UnLoad() {
            GangarusAsset = null;
            EvaAsset = null;
        }
        public static void ZenithWorldAsset() {
            if (!CWRServerConfig.Instance.AddExtrasContent || Main.dedServ) {
                return;
            }
            TextureAssets.Item[CWRLoad.Gangarus] = Main.zenithWorld ? EvaAsset : GangarusAsset;
        }
        public override void SetDefaults() {
            Item.width = 44;
            Item.damage = 2480;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 35;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 9f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 44;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ModContent.RarityType<Violet>();
            Item.shoot = ModContent.ProjectileType<GangarusProjectile>();
            Item.shootSpeed = 15f;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems16;
            Item.CWR().isHeldItem = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) => CWRUtils.SetItemLegendContentTops(ref tooltips, Name);
        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage *= (ChargeGrade + 1);

        public override void HoldItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GangarusHeldProjectile>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<GangarusProjectile>()] == 0
                && Main.myPlayer == player.whoAmI) {
                Projectile.NewProjectile(player.parent(), player.Center, Vector2.Zero, ModContent.ProjectileType<GangarusHeldProjectile>(), 0, 0, player.whoAmI);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (ChargeGrade > 0) {
                int proj = Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<GangarusProjectile>(), damage, knockback, player.whoAmI);
                Main.projectile[proj].ai[0] = 1;
                Main.projectile[proj].ai[1] = ChargeGrade;
                ChargeGrade = 0;
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ShadowspecBar>(17)
                .AddIngredient<Rock>(1)
                .AddIngredient<ScarletDevil>(1)
                .AddIngredient<CosmicCalamity>(1)
                .AddIngredient(ItemID.Gungnir, 1)
                .AddIngredient<BlackMatterStick>(3)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
