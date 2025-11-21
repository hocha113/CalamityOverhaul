using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSeadragon : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 60;
            item.DamageType = DamageClass.Ranged;
            item.width = 90;
            item.height = 38;
            item.useTime = 6;
            item.useAnimation = 5;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2.5f;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.shootSpeed = 16f;
            item.useAmmo = AmmoID.Bullet;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<SeadragonHeldProj>();
        }
    }
}
