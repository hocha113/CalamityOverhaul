using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class DeathsAscensionHeld : BaseKnife
    {
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail4";
        public override string gradientTexturePath => CWRConstant.ColorBar + "ExaltedOathblade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 122;
            overOffsetCachesRoting = MathHelper.ToRadians(16);
            canDrawSlashTrail = true;
            drawTrailTopWidth = 52;
            drawTrailHighlight = false;
            drawTrailBtommWidth = 10;
            distanceToOwner = 50;
            Length = 70;
            OtherMeleeSize = 1.64f;
            unitOffsetDrawZkMode = 0;
            IgnoreImpactBoxSize = true;
            Incandescence = true;
        }

        public override void Shoot() {
            if (Projectile.ai[2] == 0) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vr = ((-1 + i) * 0.2f).ToRotationVector2().RotatedBy(ToMouseA);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center + vr * Length, vr * 16,
                CWRID.Proj_DeathsAscensionProjectile, (int)(Projectile.damage * 0.25f)
                , Projectile.knockBack / 2, Projectile.owner);
            }
        }

        public override bool PreInOwner() {
            if (Time == 0) {
                Projectile.DamageType = Item.DamageType;
                SoundEngine.PlaySound(SoundID.Item71, Owner.Center);
            }

            if (Projectile.ai[1] == 8) {
                if (Time == 0) {
                    OtherMeleeSize = 2.24f;
                }
                SwingData.baseSwingSpeed = 12;
                OtherMeleeSize -= 0.006f / SwingMultiplication;
                return true;
            }

            if (Projectile.ai[2] == 0) {
                if (Time == 0) {
                    OtherMeleeSize = 1.64f;
                }
                if (Projectile.ai[0] == 1) {
                    SwingData.baseSwingSpeed = 5;
                    SwingAIType = SwingAITypeEnum.Down;
                }

                if (Time < maxSwingTime / 3) {
                    OtherMeleeSize += 0.025f / SwingMultiplication;
                }
                else {
                    OtherMeleeSize -= 0.005f / SwingMultiplication;
                }
            }
            else {
                overOffsetCachesRoting = MathHelper.ToRadians(6);
                SwingData.starArg = 60;
                SwingData.baseSwingSpeed = 5;
                OtherMeleeSize -= 0.006f / SwingMultiplication;
            }

            return base.PreInOwner();
        }
    }
}
