using CalamityMod.Items;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RTradewinds : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Magic.Tradewinds>();
        public override int ProtogenesisID => ModContent.ItemType<TradewindsEcType>();
        public override string TargetToolTipItemName => "TradewindsEcType";

        public override void SetDefaults(Item item) {
            item.damage = 25;
            item.DamageType = DamageClass.Magic;
            item.mana = 5;
            item.width = 28;
            item.height = 30;
            item.useTime = 12;
            item.useAnimation = 12;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5f;
            item.value = CalamityGlobalItem.RarityOrangeBuyPrice;
            item.rare = ItemRarityID.Orange;
            item.UseSound = SoundID.Item7;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<TradewindsProjectile>();
            item.shootSpeed = 20f;
            item.SetHeldProj<TradewindsHeldProj>();
        }
    }
}
