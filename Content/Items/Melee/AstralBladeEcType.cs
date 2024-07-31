using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 幻星刃
    /// </summary>
    internal class AstralBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AstralBlade";
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 85;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.scale = 1;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AstralBall>();
            Item.shootSpeed = 11;
            Item.SetKnifeHeld<AstralBladeHeld>();
        }
        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;
    }
}
