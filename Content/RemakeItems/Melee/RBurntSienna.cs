using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Healing;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBurntSienna : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<BurntSienna>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 42;
            Item.damage = 22;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 22;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.5f;
            Item.UseSound = null;
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
