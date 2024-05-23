using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBlossomFlux : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.BlossomFlux>();
        public override int ProtogenesisID => ModContent.ItemType<BlossomFluxEcType>();
        public override string TargetToolTipItemName => "BlossomFluxEcType";
        public override void SetDefaults(Item item) {
            item.damage = 50;
            item.DamageType = DamageClass.Ranged;
            item.width = 38;
            item.height = 68;
            item.useTime = 4;
            item.useAnimation = 16;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 0.15f;
            item.UseSound = SoundID.Item5;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<LeafArrow>();
            item.shootSpeed = 10f;
            item.useAmmo = AmmoID.Arrow;
            item.value = CalamityGlobalItem.RarityLimeBuyPrice;
            item.rare = ItemRarityID.Lime;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<BlossomFluxHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }

        public override bool? On_CanUseItem(Item item, Player player) {
            return false;
        }
    }
}
