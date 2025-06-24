using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 暗影焰弓
    /// </summary>
    internal class RShadowFlameBow : CWRItemOverride
    {
        public override int TargetID => ItemID.ShadowFlameBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<ShadowFlameBowHeldProj>();
    }
}
