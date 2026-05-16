using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    internal class SpearOfLonginus : ModItem, ICWRLoader
    {
        public static SoundStyle BelCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 3.5f };
        public static SoundStyle AT = new("CalamityOverhaul/Assets/Sounds/AT") { Volume = 1.5f };
        [VaultLoaden(CWRConstant.Item + "Rogue/Longinus")]
        public static Asset<Texture2D> LonginusAsset = null;
        [VaultLoaden(CWRConstant.Item + "Rogue/Longinus_Eva")]
        public static Asset<Texture2D> EvaAsset = null;
        public static int ID;
        /// <summary>
        /// 当前累积的圣神能量计数（满后转化为一次充能立场）
        /// </summary>
        public int ChargeGrade;
        /// <summary>
        /// 当前圣神能量条进度，达到 <see cref="HolyEnergyMax"/> 后清零并 <see cref="ChargeGrade"/>+1
        /// </summary>
        public int HolyEnergy;
        /// <summary>
        /// 圣神能量条的上限
        /// </summary>
        public const int HolyEnergyMax = 240;
        /// <summary>
        /// 最大可叠加的立场层数
        /// </summary>
        public const int MaxChargeGrade = 6;
        public override string Texture => CWRConstant.Item + "Rogue/Longinus";
        public static void ZenithWorldAsset() {
            if (Main.dedServ) {
                return;
            }
            TextureAssets.Item[ID] = Main.zenithWorld ? EvaAsset : LonginusAsset;
        }
        public override void SetStaticDefaults() => ID = Type;
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
            Item.value = Item.buyPrice(6, 15, 5, 5);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<LonginusThrow>();
            Item.shootSpeed = 15f;
            Item.DamageType = DamageClass.Melee;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_SpearOfLonginus;
            Item.CWR().isHeldItem = true;
            ItemOverride.ItemMeleePrefixDic[Type] = true;
            ItemOverride.ItemRangedPrefixDic[Type] = false;
        }

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = ItemGroup.MeleeWeapon;

        public override void ModifyTooltips(List<TooltipLine> tooltips) => CWRUtils.SetItemLegendContentTops(ref tooltips, Name);
        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage *= ChargeGrade + 1;

        public override void HoldItem(Player player) {
            if (player.CountProjectilesOfID<LonginusHeld>() == 0 && player.CountProjectilesOfID<LonginusThrow>() == 0
                && Main.myPlayer == player.whoAmI) {
                Projectile.NewProjectile(player.GetSource_FromThis(), player.Center, Vector2.Zero
                    , ModContent.ProjectileType<LonginusHeld>(), 0, 0, player.whoAmI);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (ChargeGrade > 0) {
                int proj = Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<LonginusThrow>(), damage, knockback, player.whoAmI);
                Main.projectile[proj].ai[0] = 1;
                Main.projectile[proj].ai[1] = ChargeGrade;
                ChargeGrade = 0;
                HolyEnergy = 0;
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }
}
