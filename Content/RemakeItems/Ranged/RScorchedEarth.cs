using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RScorchedEarth : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 350;
            item.DamageType = DamageClass.Ranged;
            item.useTime = 88;
            item.useAnimation = 32;
            item.reuseDelay = 60;
            item.useLimitPerAnimation = 4;
            item.width = 104;
            item.height = 44;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 8.7f;
            item.autoReuse = true;
            item.shootSpeed = 12.6f;
            item.useAmmo = AmmoID.Rocket;
            item.SetCartridgeGun<ScorchedEarthHeldProj>(4);
        }
    }
}
