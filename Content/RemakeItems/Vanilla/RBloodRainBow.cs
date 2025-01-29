using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 血雨弓
    /// </summary>
    internal class RBloodRainBow : ItemOverride
    {
        public override int TargetID => ItemID.BloodRainBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<BloodRainBowHeldProj>();
    }
}
