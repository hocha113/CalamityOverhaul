using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSeadragon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Seadragon>();
        public override int ProtogenesisID => ModContent.ItemType<SeadragonEcType>();
        public override string TargetToolTipItemName => "SeadragonEcType";
        public override void SetDefaults(Item item) {
            item.damage = 60;
            item.DamageType = DamageClass.Ranged;
            item.width = 90;
            item.height = 38;
            item.useTime = 6;
            item.useAnimation = 5;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2.5f;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<ArcherfishShot>();
            item.shootSpeed = 16f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<SeadragonHeldProj>();
        }
    }
}
