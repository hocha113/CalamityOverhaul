using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCItem : ModItem
    {
        //由SHPCOverride改，此处属性保兼容
        public override void SetDefaults() {
            Item.width = 152;
            Item.height = 70;
            Item.damage = 117;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 15;
            Item.useAnimation = Item.useTime = 60;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 3f;
            Item.UseSound = SoundID.Item92;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CyberPrismLaserProj>();
            Item.shootSpeed = 20f;
            Item.rare = CWRID.Rarity_HotPink > 0 ? CWRID.Rarity_HotPink : ItemRarityID.Purple;
        }
    }
}
