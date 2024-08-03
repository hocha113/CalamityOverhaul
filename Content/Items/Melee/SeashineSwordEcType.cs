using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class SeashineSwordEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "SeashineSword";
        public override void SetDefaults() {
            Item.SetCalamitySD<SeashineSword>();
            Item.SetKnifeHeld<SeashineSwordHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }
}
