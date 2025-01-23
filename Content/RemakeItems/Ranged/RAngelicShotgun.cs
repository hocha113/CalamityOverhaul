using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
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
    internal class RAngelicShotgun : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AngelicShotgun>();
        public override int ProtogenesisID => ModContent.ItemType<AngelicShotgunEcType>();
        public override string TargetToolTipItemName => "AngelicShotgunEcType";
        public override void SetDefaults(Item item) {
            item.damage = 126;
            item.knockBack = 3f;
            item.DamageType = DamageClass.Ranged;
            item.noMelee = true;
            item.autoReuse = true;
            item.width = 86;
            item.height = 38;
            item.useTime = 24;
            item.useAnimation = 24;
            item.UseSound = SoundID.Item38;
            item.useStyle = ItemUseStyleID.Shoot;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.Calamity().donorItem = true;
            item.shootSpeed = 12;
            item.shoot = ModContent.ProjectileType<HallowPointRoundProj>();
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<AngelicShotgunHeldProj>(50);
        }
    }
}
