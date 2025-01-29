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
    internal class RGaleforce : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Galeforce>();

        public override void SetDefaults(Item item) {
            item.damage = 15;
            item.DamageType = DamageClass.Ranged;
            item.width = 32;
            item.height = 52;
            item.useTime = 20;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.RarityOrangeBuyPrice;
            item.rare = ItemRarityID.Orange;
            item.UseSound = SoundID.Item5;
            item.autoReuse = true;
            item.shoot = ProjectileID.WoodenArrowFriendly;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<GaleforceHeldProj>();
        }
    }
}
