using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAngelicShotgun : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 126;
            item.knockBack = 3f;
            item.DamageType = DamageClass.Ranged;
            item.noMelee = true;
            item.autoReuse = true;
            item.width = 86;
            item.height = 38;
            item.useTime = 24;
            item.useAnimation = 24;
            item.UseSound = SoundID.Item38;
            item.useStyle = ItemUseStyleID.Shoot;
            item.shootSpeed = 12;
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<AngelicShotgunHeldProj>(50);
        }
    }
}
