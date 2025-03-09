using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RSnowmanCannon : ItemOverride
    {
        public override int TargetID => ItemID.SnowmanCannon;
        public override bool FormulaSubstitution => false;
        public override void SetDefaults(Item item) {
            item.useTime = 60;
            item.damage = 50;
            item.SetCartridgeGun<SnowmanCannonHeldProj>(12);
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
