using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RMirrorBlade : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<MirrorBladeHeld>();
        }
    }

    internal class MirrorBladeHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 30;
            distanceToOwner = 12;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = 50;
            Projectile.height = 50;
            Length = 54;
        }

        public override bool PreSwingAI() {
            if (Time > 10 && Time % (4 * UpdateRate) == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, CWRID.Proj_MirrorBlast
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI, 0f, 0f);
            }
            return base.PreSwingAI();
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int addDamage = target.damage + 100;
            if (addDamage < 100) {
                addDamage = 100;
            }
            if (addDamage > 400) {
                addDamage = 400;
            }
            Item.damage = addDamage;
        }
    }
}
