using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RGhastlyVisage : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Magic.GhastlyVisage>();
        public override int ProtogenesisID => ModContent.ItemType<GhastlyVisageEcType>();
        public override string TargetToolTipItemName => "GhastlyVisageEcType";
        public override void SetDefaults(Item item) {
            item.damage = 69;
            item.DamageType = DamageClass.Magic;
            item.noUseGraphic = true;
            item.channel = true;
            item.mana = 20;
            item.width = 32;
            item.height = 36;
            item.useTime = 27;
            item.useAnimation = 27;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5f;
            item.shootSpeed = 9f;
            item.shoot = ModContent.ProjectileType<GhastlyBlasts>();
            item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.SetHeldProj<GhastlyVisageHeldProj>();
        }

        public override void OnConsumeMana(Item item, Player player, int manaConsumed) {
            player.statMana += manaConsumed;
        }
    }
}
