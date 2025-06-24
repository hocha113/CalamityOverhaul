using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 地狱蝙蝠弓
    /// </summary>
    internal class RHellwingBow : CWRItemOverride
    {
        public override int TargetID => ItemID.HellwingBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<HellwingBowHeldProj>();
    }
}
