using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class BurntSiennaHeld : BaseKnife
    {
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BurntSienna_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            distanceToOwner = 16;
            drawTrailBtommWidth = 40;
            drawTrailTopWidth = 16;
            drawTrailCount = 8;
            Length = 44;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase1Ratio: 0.4f, phase0SwingSpeed: -0.1f, phase1SwingSpeed: 6.2f, phase2SwingSpeed: 4f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void Shoot() {
            Vector2 vr = ShootVelocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-15, 15))) * Main.rand.NextFloat(0.75f, 1.12f);
            int proj = Projectile.NewProjectile(Source, ShootSpanPos, vr
                , CWRID.Proj_DesertScourgeSpit, Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
            Main.projectile[proj].hostile = false;
            Main.projectile[proj].friendly = true;
            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, proj);
        }
    }
}
