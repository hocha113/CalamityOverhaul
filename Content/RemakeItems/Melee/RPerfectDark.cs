using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPerfectDark : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PerfectDarkHeld>();
    }

    internal class PerfectDarkHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "PerfectDark_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 14;
            drawTrailTopWidth = 20;
            Length = 50;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                , CWRID.Proj_DarkBall, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_BrainRot, 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_BrainRot, 300);
        }
    }
}
