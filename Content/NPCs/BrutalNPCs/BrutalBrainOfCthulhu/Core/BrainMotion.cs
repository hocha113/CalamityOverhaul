using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>克脑运动与演出库，状态共用</summary>
    internal static class BrainMotion
    {
        /// <summary>暗血主色</summary>
        internal static Color BloodDark => new(126, 16, 28);
        /// <summary>亮血色</summary>
        internal static Color BloodBright => new(206, 42, 46);
        /// <summary>假体冷紫色偏</summary>
        internal static Color MirrorCold => new(158, 92, 148);
        /// <summary>心光暖色</summary>
        internal static Color HeartGlow => new(255, 96, 84);

        /// <summary>阻尼弹簧悬停</summary>
        public static void SpringHover(NPC npc, Vector2 target, float stiffness = 0.012f, float damping = 0.085f, float maxSpeed = 22f) {
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

        /// <summary>屏幕震，受设置项</summary>
        public static void Shake(Vector2 pos, float strength, int frames) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, frames, 2400f, "BrainMotion");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>是否屏内(含边距)，客户端表现节流用</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 300f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        /// <summary>血雾团（本端）：Fog 染色 + 血珠</summary>
        public static void BloodMistBurst(Vector2 pos, float scale, int dropletCount = 6, float dropletSpeed = 6f) {
            if (VaultUtils.isServer || !OnScreen(pos)) {
                return;
            }

            int mistCount = 2 + (int)(scale * 3f);
            for (int i = 0; i < mistCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.6f) * scale;
                var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(pos + Main.rand.NextVector2Circular(18f, 18f) * scale,
                    vel, Color.Lerp(BloodDark, BloodBright, Main.rand.NextFloat(0.45f)) * 0.85f,
                    Main.rand.NextFloat(0.7f, 1.25f) * scale);
                mist?.Configure(Main.rand.Next(32, 55));
            }

            for (int i = 0; i < dropletCount; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.35f, 1f) * dropletSpeed * scale;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    Color.Lerp(BloodBright, BloodDark, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.5f) * scale)?.Configure(Main.rand.Next(24, 42), 0.34f);
            }

            Lighting.AddLight(pos, BloodBright.ToVector3() * 0.7f * scale);
        }

        /// <summary>瞬移撕开帧：撕裂声+血雾+短震（本端，由位移检测驱动，各端自播）</summary>
        public static void TeleportBurst(Vector2 pos, float scale, bool arriving) {
            if (VaultUtils.isServer) {
                return;
            }
            BloodMistBurst(pos, scale, arriving ? 8 : 5, arriving ? 7f : 5f);

            if (!OnScreen(pos)) {
                return;
            }

            //撕开环
            PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, BloodBright, 0.06f * scale)?
                .Configure(0.05f, 0.42f * scale, 16);

            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = arriving ? 0.9f : 0.6f,
                Pitch = arriving ? -0.45f : -0.7f,
                MaxInstances = 4
            }, pos);
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.75f,
                Pitch = -0.6f,
                MaxInstances = 4
            }, pos);

            if (arriving) {
                Shake(pos, 3.2f * scale, 9);
            }
        }

        /// <summary>服务端瞬移：设位+清速+netUpdate；表现由客户端位移检测自播</summary>
        public static void ServerTeleport(NPC npc, Vector2 destination, Vector2 exitVelocity) {
            npc.Center = destination;
            npc.velocity = exitVelocity;
            npc.netUpdate = true;
            npc.netSpam = 0;
        }

        /// <summary>咆哮（阶段用重音）</summary>
        public static void Roar(Vector2 pos, float volume = 1f, float pitch = -0.2f, bool heavy = false) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound((heavy ? SoundID.ForceRoarPitched : SoundID.Roar) with {
                Volume = volume,
                Pitch = pitch,
                MaxInstances = 2
            }, pos);
        }

        /// <summary>湿滑肉质音（假体碎裂/裂隙开合）</summary>
        public static void FleshSquish(Vector2 pos, float volume = 0.8f, float pitch = -0.35f) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = volume,
                Pitch = pitch,
                MaxInstances = 5,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, pos);
        }

        /// <summary>假体碎裂演出：冷紫血雾+镜片感光屑</summary>
        public static void MirrorShatter(Vector2 pos, float scale = 1f) {
            if (VaultUtils.isServer || !OnScreen(pos)) {
                return;
            }

            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.2f, 2.2f) * scale;
                var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(pos + Main.rand.NextVector2Circular(24f, 24f),
                    vel, Color.Lerp(MirrorCold, BloodDark, Main.rand.NextFloat(0.6f)) * 0.8f,
                    Main.rand.NextFloat(0.8f, 1.3f) * scale);
                mist?.Configure(Main.rand.Next(26, 44));
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 9f) * scale;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(MirrorCold, Color.White, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(true, Main.rand.Next(12, 20));
            }

            FleshSquish(pos, 0.8f, -0.15f);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.42f, Pitch = 0.25f, MaxInstances = 4 }, pos);
        }

        /// <summary>寻找脑本体（跨端一致，走 crimsonBoss 索引）</summary>
        public static NPC FindBrain() {
            int idx = NPC.crimsonBoss;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC brain = Main.npc[idx];
                if (brain.active && brain.type == NPCID.BrainofCthulhu) {
                    return brain;
                }
            }
            //兜底扫描
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.BrainofCthulhu) {
                    return n;
                }
            }
            return null;
        }

        /// <summary>0→1→0 短脉冲包络</summary>
        public static float Bump01(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return (float)Math.Sin(t * MathHelper.Pi);
        }

        /// <summary>极锐 ease-out（打击拍用）</summary>
        public static float SharpOut(float t, int power = 8) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f - (float)Math.Pow(1f - t, power);
        }
    }
}
