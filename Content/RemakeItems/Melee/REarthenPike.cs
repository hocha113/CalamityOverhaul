using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class REarthenPike : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.EarthenPike>();
        public override int ProtogenesisID => ModContent.ItemType<EarthenPike>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override void SetDefaults(Item item) {
            item.width = 60;
            item.damage = 90;
            item.DamageType = DamageClass.Melee;
            item.noMelee = true;
            item.useTurn = true;
            item.noUseGraphic = true;
            item.useAnimation = 25;
            item.useStyle = ItemUseStyleID.Shoot;
            item.useTime = 25;
            item.knockBack = 7f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 60;
            item.value = CalamityGlobalItem.Rarity5BuyPrice;
            item.rare = ItemRarityID.Pink;
            item.shoot = ModContent.ProjectileType<REarthenPikeSpear>();
            item.shootSpeed = 8f;
            CWRUtils.EasySetLocalTextNameOverride(item, "EarthenPike");
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "EarthenPike");
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile proj = Projectile.NewProjectileDirect(source, position, velocity, ModContent.ProjectileType<EarthenPikeThrowProj>(), damage * 2, knockback);
                EarthenPikeThrowProj earthenPikeThrowProj = (EarthenPikeThrowProj)proj.ModProjectile;
                if (earthenPikeThrowProj != null) {
                    earthenPikeThrowProj.earthenPike = item.Clone();
                    item.TurnToAir();
                }
                else {
                    proj.Kill();
                }
                return false;
            }
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }
    }
}
