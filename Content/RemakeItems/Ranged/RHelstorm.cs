using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHelstorm : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Helstorm>();

        public override void SetDefaults(Item item) {
            item.damage = 31;
            item.DamageType = DamageClass.Ranged;
            item.width = 50;
            item.height = 24;
            item.useTime = 7;
            item.useAnimation = 7;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 2.5f;
            item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.noMelee = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 11.5f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<HelstormHeldProj>(110);
        }
    }
}
