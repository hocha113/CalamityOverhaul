using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class REvilSmasher : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<EvilSmasherHeld>();
        }
    }

    internal class EvilSmasherHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "Greentide_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 50;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 66;
        }

        public override bool PreInOwner() {
            int evilSmasherBoost = Owner.RefPlayerEvilSmasherBoost();
            if (Time == 0) {
                SwingData.baseSwingSpeed = 4f + evilSmasherBoost * 0.02f;
            }
            ExecuteAdaptiveSwing(initialMeleeSize: 1 + 0.015f * evilSmasherBoost,
                phase0SwingSpeed: -0.3f, phase1SwingSpeed: 5.2f, phase2SwingSpeed: 3f);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.life <= 0 && Owner.RefPlayerEvilSmasherBoost() < 10) {
                Owner.RefPlayerEvilSmasherBoost()++;
            }
        }
    }
}
