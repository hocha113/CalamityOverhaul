using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGreentide : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Greentide>();
        public override int ProtogenesisID => ModContent.ItemType<Greentide>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override void SetDefaults(Item item) {
            item.damage = 95;
            item.DamageType = DamageClass.Melee;
            item.width = 62;
            item.height = 62;
            item.scale = 1.5f;
            item.useTime = 24;
            item.useAnimation = 24;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 7;
            item.value = CalamityGlobalItem.Rarity7BuyPrice;
            item.rare = ItemRarityID.Lime;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<GreenWater>();
            item.shootSpeed = 18f;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Greentide");
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? UseItem(Item item, Player player) {
            item.useAnimation = item.useTime = 20;
            item.scale = 1f;
            if (player.altFunctionUse == 2) {
                item.useAnimation = item.useTime = 24;
                item.scale = 1.5f;
            }
            return base.UseItem(item, player);
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(source, Main.MouseWorld + new Vector2(Main.rand.Next(-12, 12), Main.rand.Next(322, 382))
                    , new Vector2(Main.rand.NextFloat(-2, 2), Main.rand.Next(-19, -16)), type
                , damage / 2, knockback, Main.myPlayer, 0f, Main.rand.Next(3));
            }
            return false;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2)
                Greentide.OnHitSpanProj(item, player, hit.Knockback);
            return false;
        }

        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.altFunctionUse == 2)
                Greentide.OnHitSpanProj(item, player, hurtInfo.Knockback);
            return false;
        }
    }
}
