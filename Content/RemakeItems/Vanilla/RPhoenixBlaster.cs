using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 凤凰爆破枪
    /// </summary>
    internal class RPhoenixBlaster : CWRItemOverride
    {
        public override int TargetID => ItemID.PhoenixBlaster;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<PhoenixBlasterHeldProj>(22);
    }
}
