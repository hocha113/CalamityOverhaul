using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core
{
    /// <summary>女王运动与通用演出库，状态共用</summary>
    internal static class QueenBeeMotion
    {
        /// <summary>蜂蜜金主色</summary>
        internal static Color HoneyGold => new(255, 196, 72);
        /// <summary>深琥珀</summary>
        internal static Color AmberDeep => new(198, 112, 24);
        /// <summary>蜂蜡淡黄</summary>
        internal static Color WaxPale => new(240, 218, 150);

        /// <summary>阻尼弹簧悬停</summary>
        public static void SpringHover(NPC npc, Vector2 target, float stiffness = 0.016f, float damping = 0.085f, float maxSpeed = 34f) {
            npc.velocity += (target - npc.Center) * stiffness;
            npc.velocity *= 1f - damping;
            if (npc.velocity.Length() > maxSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
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

        /// <summary>一帧速度置位式冲刺起步+琥珀音爆</summary>
        public static void DashLaunch(NPC npc, Vector2 direction, float speed, float boomStrength = 1f) {
            npc.velocity = direction * speed;
            AmberBoom(npc.Center, direction, boomStrength);
        }

        /// <summary>阶梯硬刹</summary>
        public static void BrakeHard(NPC npc, float brake = 0.72f) {
            npc.velocity *= brake;
            if (npc.velocity.Length() < 0.6f) {
                npc.velocity = Vector2.Zero;
            }
        }

        /// <summary>线性预判落点</summary>
        public static Vector2 PredictTarget(Player player, Vector2 from, float projSpeed, float leadFactor = 1f) {
            float flightTime = Vector2.Distance(from, player.Center) / Math.Max(projSpeed, 1f);
            return player.Center + player.velocity * flightTime * leadFactor;
        }

        /// <summary>屏幕震，受设置项与距离衰减</summary>
        public static void Shake(Vector2 pos, float strength, int frames) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, frames, 2200f, "QueenBeeMotion");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>琥珀音爆：正交冲击环+后向花粉火花+短震</summary>
        public static void AmberBoom(Vector2 pos, Vector2 direction, float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }

            PRTLoader.NewParticle<PRT_DWave>(pos, direction * 1.4f, HoneyGold, 0.24f * strength)?
                .Configure(new Vector2(1.4f, 0.55f), direction.ToRotation() + MathHelper.PiOver2, 1.05f * strength, 15);
            PRTLoader.NewParticle<PRT_DWave>(pos, direction * 0.7f, WaxPale * 0.7f, 0.14f * strength)?
                .Configure(new Vector2(1.15f, 0.7f), direction.ToRotation() + MathHelper.PiOver2, 0.68f * strength, 11);

            for (int i = 0; i < 8; i++) {
                Vector2 sparkVel = -direction.RotatedBy(Main.rand.NextFloat(-0.65f, 0.65f)) * Main.rand.NextFloat(4f, 10f) * strength;
                PRTLoader.NewParticle<PRT_Spark>(pos, sparkVel, HoneyGold,
                    Main.rand.NextFloat(0.9f, 1.5f) * strength)?.Configure(true, 17);
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.7f * strength, Pitch = 0.45f, MaxInstances = 3 }, pos);
            Shake(pos, 4f * strength, 8);
        }

        /// <summary>女王咆哮：吼声+扩散环+震屏</summary>
        public static void RoarBurst(Vector2 pos, float strength = 1f) {
            SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.95f * strength, Pitch = -0.12f }, pos);
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, HoneyGold, 0.3f * strength)?
                .Configure(Vector2.One, 0f, 1.5f * strength, 20);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, AmberDeep * 0.8f, 0.2f * strength)?
                .Configure(Vector2.One, 0f, 1f * strength, 15);
            Shake(pos, 5.5f * strength, 12);
        }

        /// <summary>蜂蜜迸溅：黏滴+雾+轻响</summary>
        public static void HoneyBurst(Vector2 pos, float scale, int dropCount = 10, bool withSound = true) {
            if (withSound) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.75f, Pitch = -0.5f, MaxInstances = 4 }, pos);
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 4 }, pos);
            }
            if (VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < dropCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 7f) * scale
                    - Vector2.UnitY * Main.rand.NextFloat(1f, 4f) * scale;
                PRTLoader.NewParticle<PRT_HoneyDrop>(pos + Main.rand.NextVector2Circular(8f, 8f) * scale, vel,
                    Color.Lerp(HoneyGold, AmberDeep, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.25f) * scale);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HoneyMist>(pos + Main.rand.NextVector2Circular(14f, 10f) * scale,
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f) + Main.rand.NextVector2Circular(0.8f, 0.4f),
                    HoneyGold * 0.5f, Main.rand.NextFloat(0.7f, 1.15f) * scale);
            }
            Lighting.AddLight(pos, HoneyGold.ToVector3() * 0.5f * scale);
        }

        /// <summary>蓄力内聚花粉粒</summary>
        public static void ChargeGatherFX(Vector2 center, float progress, float radius = 110f) {
            if (VaultUtils.isServer) {
                return;
            }
            //末段静默：蓄力最后四分之一收声收粒
            if (progress > 0.72f) {
                return;
            }
            Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(radius, radius) * (1f - progress * 0.45f);
            PRTLoader.NewParticle<PRT_Spark>(spawnPos, (center - spawnPos) * 0.1f,
                Color.Lerp(HoneyGold, WaxPale, Main.rand.NextFloat()),
                Main.rand.NextFloat(0.8f, 1.4f) * (0.55f + progress * 0.7f))?.Configure(false, 15);
        }

        /// <summary>蜂翅密鸣，稀疏播放防叠爆</summary>
        public static void WingHum(Vector2 pos, float volume = 0.4f, float pitch = -0.2f) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item97 with { Volume = volume, Pitch = pitch, MaxInstances = 2 }, pos);
        }
    }
}
