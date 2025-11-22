using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RThunderstorm : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 132;
            item.mana = 50;
            item.DamageType = DamageClass.Magic;
            item.width = 48;
            item.height = 22;
            item.useTime = 24;
            item.useAnimation = 24;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2f;
            item.autoReuse = true;
            item.shootSpeed = 6f;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<ThunderstormHeld>();
            item.CWR().Scope = true;
        }
    }
}
