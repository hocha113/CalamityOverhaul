using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.DragonsScaleGreatswordProj;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class DragonsScaleGreatswordHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DragonsScaleGreatsword>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "DragonsScaleGreatsword_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 112;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 4;
            distanceToOwner = -20;
            drawTrailTopWidth = 86;
            ownerOrientationLock = true;
            SwingData.starArg = 48;
            SwingData.baseSwingSpeed = 5;
            Length = 124;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.6f
                , phase1SwingSpeed: 6.6f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void MeleeEffect() {
            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.JungleSpore);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].scale = Main.rand.NextFloat(0.5f, 2.2f);
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<DragonsScaleGreatswordBeam>();
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, type, Item.damage / 2, 0, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(5)) {
                return;
            }
            int type = ModContent.ProjectileType<SporeCloud>();
            target.AddBuff(BuffID.Poisoned, 1200);
            if (Owner.ownedProjectileCounts[type] < 220) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spanPos = target.Center + new Vector2(Main.rand.Next(-723, 724), Main.rand.Next(-553, 0));
                    int proj = Projectile.NewProjectile(Owner.GetSource_FromThis(), spanPos
                        , spanPos.To(target.Center).UnitVector() * Main.rand.Next(9, 13), type, Item.damage / 2, 0, Owner.whoAmI);
                    Main.projectile[proj].timeLeft = 120;
                    Main.projectile[proj].scale = 1.2f + Main.rand.NextFloat(0.3f);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Poisoned, 600);
    }
}
