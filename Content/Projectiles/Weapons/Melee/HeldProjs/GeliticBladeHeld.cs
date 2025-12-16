using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class GeliticBladeHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "Gel_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 38;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 3.5f;
            drawTrailBtommWidth = 40;
            drawTrailCount = 12;
            distanceToOwner = 24;
            drawTrailTopWidth = 20;
            Length = 50;
            ShootSpeed = 9;
        }

        public override void Shoot() {
            SoundEngine.PlaySound(SoundID.Item1, Owner.Center);
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , CWRID.Proj_GelWave, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slimed, 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 300);
        }
    }
}
