using InnoVault.GameContent.BaseEntity;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class SepticSkewerProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "SepticSkewerHarpoon";
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public override void SetDefaults() => Projectile.CloneDefaults(CWRID.Proj_SepticSkewerHarpoon);

        public override void AI() {
            if (Main.rand.NextBool(5)) {
                _ = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , DustID.Venom, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }

            Vector2 playerDist = Owner.Center - Projectile.Center;
            Projectile.ai[1] += 1f;
            if (Projectile.ai[1] > 5f) {
                Projectile.alpha = 0;
            }

            if (Projectile.ai[1] % 8f == 0f && Projectile.IsOwnedByLocalPlayer() && Main.rand.NextBool(5)) {
                Vector2 harpoonVel = -playerDist;
                harpoonVel.Normalize();
                harpoonVel *= Main.rand.Next(45, 65) * 0.1f;
                harpoonVel = harpoonVel.RotatedBy((Main.rand.NextDouble() - 0.5) * MathHelper.PiOver2);
                _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, harpoonVel
                    , CWRID.Proj_SepticSkewerBacteria, (int)(Projectile.damage * 0.175)
                    , Projectile.knockBack * 0.2f, Projectile.owner, -10f, 0f);
            }

            if (Owner.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.extraUpdates = Projectile.ai[0] == 0f ? 2 : 3;

            Vector2 center = Projectile.Center;
            Vector2 ownerCenter = Owner.Center;
            Vector2 toOwner = ownerCenter - center;
            float distanceToOwner = toOwner.Length();

            if (Projectile.ai[0] == 0f) {
                if (distanceToOwner > 2000f) {
                    Projectile.ai[0] = 1f;
                }

                Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + 1.57f;

                if (Projectile.ai[1] > 8f) {
                    Projectile.ai[1] = 8f;
                }

                if (Projectile.ai[1] >= 10f) {
                    Projectile.ai[1] = 15f;
                    Projectile.velocity.Y += 0.3f;
                }
            }
            else {
                Projectile.tileCollide = false;
                Projectile.rotation = (float)Math.Atan2(toOwner.Y, toOwner.X) - 1.57f;

                if (distanceToOwner < 50f) {
                    Projectile.Kill();
                }

                float returnSpeed = 20f;
                float scale = returnSpeed / distanceToOwner;
                Projectile.velocity = toOwner * scale;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.ai[0] = 1f;
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            Projectile.damage = (int)(Projectile.damage * 0.9f);
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(CWRID.Buff_SulphuricPoisoning, 180);
    }
}
