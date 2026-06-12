using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    /// <summary>
    /// 火力阵微型寻热导弹：上抛滞空 → 错相点火俯冲 → 微追踪蛇行。
    /// 追踪力随时间衰减、近身自动熄锁，横向位移即可甩脱（公平阀）。
    /// <para><c>ai[0]</c> = 目标玩家索引；<c>ai[1]</c> = 每发种子（点火错相/散布/蛇行相位）</para>
    /// </summary>
    internal class PrimeSeekerMissile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "DestroyerGrenade";

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float Seed => ref Projectile.ai[1];
        private ref float Tick => ref Projectile.localAI[0];

        /// <summary>点火前滞空 tick 数，按种子在 20~36 间错相，让弹群依次俯冲</summary>
        private int IgniteTick => 20 + (int)(Hash01(Seed) * 16f);
        private const int DiveTicks = 26;
        private const int HuntTicks = 96;
        private float maxSpeed;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 260;
            Projectile.extraUpdates = 1;
            CooldownSlot = ImmunityCooldownID.Bosses;

            maxSpeed = 7.2f;
            if (Main.masterMode) {
                maxSpeed += 0.8f;
            }
            if (CWRRef.GetBossRushActive() || Main.zenithWorld) {
                maxSpeed += 1.2f;
            }
        }

        /// <summary>黄金比散列：种子 → [0,1) 确定性伪随机，各端一致</summary>
        private static float Hash01(float seed) => (seed * 0.6180339887f) % 1f;

        public override void AI() {
            Player target = CWRUtils.GetPlayerInstance((int)TargetIndex);
            float hash = Hash01(Seed);
            float weavePhase = hash * MathHelper.TwoPi;
            int ignite = IgniteTick;

            if (Tick == 0) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                LaunchEffect(hash);
            }

            if (Tick < ignite) {
                //滞空段：上抛减速，只冒薄烟
                Projectile.velocity *= 0.965f;
                SpawnExhaust(0.4f, smokeOnly: true);
            }
            else if (Tick < ignite + DiveTicks) {
                if (Tick == ignite && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.35f, Pitch = 0.5f }, Projectile.Center);
                }
                //点火俯冲：朝玩家周围的散布点强转向调头，爆发加速
                if (target.Alives()) {
                    Vector2 aim = target.Center + new Vector2((hash - 0.5f) * 360f, 0f);
                    Projectile.SmoothHomingBehavior(aim, 1f, 0.105f);
                }
                float diveSpeed = MathF.Min(Projectile.velocity.Length() * 1.05f + 0.1f, maxSpeed * 0.85f);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * diveSpeed;
                SpawnExhaust(1f);
            }
            else if (Tick < ignite + DiveTicks + HuntTicks) {
                //微追踪段：转向力线性衰减，近身熄锁，叠加蛇行
                float huntT = (Tick - ignite - DiveTicks) / (float)HuntTicks;
                if (target.Alives() && Projectile.Distance(target.Center) > 90f) {
                    float turn = MathHelper.Lerp(0.032f, 0.005f, huntT);
                    Projectile.SmoothHomingBehavior(target.Center, 1f, turn);
                }
                float speed = MathF.Min(Projectile.velocity.Length() + 0.06f, maxSpeed);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * speed;
                Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Tick * 0.11f + weavePhase) * 0.018f);
                SpawnExhaust(0.85f);
            }
            else {
                //熄火段：失去制导直线掠过，残余蛇行
                Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Tick * 0.07f + weavePhase) * 0.006f);
                SpawnExhaust(0.3f, smokeOnly: true);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.7f, 0.45f, 0.15f);
            Tick++;
        }

        private void LaunchEffect(float hash) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.4f, Pitch = 0.3f + hash * 0.25f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch
                    , Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(2f, 2f), 100, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }
        }

        private void SpawnExhaust(float intensity, bool smokeOnly = false) {
            if (Main.dedServ) {
                return;
            }

            Vector2 tail = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitY) * 12f;
            if (PRTLoader.NumberUsablePRT() > 10) {
                if (!smokeOnly && Tick % 2 == 0) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(tail, -Projectile.velocity * 0.25f
                        , Color.OrangeRed, 0.8f * intensity).Configure(false, 7);
                }
                if (Tick % 6 == 0) {
                    PRTLoader.NewParticle<PRT_Smoke>(tail, -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.4f, 0.4f)
                        , Color.DimGray, 0.09f).Configure(20, 0.3f * intensity, 0.02f);
                }
            }
            else if (Tick % 2 == 0) {
                Dust dust = Dust.NewDustPerfect(tail, smokeOnly ? DustID.Smoke : DustID.Torch, -Projectile.velocity * 0.3f);
                dust.noGravity = true;
                dust.scale = 1.2f * intensity;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => Projectile.Kill();

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.3f, Pitch = 0.35f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch
                    , Main.rand.NextVector2Circular(4f, 4f), 100, default, Main.rand.NextFloat(1.2f, 1.9f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke
                    , Main.rand.NextVector2Circular(2f, 2f), 120, Color.DarkGray, 1.2f);
                dust.fadeIn = 0.6f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.GetRectangle();
            Vector2 origin = frame.Size() / 2f;

            //热焰残影拖尾：越新越亮越大
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float fade = (Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;
                Color glow = new Color(255, 140, 40, 0) * (fade * 0.45f);
                Main.EntitySpriteDraw(tex, drawPos, frame, glow, Projectile.oldRot[k]
                    , origin, Projectile.scale * (0.5f + fade * 0.5f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, Color.White
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
