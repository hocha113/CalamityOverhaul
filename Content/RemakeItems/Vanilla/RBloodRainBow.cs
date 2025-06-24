using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 血雨弓
    /// </summary>
    internal class RBloodRainBow : CWRItemOverride
    {
        public override int TargetID => ItemID.BloodRainBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<BloodRainBowHeldProj>();
    }
}
