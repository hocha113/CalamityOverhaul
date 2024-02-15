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
    internal class RArbalest : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Arbalest>();
        public override int ProtogenesisID => ModContent.ItemType<Arbalest>();
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Arbalest>()] = true;
        }

        public override void SetDefaults(Item item) {
            item.damage = 28;
            item.DamageType = DamageClass.Ranged;
            item.width = 82;
            item.height = 34;
            item.useTime = 7;
            item.useAnimation = 7;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.knockBack = 4f;
            item.value = CalamityGlobalItem.Rarity5BuyPrice;
            item.rare = ItemRarityID.Pink;
            item.UseSound = null;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<ArbalestHeldProj>();
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Arrow;
            item.Calamity().canFirePointBlankShots = true;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Arbalest");
        }

        public override void HoldItem(Item item, Player player) {
            item.initialize();
            Projectile heldProj = CWRUtils.GetProjectileInstance((int)item.CWR().ai[0]);
            if (heldProj != null && heldProj.type == ModContent.ProjectileType<ArbalestHeldProj>()) {
                heldProj.localAI[1] = item.CWR().ai[1];
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            item.initialize();
            item.CWR().ai[1] = type;
            if (player.ownedProjectileCounts[ModContent.ProjectileType<ArbalestHeldProj>()] <= 0) {
                item.CWR().ai[0] = Projectile.NewProjectile(source, position, Vector2.Zero
                , ModContent.ProjectileType<ArbalestHeldProj>()
                , item.damage, knockback, player.whoAmI);
                if (player.altFunctionUse == 2) {
                    Main.projectile[(int)item.CWR().ai[0]].ai[0] = 1;
                }
            }
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }
    }
}
