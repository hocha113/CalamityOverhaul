using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAnarchyBlade : CWRItemOverride
    {
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 114;
            Item.damage = 150;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 19;
            Item.useTime = 19;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.height = 122;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<AnarchyBeam>();
            Item.shootSpeed = 15;
            Item.SetKnifeHeld<AnarchyBladeHeld>();
        }
    }
}
