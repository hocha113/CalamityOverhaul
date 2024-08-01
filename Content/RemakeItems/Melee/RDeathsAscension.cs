using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDeathsAscension : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<DeathsAscension>();
        public override int ProtogenesisID => ModContent.ItemType<DeathsAscensionEcType>();
        public override string TargetToolTipItemName => "DeathsAscensionEcType";

        private int swingIndex = 0;
        public override void SetDefaults(Item item) {
            item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            swingIndex = 0;
            item.UseSound = null;
            item.noUseGraphic = true;
            item.autoReuse = true;
            item.noMelee = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.shoot = ModContent.ProjectileType<DeathsAscensionHeld>();
        }

        public override bool? On_AltFunctionUse(Item item, Player player) => false;

        public override bool? On_UseItem(Item item, Player player) => true;

        public override bool? On_CanUseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DeathsAscensionThrowable>()] <= 0;
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (++swingIndex > 6) {
                type = ModContent.ProjectileType<DeathsAscensionThrowable>();
                swingIndex = 0;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, swingIndex % 2 == 0 ? 0 : 1, swingIndex + 1);
            return false;
        }
    }
}
