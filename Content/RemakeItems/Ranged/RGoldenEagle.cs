using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RGoldenEagle : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<GoldenEagle>();
        public override int ProtogenesisID => ModContent.ItemType<GoldenEagleEcType>();
        public override string TargetToolTipItemName => "GoldenEagleEcType";
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
            item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            item.rare = ItemRarityID.Purple;
            item.UseSound = SoundID.Item41;
            item.autoReuse = true;
            item.shoot = ProjectileID.Bullet;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<GoldenEagleHeldProj>(38);
        }
    }
}
