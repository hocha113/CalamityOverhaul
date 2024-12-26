using CalamityMod;
using CalamityMod.Items.Tools;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.CWRDamageTypes;
using CalamityOverhaul.Content.Items.Materials;

using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class InfinitePick : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/" + (IsPick ? "Pickaxe" : "Hammer");
        private Texture2D value => CWRUtils.GetT2DValue(Texture);
        private bool IsPick = true;
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
        }
        public override void SetDefaults() {
            Item.damage = 9999;
            Item.DamageType = EndlessDamageClass.Instance;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 1;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(gold: 999);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = 0;
            Item.shootSpeed = 32;
            Item.pick = 9999;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems3;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit = 9999;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage = damage.Scale(0);

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI) {
                return;
            }

            if (IsPick) {
                Item.pick = 9999;
                Item.hammer = 0;
                Item.useAnimation = Item.useTime = 10;

            }
            else {
                Item.pick = 0;
                Item.hammer = 9999;
                Item.useAnimation = Item.useTime = 30;
            }
            if (CWRKeySystem.InfinitePickSkillKey.JustPressed) {
                IsPick = !IsPick;
                SoundEngine.PlaySound(!IsPick ? CWRSound.Pecharge : CWRSound.Peuncharge, player.Center);
                TextureAssets.Item[Type] = CWRUtils.GetT2DAsset(Texture);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<InfinitePickProj>()
                    , Item.damage * 10, 0, player.whoAmI, IsPick ? 1 : 0, Main.MouseWorld.X, Main.MouseWorld.Y);
            }
            
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (IsPick) {
                TooltipLine cumstops = tooltips.FirstOrDefault((TooltipLine x) => x.Name == "PickPower" && x.Mod == "Terraria");
                if (cumstops != null) {
                    string typeV = Language.GetTextValue("LegacyTooltip.26");
                    cumstops.Text = $"{int.MaxValue}{typeV}";
                }
            }
            else {
                TooltipLine cumstops = tooltips.FirstOrDefault((TooltipLine x) => x.Name == "HammerPower" && x.Mod == "Terraria");
                if (cumstops != null) {
                    string typeV = Language.GetTextValue("LegacyTooltip.28");
                    cumstops.Text = $"{int.MaxValue}{typeV}";
                }
            }

            tooltips.IntegrateHotkey(CWRKeySystem.InfinitePickSkillKey);
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if ((line.Name == "ItemName" || line.Name == "Damage" || line.Name == "PickPower" || line.Name == "HammerPower")
                && line.Mod == "Terraria") {
                InfiniteIngot.DrawColorText(Main.spriteBatch, line);
                return false;
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CrystylCrusher>()
                .AddIngredient<DarkMatterBall>(12)
                .AddIngredient<InfiniteIngot>(18)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
