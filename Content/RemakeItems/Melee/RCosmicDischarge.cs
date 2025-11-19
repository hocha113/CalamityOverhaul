using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCosmicDischarge : CWRItemOverride
    {
        public override bool DrawingInfo => false;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            float ai3 = (Main.rand.NextFloat() - 0.75f) * 0.7853982f;
            int sengs = 1;
            if (player.AdrenalineMode()) {
                sengs = 12;
            }
            Projectile.NewProjectile(source, position.X, position.Y, velocity.X * sengs, velocity.Y * sengs
                , type, damage, knockback, player.whoAmI, 0f, ai3);
            return false;
        }
    }
}
