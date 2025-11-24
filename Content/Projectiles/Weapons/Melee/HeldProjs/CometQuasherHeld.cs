using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class CometQuasherHeld : BaseKnife
    {
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 52;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase1Ratio: 0.2f, phase0SwingSpeed: 0.1f, phase1SwingSpeed: 4.2f, phase2SwingSpeed: 6f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void Shoot() {
            int proj = Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                    , CWRID.Proj_CometQuasherMeteor, Projectile.damage / 2
                    , Projectile.knockBack, Owner.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            //Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits > 1) {
                return;
            }
            Vector2 offsetVr = VaultUtils.RandVrInAngleRange(-75, -105, Main.rand.Next(500, 600));
            Vector2 spanPos = target.Center + offsetVr;
            Vector2 vr = offsetVr.UnitVector() * -17;
            int proj = Projectile.NewProjectile(Source, spanPos, vr
                , CWRID.Proj_CometQuasherMeteor, Projectile.damage / 3
                , Projectile.knockBack, Owner.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            //Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits > 1) {
                return;
            }
            Vector2 offsetVr = VaultUtils.RandVrInAngleRange(-75, -105, Main.rand.Next(500, 600));
            Vector2 spanPos = target.Center + offsetVr;
            Vector2 vr = offsetVr.UnitVector() * -17;
            int proj = Projectile.NewProjectile(Source, spanPos, vr
                , CWRID.Proj_CometQuasherMeteor, Projectile.damage / 3
                , Projectile.knockBack, Owner.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            //Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }
    }
}
