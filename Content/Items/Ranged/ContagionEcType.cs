using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 瘟疫
    /// </summary>
    internal class ContagionEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Contagion";
        public override void SetDefaults() {
            Item.damage = 280;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 22;
            Item.height = 50;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 5f;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ContagionBow>();
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.UseSound = SoundID.Item5;
            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.rare = ModContent.RarityType<HotPink>();
            Item.Calamity().devItem = true;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<ContagionHeldProj>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
