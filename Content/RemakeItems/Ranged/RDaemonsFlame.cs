using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDaemonsFlame : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 150;
            item.width = 62;
            item.height = 128;
            item.useTime = 12;
            item.useAnimation = 12;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 4f;
            item.UseSound = SoundID.Item5;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<DaemonsFlameHeldProj>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.SetHeldProj<DaemonsFlameHeldProj>();
        }
    }
}
