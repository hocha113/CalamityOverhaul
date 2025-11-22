using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAntiMaterielRifle : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 1060;
            item.DamageType = DamageClass.Ranged;
            item.width = 154;
            item.height = 40;
            item.useTime = 60;
            item.useAnimation = 60;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 9.5f;
            item.autoReuse = true;
            item.shoot = ProjectileID.Bullet;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<AntiMaterielRifleHeld>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 9;
            item.CWR().Scope = true;
        }
    }
}
