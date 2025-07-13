using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 霰弹枪
    /// </summary>
    internal class RShotgun : CWRItemOverride
    {
        public override int TargetID => ItemID.Shotgun;
        
        public override void SetDefaults(Item item) {
            item.damage = 28;
            item.SetHeldProj<ShotgunHeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 8;
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
