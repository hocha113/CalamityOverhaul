using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class CometQuasherHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<CometQuasher>();
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

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(phase1Ratio: 0.2f, phase0SwingSpeed: 0.1f, phase1SwingSpeed: 4.2f, phase2SwingSpeed: 6f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            int proj = Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                    , ModContent.ProjectileType<CometQuasherMeteor>(), Projectile.damage / 2
                    , Projectile.knockBack, Owner.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits > 1) {
                return;
            }
            Vector2 offsetVr = CWRUtils.GetRandomVevtor(-75, -105, Main.rand.Next(500, 600));
            Vector2 spanPos = target.Center + offsetVr;
            Vector2 vr = offsetVr.UnitVector() * -17;
            int proj = Projectile.NewProjectile(Source, spanPos, vr
                , ModContent.ProjectileType<CometQuasherMeteor>(), Projectile.damage / 3
                , Projectile.knockBack, Owner.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits > 1) {
                return;
            }
            Vector2 offsetVr = CWRUtils.GetRandomVevtor(-75, -105, Main.rand.Next(500, 600));
            Vector2 spanPos = target.Center + offsetVr;
            Vector2 vr = offsetVr.UnitVector() * -17;
            int proj = Projectile.NewProjectile(Source, spanPos, vr
                , ModContent.ProjectileType<CometQuasherMeteor>(), Projectile.damage / 3
                , Projectile.knockBack, Owner.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }
    }
}
