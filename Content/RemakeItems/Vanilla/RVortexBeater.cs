using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RVortexBeater : ItemOverride
    {
        public override int TargetID => ItemID.VortexBeater;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<VortexBeaterHeldProj>(122);
            item.UseSound = CWRSound.Gun_AWP_Shoot with { Pitch = -0.3f, Volume = 0.12f };
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
