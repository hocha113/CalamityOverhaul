using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBalefulHarvester : BaseRItem 
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.BalefulHarvester>();
        public override int ProtogenesisID => ModContent.ItemType<BalefulHarvester>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override void SetDefaults(Item item) {
            item.damage = 90;
            item.width = 74;
            item.height = 86;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 22;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 22;
            item.useTurn = true;
            item.knockBack = 8f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.Rarity10BuyPrice;
            item.rare = ItemRarityID.Red;
            item.shoot = ModContent.ProjectileType<BalefulHarvesterHeldProj>();
            item.shootSpeed = 15;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "BalefulHarvester");
        }

        public override bool? CanUseItem(Item item, Player player) 
            => player.ownedProjectileCounts[ModContent.ProjectileType<BalefulHarvesterHeldProj>()] == 0;

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            item.noMelee = false;
            item.noUseGraphic = false;
            if (player.altFunctionUse == 2) {
                item.noMelee = true;
                item.noUseGraphic = true;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }
            SoundEngine.PlaySound(SoundID.Item71, player.position);
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(source, position, velocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)), ModContent.ProjectileType<BalefulSickle>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void MeleeEffects(Item item, Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3))
                BalefulHarvester.SpanDust(hitbox.TopLeft() + new Vector2(Main.rand.Next(hitbox.Width), Main.rand.Next(hitbox.Height)), 6, 0.3f, 0.5f);
        }
    }
}
