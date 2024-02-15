using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RGaleforce : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Galeforce>();
        public override int ProtogenesisID => ModContent.ItemType<Galeforce>();
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.damage = 18;
            item.DamageType = DamageClass.Ranged;
            item.width = 32;
            item.height = 52;
            item.useTime = 20;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.Rarity3BuyPrice;
            item.rare = ItemRarityID.Orange;
            item.UseSound = SoundID.Item5;
            item.autoReuse = true;
            item.shoot = ProjectileID.WoodenArrowFriendly;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.Calamity().canFirePointBlankShots = true;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Galeforce");
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int heldType = ModContent.ProjectileType<GaleforceHeldProj>();
            if (player.ownedProjectileCounts[heldType] <= 0) {
                Projectile.NewProjectile(source, position, Vector2.Zero
                , heldType
                , damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }
    }
}
