using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class ForsakenSaberEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "ForsakenSaber";
        public override void SetDefaults() {
            Item.SetCalamitySD<ForsakenSaber>();
            Item.SetKnifeHeld<ForsakenSaberHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }
}
