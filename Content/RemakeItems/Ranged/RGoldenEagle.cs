using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RGoldenEagle : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 85;
            item.DamageType = DamageClass.Ranged;
            item.noMelee = true;
            item.width = 46;
            item.height = 30;
            item.useTime = 19;
            item.useAnimation = 19;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3f;
            item.rare = ItemRarityID.Purple;
            item.UseSound = SoundID.Item41;
            item.autoReuse = true;
            item.shoot = ProjectileID.Bullet;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<GoldenEagleHeld>(38);
        }
    }
}
