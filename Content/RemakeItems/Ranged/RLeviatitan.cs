using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RLeviatitan : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Leviatitan>();
        public override void SetDefaults(Item item) {
            item.damage = 80;
            item.DamageType = DamageClass.Ranged;
            item.width = 82;
            item.height = 28;
            item.useTime = 9;
            item.useAnimation = 9;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5f;
            item.value = CalamityGlobalItem.RarityLimeBuyPrice;
            item.rare = ItemRarityID.Lime;
            item.UseSound = SoundID.Item92;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<AquaBlast>();
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<LeviatitanHeldProj>(280);
        }
    }
}
