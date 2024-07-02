using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 啸流
    /// </summary>
    internal class AlluvionEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Alluvion";
        public override void SetDefaults() {
            Item.damage = 165;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 62;
            Item.height = 90;
            Item.useTime = 15;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 4f;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().hasHeldNoCanUseBool = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<AlluvionHeldProj>();
        }
    }
}
