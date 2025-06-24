using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheMaelstrom : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<TheMaelstrom>();
        public override void SetDefaults(Item item) {
            item.damage = 530;
            item.width = 20;
            item.height = 12;
            item.useTime = 15;
            item.useAnimation = 15;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<MaelstromHoldout>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.Calamity().canFirePointBlankShots = true;
            item.Calamity().donorItem = true;
            item.CWR().heldProjType = ModContent.ProjectileType<TheMaelstromHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
