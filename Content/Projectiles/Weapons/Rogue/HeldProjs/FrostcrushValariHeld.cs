using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal class FrostcrushValariHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Cay_Wap_Rogue + "FrostcrushValari";
        public static int FireIndex = 1;
        internal bool accompanying {
            get => Projectile.ai[2] == 1;
            set {
                Projectile.ai[2] = value ? 1 : 0;
            }
        }

        public override void PostSetThrowable() {
            FireIndex *= -1;
            if (stealthStrike) {
                Projectile.scale *= 2;
            }
        }

        public override void PostUpdate() {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , DustID.IceRod, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
            if (accompanying && ReturnProgress != 1 && Projectile.timeLeft < 220) {
                NPC target = Projectile.Center.FindClosestNPC(600);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1, 0.3f);
                }
            }
        }

        public override bool PreReturnTrip() {
            return !accompanying;
        }

        public override bool PreThrowOut() {
            if (stealthStrike && !accompanying && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 2; i++) {
                    Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center
                        , Projectile.velocity.RotatedBy(i == 0 ? -0.3f : 0.3f), Type, Projectile.damage, 0.2f, Owner.whoAmI, 0, 0, 1);
                }
            }
            return base.PreThrowOut();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (stealthStrike && !accompanying) {
                int icicleAmt = Main.rand.Next(12, 14);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    for (int i = 0; i < icicleAmt; i++) {
                        Vector2 velocity = -Projectile.velocity.RotatedByRandom(0.65f) * Main.rand.NextFloat(0.3f, 0.45f);
                        int shard = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                            , Main.rand.NextBool() ? CWRID.Proj_Valaricicle
                            : CWRID.Proj_Valaricicle2, Projectile.damage / 3, 0f, Projectile.owner);
                        Main.projectile[shard].extraUpdates = 2;
                    }
                }
            }
            target.AddBuff(BuffID.Frostburn2, 120);
            if (stealthStrike) {
                target.AddBuff(CWRID.Buff_GlacialState, 45);
            }
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) {
            return true;
        }
    }
}
