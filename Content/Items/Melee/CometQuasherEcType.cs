using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 彗星陨刃
    /// </summary>
    internal class CometQuasherEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CometQuasher";
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 46;
            Item.height = 62;
            Item.scale = 1.5f;
            Item.damage = 80;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.knockBack = 2.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<CometQuasherMeteor>();
            Item.shootSpeed = 9f;
            Item.SetKnifeHeld<CometQuasherHeld>();
        }
    }
}
