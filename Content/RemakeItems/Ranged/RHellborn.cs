using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHellborn : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 20;
            item.DamageType = DamageClass.Ranged;
            item.width = 62;
            item.height = 34;
            item.useAnimation = item.useTime = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 2f;
            item.rare = ItemRarityID.Pink;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.noMelee = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<HellbornHeld>(80);
        }
    }
}
