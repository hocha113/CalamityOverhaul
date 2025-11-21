using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheStorm : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 35;
            item.DamageType = DamageClass.Ranged;
            item.width = 54;
            item.height = 90;
            item.useTime = 14;
            item.useAnimation = 14;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3.5f;
            item.UseSound = SoundID.Item122;
            item.autoReuse = true;
            item.shootSpeed = 28f;
            item.useAmmo = AmmoID.Arrow;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<TheStormHeldProj>();
        }
    }
}
