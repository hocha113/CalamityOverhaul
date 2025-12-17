using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RContagion : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 280;
            item.DamageType = DamageClass.Ranged;
            item.width = 22;
            item.height = 50;
            item.useTime = 20;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.channel = true;
            item.knockBack = 5f;
            item.autoReuse = true;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.UseSound = SoundID.Item97 with { Pitch = -0.42f };
            item.CWR().heldProjType = ModContent.ProjectileType<ContagionHeld>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
