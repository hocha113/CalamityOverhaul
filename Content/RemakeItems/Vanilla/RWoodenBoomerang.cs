using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 木制回旋镖
    /// </summary>
    internal class RWoodenBoomerang : BaseRItem
    {
        public override int TargetID => ItemID.WoodenBoomerang;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_WoodenBoomerang_Text";
        public override void SetDefaults(Item item) {
            item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            item.shoot = ModContent.ProjectileType<WoodenBoomerangHeld>();
        }
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 0;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
