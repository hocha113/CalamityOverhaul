using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 暗影焰弓
    /// </summary>
    internal class RShadowFlameBow : BaseRItem
    {
        public override int TargetID => ItemID.ShadowFlameBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_ShadowFlameBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<ShadowFlameBowHeldProj>();
    }
}
