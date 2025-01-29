using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 玛瑙爆破枪
    /// </summary>
    internal class ROnyxBlaster : ItemOverride
    {
        public override int TargetID => ItemID.OnyxBlaster;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxBlasterHeldProj>(8);
    }
}
