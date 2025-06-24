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
    internal class RCoralCannon : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<CoralCannon>();

        public override void SetDefaults(Item item) {
            item.damage = 124;
            item.DamageType = DamageClass.Ranged;
            item.width = 52;
            item.height = 40;
            item.useTime = 90;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 7.5f;
            item.value = CalamityGlobalItem.RarityGreenBuyPrice;
            item.rare = ItemRarityID.Green;
            item.UseSound = SoundID.Item61;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<SmallCoral>();
            item.shootSpeed = 10f;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<CoralCannonHeldProj>();
        }
    }
}
