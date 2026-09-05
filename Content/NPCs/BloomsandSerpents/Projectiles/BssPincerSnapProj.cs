using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 巨钳合击判定：双螯在钳点合拢的一瞬，判定盒即钳口范围，只活 <see cref="BssDirector.SnapHitFrames"/> 帧。
    /// 钳合本身由鳌足骨架（Snatch 姿态）在各端本地演出，这里只负责伤害窗与合拢那一记沙爆。
    /// ai[0]=合拢朝向（弧度，沙爆沿它喷）。
    /// </summary>
    internal class BssPincerSnapProj : BssModProjectile
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle5";

        private float Facing => Projectile.ai[0];
        private float Progress => 1f - Projectile.timeLeft / (float)BssDirector.SnapHitFrames;

        public override void SetDefaults() {
            Projectile.width = (int)(BssDirector.SnapRadius * 2f);
            Projectile.height = (int)(BssDirector.SnapRadius * 2f);
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BssDirector.SnapHitFrames;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] != 0f || Main.dedServ) {
                return;
            }
            Projectile.localAI[0] = 1f;

            //合拢瞬间：干木钳合声 + 沿钳向喷出的沙夹 + 短震
            SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.9f, Pitch = -0.45f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
            BssVfx.Shake(Projectile.Center, 4f, 1000f);
            Vector2 dir = Facing.ToRotationVector2();
            for (int i = 0; i < 18; i++) {
                Vector2 vel = dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(3f, 8f);
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(18f, 18f), DustID.Sand,
                    vel, 90, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool();
            }
            //两侧夹合的沙线：从钳口两翼向中心合
            Vector2 side = dir.RotatedBy(MathHelper.PiOver2);
            for (int s = -1; s <= 1; s += 2) {
                for (int i = 0; i < 6; i++) {
                    Vector2 from = Projectile.Center + side * s * Main.rand.NextFloat(40f, 70f) + dir * Main.rand.NextFloat(-20f, 20f);
                    Dust d = Dust.NewDustPerfect(from, DustID.Sand, -side * s * Main.rand.NextFloat(3f, 6f), 110, default, 1.1f);
                    d.noGravity = true;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //圆形钳口判定
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, Projectile.Center) <= BssDirector.SnapRadius * BssDirector.SnapRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            //钳合冲击环：贴地压扁的沙色短环，一瞬绽开即散
            float p = MathHelper.Clamp(Progress, 0f, 1f);
            float radius = 26f + 74f * (1f - MathF.Pow(1f - p, 2f));
            float alpha = (1f - p) * 0.75f;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, 12f + 10f * p,
                new Color(240, 214, 160), BssVfx.SandWarm, BssVfx.SandDark, alpha,
                squish: 0.75f, innerGlow: 0.05f, timeSeed: Projectile.whoAmI * 0.37f);
            return false;
        }
    }
}
