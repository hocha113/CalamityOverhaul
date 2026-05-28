using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCItem : ModItem
    {
        //以SHPCOverride修改，这里写上属性是为了兼容性
        public override void SetDefaults() {
            Item.width = 124;
            Item.height = 52;
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
