using CalamityMod.Items;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 信风
    /// </summary>
    internal class TradewindsEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Tradewinds";

        public override void SetDefaults() {
            Item.damage = 25;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 5;
            Item.width = 28;
            Item.height = 30;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5f;
            Item.value = CalamityGlobalItem.RarityOrangeBuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.UseSound = SoundID.Item7;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<TradewindsProjectile>();
            Item.shootSpeed = 20f;
            Item.SetHeldProj<TradewindsHeldProj>();
        }
    }
}
