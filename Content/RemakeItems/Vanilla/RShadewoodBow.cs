using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 暗影木
    /// </summary>
    internal class RShadewoodBow : CWRItemOverride
    {
        public override int TargetID => ItemID.ShadewoodBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<ShadewoodBowHeldProj>();
    }
}
