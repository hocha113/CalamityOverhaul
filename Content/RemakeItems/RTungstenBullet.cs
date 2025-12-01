using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal class RTungstenBullet : CWRItemOverride
    {
        public override int TargetID => ItemID.TungstenBullet;
        public override bool CanLoadLocalization => false;
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<TungstenBulletProj>();
        }
    }
}
