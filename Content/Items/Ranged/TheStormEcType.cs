using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 风暴眼
    /// </summary>
    internal class TheStormEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheStorm";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 9));
        }

        public override void SetDefaults() {
            Item.damage = 35;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 54;
            Item.height = 90;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 3.5f;
            Item.value = CalamityGlobalItem.Rarity12BuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.UseSound = SoundID.Item122;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<Bolt>();
            Item.shootSpeed = 28f;
            Item.useAmmo = AmmoID.Arrow;
            Item.CWR().hasHeldNoCanUseBool = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<TheStormHeldProj>();
        }
    }
}
