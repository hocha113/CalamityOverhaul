using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPhangasm : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 120;
            item.width = 48;
            item.height = 82;
            item.useTime = 15;
            item.useAnimation = 15;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3f;
            item.UseSound = SoundID.Item5;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.CWR().heldProjType = ModContent.ProjectileType<PhangasmBowHeld>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
