using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class SoulSeekerSkull : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "MourningSkull";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() => Projectile.CloneDefaults(CWRID.Proj_MourningSkull);

        public override void AI() {
            if (Projectile.ai[0] < 0f) {
                Projectile.alpha = 0;
            }
            if (Projectile.alpha > 0) {
                Projectile.alpha = Math.Max(0, Projectile.alpha - 50);
            }

            SetProjectileDirectionAndRotation();

            if (Projectile.timeLeft < 570) {
                //尝试获取最近的 NPC
                NPC target = Projectile.Center.FindClosestNPC(600);
                if (target != null && target.active) {
                    //如果找到目标 NPC，则调整弹幕的速度以追踪目标
                    AdjustVelocityTowardsTarget(target);
                }
                else {
                    //如果没有有效目标，寻找新的目标
                    FindNewTarget();
                }
                SetProjectileDirectionAndRotation();
                CreateDustEffect();
            }
        }

        private void SetProjectileDirectionAndRotation() {
            if (Projectile.velocity.X < 0f) {
                Projectile.spriteDirection = -1;
                Projectile.rotation = (float)Math.Atan2(-Projectile.velocity.Y, -Projectile.velocity.X);
            }
            else {
                Projectile.spriteDirection = 1;
                Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X);
            }
        }

        private void AdjustVelocityTowardsTarget(NPC target) {
            Vector2 projCenter = Projectile.Center;
            Vector2 directionToTarget = target.Center - projCenter;
            float distance = directionToTarget.Length();
            directionToTarget *= 8f / distance;
            Projectile.velocity = (Projectile.velocity * 14f + directionToTarget) / 15f;
        }

        private void FindNewTarget() {
            float closestDistance = 1000f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC potentialTarget = Main.npc[i];
                if (potentialTarget.CanBeChasedBy(Projectile)) {
                    float targetDistance = Projectile.Center.Distance(potentialTarget.Center);

                    if (targetDistance < closestDistance &&
                        Collision.CanHit(Projectile.position, Projectile.width, Projectile.height
                        , potentialTarget.position, potentialTarget.width, potentialTarget.height)) {
                        closestDistance = targetDistance;
                        Projectile.ai[0] = i;
                    }
                }
            }
        }

        private void CreateDustEffect() {
            const int offset = 8;
            Vector2 dustPosition = Projectile.position + new Vector2(offset);
            int dustWidth = Projectile.width - offset * 2;
            int dustHeight = Projectile.height - offset * 2;

            int dustType = Main.rand.NextBool() ? 5 : 6;
            int dustIndex = Dust.NewDust(dustPosition, dustWidth, dustHeight, dustType, 0f, 0f, 0, default, 1f);

            Dust dust = Main.dust[dustIndex];
            dust.velocity *= 0.5f;
            dust.velocity += Projectile.velocity * 0.5f;
            dust.noGravity = true;
            dust.noLight = true;
            dust.scale = 1.4f;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(200, 200, 200, Main.rand.Next(0, 128));

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);
            for (int i = 0; i < 5; i++) {
                int bloodyDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 2f);
                Main.dust[bloodyDust].velocity *= 3f;

                //随机调整某些尘埃的大小和淡入效果
                if (Main.rand.NextBool()) {
                    Main.dust[bloodyDust].scale = 0.5f;
                    Main.dust[bloodyDust].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                }
            }

            for (int j = 0; j < 10; j++) {
                //高速火焰尘埃
                int fieryDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 3f);
                Main.dust[fieryDust].noGravity = true;
                Main.dust[fieryDust].velocity *= 5f;
                //较慢的火焰尘埃
                fieryDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 2f);
                Main.dust[fieryDust].velocity *= 2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Daybreak, 300);
            for (int k = 0; k < 2; k++) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.InfernoFork,
                    Projectile.oldVelocity.X * 0.5f, Projectile.oldVelocity.Y * 0.5f);
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.position.X, Projectile.position.Y,
                    Main.rand.Next(-35, 36) * 0.2f, Main.rand.Next(-35, 36) * 0.2f,
                    CWRID.Proj_TinyFlare, (int)(Projectile.damage * 0.35),
                    Projectile.knockBack * 0.35f, Main.myPlayer, 0f, 0f
                );
            }
        }
    }
}
