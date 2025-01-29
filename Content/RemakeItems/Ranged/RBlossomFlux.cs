using CalamityMod.Items;
using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBlossomFlux : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<BlossomFlux>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? On_CanUseItem(Item item, Player player) {
            return false;
        }
        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 50;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 38;
            Item.height = 68;
            Item.useTime = 4;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 0.15f;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<LeafArrow>();
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Arrow;
            Item.value = CalamityGlobalItem.RarityLimeBuyPrice;
            Item.rare = ItemRarityID.Lime;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<BlossomFluxHeldProj>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
