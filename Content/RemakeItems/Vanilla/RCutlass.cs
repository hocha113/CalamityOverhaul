using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 海盗弯刀
    /// </summary>
    internal class RCutlass : BaseRItem
    {
        public override int TargetID => ItemID.Cutlass;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<CutlassHeld>();
        }
    }

    internal class CutlassHeld : BaseKnife
    {
        public override int TargetID => ItemID.Cutlass;
        public override string gradientTexturePath => CWRConstant.ColorBar + "WindBlade_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 40;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 46;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwnerUpdate();
        }
    }
}
