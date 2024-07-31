using CalamityMod.Items;
using CalamityMod.Projectiles.Healing;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 沙之刃
    /// </summary>
    internal class BurntSiennaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BurntSienna";
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 42;
            Item.damage = 22;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 22;
            Item.useTurn = true;
            Item.useStyle = 1;
            Item.knockBack = 5.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 54;
            Item.value = CalamityGlobalItem.RarityBlueBuyPrice;
            Item.rare = ItemRarityID.Blue;
            Item.shoot = ModContent.ProjectileType<BurntSiennaProj>();
            Item.shootSpeed = 7f;
            Item.SetKnifeHeld<BurntSiennaHeld>();
        }
    }
}
