using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 猎人长弓
    /// </summary>
    internal class MarksmanBowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MarksmanBow";
        public override void SetDefaults() {
            Item.damage = 35;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 36;
            Item.height = 110;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 6f;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.JestersArrow;
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Arrow;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.Calamity().donorItem = true;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<MarksmanBowHeldProj>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
