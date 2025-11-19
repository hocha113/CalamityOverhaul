using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCatastropheClaymore : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.useTime = 16;
            item.SetKnifeHeld<CatastropheClaymoreHeld>();
        }
    }

    internal class CatastropheClaymoreHeld : BaseKnife
    {
        public override int TargetID => CWRItemOverride.GetCalItemID("CatastropheClaymore");
        public override string gradientTexturePath => CWRConstant.ColorBar + "CatastropheClaymore_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 60;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 5f;
            Projectile.width = Projectile.height = 46;
            Length = 56;
            autoSetShoot = true;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy);
            }
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: -0.1f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 6f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void Shoot() {
            int type = CWRID.Proj_CatastropheClaymoreSparkle;
            int proj = Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, type
                , Projectile.damage, Projectile.knockBack, Main.myPlayer, Main.rand.Next(3));
            Main.projectile[proj].extraUpdates = 2;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextBool(3)) {
                target.AddBuff(BuffID.Ichor, 60);
                target.AddBuff(BuffID.OnFire3, 180);
                target.AddBuff(BuffID.Frostburn2, 120);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Main.rand.NextBool(3)) {
                target.AddBuff(BuffID.Ichor, 60);
                target.AddBuff(BuffID.OnFire3, 180);
                target.AddBuff(BuffID.Frostburn2, 120);
            }
        }
    }
}
