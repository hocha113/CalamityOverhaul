using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAlluvion : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 125;
            item.DamageType = DamageClass.Ranged;
            item.width = 62;
            item.height = 90;
            item.useTime = 18;
            item.useAnimation = 30;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 4f;
            item.UseSound = SoundID.Item5;
            item.autoReuse = true;
            item.shoot = ProjectileID.WoodenArrowFriendly;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Arrow;
            item.SetHeldProj<AlluvionHeldProj>();
        }
    }
}
