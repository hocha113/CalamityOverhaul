using CalamityMod.Items;
using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.GangarusProjectiles;
using Mono.Cecil;
using Terraria.Audio;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;
using Terraria.Localization;
using CalamityMod.Rarities;
using System;
using Terraria.GameContent;
using CalamityOverhaul.Content.UIs.SupertableUIs;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class Gangarus : ModItem
    {
        public static SoundStyle BelCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 3.5f };
        public static SoundStyle AT = new("CalamityOverhaul/Assets/Sounds/AT") { Volume = 1.5f };
        public int ChargeGrade;
        public override string Texture => CWRConstant.Item + "Rogue/Gangarus";
        public LocalizedText Legend { get; private set; }

        public static void ZenithWorldAsset() {
            if (Main.zenithWorld) {
                TextureAssets.Item[CWRIDs.Gangarus] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Rogue/Gangarus3");
            }
            else {
                TextureAssets.Item[CWRIDs.Gangarus] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Rogue/Gangarus");
            }
        }

        public override void SetStaticDefaults() => Legend = this.GetLocalization(nameof(Legend));
        public override void SetDefaults() {
            Item.width = 44;
            Item.damage = 4480;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 35;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 9f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 44;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ModContent.RarityType<Violet>();
            Item.shoot = ModContent.ProjectileType<GangarusProjectile>();
            Item.shootSpeed = 15f;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems16;
            Item.CWR().isHeldItem = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            ItemSystem.SetItemLegendContentTops(ref tooltips, Name);
        }

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
    }
}
