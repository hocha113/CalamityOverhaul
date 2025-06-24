using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 玛瑙爆破枪
    /// </summary>
    internal class ROnyxBlaster : CWRItemOverride
    {
        public override int TargetID => ItemID.OnyxBlaster;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxBlasterHeldProj>(8);
    }
}
