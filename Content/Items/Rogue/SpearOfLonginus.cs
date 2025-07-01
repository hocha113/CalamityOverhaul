﻿using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.Longinus;
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

namespace CalamityOverhaul.Content.Items.Rogue
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
        public int ChargeGrade;
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
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ModContent.RarityType<Violet>();
            Item.shoot = ModContent.ProjectileType<LonginusThrow>();
            Item.shootSpeed = 15f;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_SpearOfLonginus;
            Item.CWR().isHeldItem = true;
            ItemOverride.ItemMeleePrefixDic[Type] = true;
            ItemOverride.ItemRangedPrefixDic[Type] = false;
        }

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;

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
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }
}
