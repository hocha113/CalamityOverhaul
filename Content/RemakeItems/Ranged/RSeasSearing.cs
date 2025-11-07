using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSeasSearing : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 40;
            item.DamageType = DamageClass.Ranged;
            item.width = 88;
            item.height = 44;
            item.useTime = 5;
            item.useAnimation = 10;
            item.reuseDelay = 16;
            item.useLimitPerAnimation = 3;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5f;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.useAmmo = AmmoID.Bullet;
            item.shoot = ModContent.ProjectileType<SeasSearingBubble>();
            item.shootSpeed = 13f;
            item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            item.rare = ItemRarityID.Pink;
            item.SetHeldProj<SeasSearingHeldProj>();
        }
    }
}
