using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 蘑菇回旋镖
    /// </summary>
    internal class RShroomerang : ItemOverride
    {
        public override int TargetID => ItemID.Shroomerang;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.damage = 18;
            item.DamageType = CWRLoad.RogueDamageClass;
            item.shoot = ModContent.ProjectileType<ShroomerangHeld>();
        }
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 4;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
