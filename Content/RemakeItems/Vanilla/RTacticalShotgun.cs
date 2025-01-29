using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 战术霰弹枪
    /// </summary>
    internal class RTacticalShotgun : ItemOverride
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
