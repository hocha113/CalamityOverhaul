using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 硫火短剑
    /// </summary>
    internal class BrimstoneSwordEcType : EctypeItem
    {
        public override string Texture => "CalamityMod/Items/Weapons/Melee/BrimstoneSword";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Rapier;
            Item.damage = 98;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 10;
            Item.shoot = ModContent.ProjectileType<BrimstoneSwordHeldProj>();
            Item.shootSpeed = 2f;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item1;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
        }

        public override bool MeleePrefix() => true;
    }
}
