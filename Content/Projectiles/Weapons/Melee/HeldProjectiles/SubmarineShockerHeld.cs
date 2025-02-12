using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.SparkProj;
using CalamityOverhaul.Content.RemakeItems.Melee;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class SubmarineShockerHeld : BaseKnife
    {
        public override string Texture => "CalamityMod/Items/Weapons/Melee/SubmarineShocker";
        public override int TargetID => ModContent.ItemType<SubmarineShocker>();
        private bool trueMelee;
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 24;
            Length = 20;
            autoSetShoot = true;
        }

        public override bool PreSwingAI() {
            StabBehavior(initialLength: 20, lifetime: maxSwingTime, scaleFactorDenominator: 420f, minLength: 20, maxLength: 40);
            return false;
        }

        public override void Shoot() {
            if (trueMelee || !RSubmarineShocker.canShoot) {
                RSubmarineShocker.canShoot = true;
                return;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity
                    , ModContent.ProjectileType<LigSpark>(), (int)(Projectile.damage * 0.7f), Projectile.knockBack, Main.myPlayer);
            RSubmarineShocker.canShoot = false;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            trueMelee = true;
            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(5)) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source, target.Center, Main.rand.NextVector2Unit() * Main.rand.Next(2, 5)
                    , ModContent.ProjectileType<SparkBall>(), (int)(Projectile.damage * 0.7f), Projectile.knockBack, Main.myPlayer);
                Main.projectile[proj].penetrate = 2;
                Main.projectile[proj].localNPCHitCooldown = 30;
            }
        }
    }
}
