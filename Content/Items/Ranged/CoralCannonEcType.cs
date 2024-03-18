using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CoralCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CoralCannon";
        public override void SetDefaults() {
            Item.damage = 124;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 52;
            Item.height = 40;
            Item.useTime = 90;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 7.5f;
            Item.value = CalamityGlobalItem.Rarity2BuyPrice;
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item61;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<SmallCoral>();
            Item.shootSpeed = 10f;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<CoralCannonHeldProj>();
        }
    }
}
