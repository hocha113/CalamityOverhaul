using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 海啸
    /// </summary>
    internal class RTsunami : CWRItemOverride
    {
        public override int TargetID => ItemID.Tsunami;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<TsunamiHeldProj>();
    }
}
