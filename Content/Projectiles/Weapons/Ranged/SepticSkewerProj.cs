using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class SepticSkewerProj : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "SepticSkewerHarpoon";
        public override void SetDefaults() => Projectile.CloneDefaults(ModContent.ProjectileType<SepticSkewerHarpoon>());

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (Main.rand.NextBool(5)) {
                _ = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.Venom, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
            Vector2 playerDist = owner.Center - Projectile.Center;
            Projectile.ai[1] += 1f;
            if (Projectile.ai[1] > 5f) {
                Projectile.alpha = 0;
            }
            if (Projectile.ai[1] % 8f == 0f && Projectile.owner == Main.myPlayer && Main.rand.NextBool(5)) {
                Vector2 harpoonPos = playerDist * -1f;
                harpoonPos.Normalize();
                harpoonPos *= Main.rand.Next(45, 65) * 0.1f;
                harpoonPos = harpoonPos.RotatedBy((Main.rand.NextDouble() - 0.5) * MathHelper.PiOver2);
                _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center.X, Projectile.Center.Y, harpoonPos.X, harpoonPos.Y, ModContent.ProjectileType<SepticSkewerBacteria>(), (int)(Projectile.damage * 0.175), Projectile.knockBack * 0.2f, Projectile.owner, -10f, 0f);
            }
            if (owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.extraUpdates = Projectile.ai[0] == 0f ? 2 : 3;
            Vector2 halfDist = new(Projectile.position.X + (Projectile.width * 0.5f), Projectile.position.Y + (Projectile.height * 0.5f));
            float xDist = owner.position.X + owner.width / 2 - halfDist.X;
            float yDist = owner.position.Y + owner.height / 2 - halfDist.Y;
            float playerDistance = (float)Math.Sqrt((double)((xDist * xDist) + (yDist * yDist)));
            if (Projectile.ai[0] == 0f) {
                if (playerDistance > 2000f) {
                    Projectile.ai[0] = 1f;
                }
                Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + 1.57f;
                Projectile.ai[1] += 1f;
                if (Projectile.ai[1] > 5f) {
                    Projectile.alpha = 0;
                }
                if (Projectile.ai[1] > 8f) {
                    Projectile.ai[1] = 8f;
                }
                if (Projectile.ai[1] >= 10f) {
                    Projectile.ai[1] = 15f;
                    Projectile.velocity.Y = Projectile.velocity.Y + 0.3f;
                }
            }
            else if (Projectile.ai[0] == 1f) {
                Projectile.tileCollide = false;
                Projectile.rotation = (float)Math.Atan2((double)yDist, (double)xDist) - 1.57f;
                float returnSpeed = 20f;
                if (playerDistance < 50f) {
                    Projectile.Kill();
                }
                playerDistance = returnSpeed / playerDistance;
                xDist *= playerDistance;
                yDist *= playerDistance;
                Projectile.velocity.X = xDist;
                Projectile.velocity.Y = yDist;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.ai[0] = 1f;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(ModContent.BuffType<SulphuricPoisoning>(), 180);
    }
}
