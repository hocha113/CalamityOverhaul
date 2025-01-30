using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDisseminator : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Disseminator>();

        public override void SetDefaults(Item item) {
            item.damage = 48;
            item.DamageType = DamageClass.Ranged;
            item.width = 66;
            item.height = 24;
            item.useTime = 35;
            item.useAnimation = 35;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 4.5f;
            item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            item.rare = ItemRarityID.Red;
            item.UseSound = SoundID.Item38;
            item.autoReuse = true;
            item.shootSpeed = 13f;
            item.shoot = ProjectileID.PurificationPowder;
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<DisseminatorHeldProj>(85);
        }
    }
}
