using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCometQuasher : CWRItemOverride
    {
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 46;
            Item.height = 62;
            Item.scale = 1.5f;
            Item.damage = 96;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.knockBack = 2.75f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shootSpeed = 9f;
            Item.SetKnifeHeld<CometQuasherHeld>();
        }
    }
}
