using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDeathwind : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 248;
            item.DamageType = DamageClass.Ranged;
            item.width = 40;
            item.height = 82;
            item.useTime = 14;
            item.useAnimation = 14;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.knockBack = 5f;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<DeathwindHeld>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.SetHeldProj<DeathwindHeld>();
        }
    }
}
