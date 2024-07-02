using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 幽漩风暴
    /// </summary>
    internal class TheMaelstromEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheMaelstrom";
        public override void SetDefaults() {
            Item.damage = 530;
            Item.width = 20;
            Item.height = 12;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MaelstromHoldout>();
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.Calamity().canFirePointBlankShots = true;
            Item.Calamity().donorItem = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<TheMaelstromHeldProj>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
