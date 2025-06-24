using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSDFMG : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<SDFMG>();
        public override void SetDefaults(Item item) {
            item.damage = 100;
            item.DamageType = DamageClass.Ranged;
            item.width = 74;
            item.height = 34;
            item.useTime = 2;
            item.useAnimation = 2;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2.75f;
            item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 16f;
            item.useAmmo = AmmoID.Bullet;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<SDFMGHeldProj>(800);
        }
    }
}
