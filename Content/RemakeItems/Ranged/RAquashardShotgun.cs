using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAquashardShotgun : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 9;
            item.DamageType = DamageClass.Ranged;
            item.width = 62;
            item.height = 26;
            item.useTime = 30;
            item.useAnimation = 30;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5.5f;
            item.rare = ItemRarityID.Orange;
            item.UseSound = SoundID.Item61;
            item.autoReuse = true;
            item.shootSpeed = 22f;
            item.useAmmo = AmmoID.Bullet;
            item.SetHeldProj<AquashardShotgunHeldProj>();
        }
    }
}
