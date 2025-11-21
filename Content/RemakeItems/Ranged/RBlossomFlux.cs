using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBlossomFlux : CWRItemOverride
    {
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? On_CanUseItem(Item item, Player player) {
            return false;
        }
        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 35;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 38;
            Item.height = 68;
            Item.useTime = 4;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 0.15f;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ItemRarityID.Lime;
            Item.SetHeldProj<BlossomFluxHeldProj>();
        }
    }
}
