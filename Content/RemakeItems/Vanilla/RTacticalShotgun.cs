using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 战术霰弹枪
    /// </summary>
    internal class RTacticalShotgun : CWRItemOverride
    {
        public override int TargetID => ItemID.TacticalShotgun;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.damage = 40;
            item.SetHeldProj<TacticalShotgunHeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 10;
        }
    }
}
