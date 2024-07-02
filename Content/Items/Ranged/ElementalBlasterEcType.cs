using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 元素爆破
    /// </summary>
    internal class ElementalBlasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ElementalBlaster";
        public override void SetDefaults() {
            Item.damage = 67;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 104;
            Item.height = 42;
            Item.useTime = 6;
            Item.useAnimation = 6;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1.75f;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = CommonCalamitySounds.PlasmaBoltSound;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<RainbowBlast>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Bullet;
            Item.CWR().heldProjType = ModContent.ProjectileType<ElementalBlasterHeldProj>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
