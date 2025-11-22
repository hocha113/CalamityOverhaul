using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheMaelstrom : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 530;
            item.width = 20;
            item.height = 12;
            item.useTime = 15;
            item.useAnimation = 15;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3f;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.CWR().heldProjType = ModContent.ProjectileType<TheMaelstromHeld>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
