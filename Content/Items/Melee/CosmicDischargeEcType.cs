using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class CosmicDischargeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CosmicDischarge";
        public override void SetDefaults() => Item.SetCalamitySD<CosmicDischarge>();
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            float ai3 = (Main.rand.NextFloat() - 0.75f) * 0.7853982f;
            int sengs = 1;
            if (player.Calamity().adrenalineModeActive) {
                sengs = 12;
            }
            Projectile.NewProjectile(source, position.X, position.Y, velocity.X * sengs, velocity.Y * sengs, type, damage, knockback, player.whoAmI, 0f, ai3);
            return false;
        }
    }
}
