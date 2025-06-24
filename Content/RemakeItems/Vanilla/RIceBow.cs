using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 冰雪弓
    /// </summary>
    internal class RIceBow : CWRItemOverride
    {
        public override int TargetID => ItemID.IceBow;
        public override bool IsVanilla => true;

        public override void SetDefaults(Item item) {
            item.damage = 30;
            item.SetHeldProj<IceBowHeldProj>();
        }
    }
}
