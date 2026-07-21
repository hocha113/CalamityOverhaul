using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>双子运动库，状态共用</summary>
    internal static class TwinsMotion
    {
        internal static Color SpazColor => new(255, 110, 35);
        internal static Color RetinColor => new(120, 200, 255);

        /// <summary>阻尼弹簧悬停，stiffness越大越跟，damping越小越冲</summary>
        public static void SpringHover(NPC npc, Vector2 target, float stiffness = 0.014f, float damping = 0.082f, float maxSpeed = 32f) {
            npc.velocity += (target - npc.Center) * stiffness;
            npc.velocity *= 1f - damping;
            if (npc.velocity.Length() > maxSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
        }

        /// <summary>呼吸浮动，seed 错相位</summary>
        public static Vector2 BreathingOffset(float seed, float amplitude = 14f) {
            float time = Main.GlobalTimeWrappedHourly * 2.1f + seed;
            return new Vector2((float)Math.Sin(time * 0.7f) * amplitude * 0.4f, (float)Math.Sin(time) * amplitude);
        }

        /// <summary>限转速弧线追踪，速恒定只改向</summary>
        public static void CurveChase(NPC npc, Vector2 target, float speed, float maxTurnRad) {
            if (npc.velocity == Vector2.Zero) {
                npc.velocity = (target - npc.Center).SafeNormalize(Vector2.UnitY) * speed;
                return;
            }
            float current = npc.velocity.ToRotation();
            float desired = (target - npc.Center).ToRotation();
            float next = current.AngleTowards(desired, maxTurnRad);
            npc.velocity = next.ToRotationVector2() * speed;
        }

        /// <summary>急停甩头</summary>
        public static void BrakeAndWhip(NPC npc, Vector2 faceTarget, float brake = 0.78f, float rotLerp = 0.28f) {
            npc.velocity *= brake;
            float targetRot = (faceTarget - npc.Center).ToRotation() - MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetRot, rotLerp);
        }

        /// <summary>限角速朝向，rotation=朝向-PiOver2</summary>
        public static void RotateToward(NPC npc, float targetDirRot, float maxStep) {
            float currentDir = npc.rotation + MathHelper.PiOver2;
            float nextDir = currentDir.AngleTowards(targetDirRot, maxStep);
            npc.rotation = nextDir - MathHelper.PiOver2;
        }

        /// <summary>线性预判落点</summary>
        public static Vector2 PredictTarget(Player player, Vector2 from, float projSpeed, float leadFactor = 1f) {
            float flightTime = Vector2.Distance(from, player.Center) / Math.Max(projSpeed, 1f);
            return player.Center + player.velocity * flightTime * leadFactor;
        }

        /// <summary>冲刺起步+音爆；boomStrength≥1.15 加扭曲环</summary>
        public static void DashLaunch(NPC npc, Vector2 direction, float speed, bool spazTheme, float boomStrength = 1f) {
            npc.velocity = direction * speed;
            SonicBoom(npc.Center, direction, spazTheme, boomStrength);

            if (boomStrength >= 1.15f && !VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<TwinsSupernovaBlast>(), 0, 0f, Main.myPlayer,
                    0f, spazTheme ? 1f : 0f);
            }
        }

        /// <summary>音爆冲击环/火花/短震</summary>
        public static void SonicBoom(Vector2 pos, Vector2 direction, bool spazTheme, float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }

            Color themeColor = spazTheme ? SpazColor : RetinColor;

            //冲刺正交冲击环
            PRTLoader.NewParticle<PRT_DWave>(pos, direction * 1.5f, themeColor, 0.25f * strength)?
                .Configure(new Vector2(1.45f, 0.55f), direction.ToRotation() + MathHelper.PiOver2, 1.1f * strength, 16);
            //余波
            PRTLoader.NewParticle<PRT_DWave>(pos, direction * 0.8f, Color.White * 0.7f, 0.15f * strength)?
                .Configure(new Vector2(1.2f, 0.7f), direction.ToRotation() + MathHelper.PiOver2, 0.7f * strength, 12);

            //后向火花
            for (int i = 0; i < 9; i++) {
                Vector2 sparkVel = -direction.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(4f, 11f) * strength;
                PRTLoader.NewParticle<PRT_TwinsSpark>(pos, sparkVel, Color.White,
                    Main.rand.NextFloat(1f, 1.7f) * strength)?.Configure(18, spazTheme ? 1 : 0);
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.85f * strength, Pitch = 0.25f }, pos);
            Shake(pos, 4.5f * strength, 8);
        }

        /// <summary>屏幕震，受设置项</summary>
        public static void Shake(Vector2 pos, float strength, int frames) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, frames, 2200f, "TwinsMotion");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>蓄力内聚粒子</summary>
        public static void ChargeGatherFX(Vector2 center, bool spazTheme, float progress, float radius = 90f) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(radius, radius) * (1f - progress * 0.5f);
            PRTLoader.NewParticle<PRT_TwinsSpark>(spawnPos, (center - spawnPos) * 0.11f,
                Color.White, Main.rand.NextFloat(1f, 1.6f) * (0.6f + progress * 0.7f))?
                .Configure(16, spazTheme ? 1 : 0);
        }
    }
}
