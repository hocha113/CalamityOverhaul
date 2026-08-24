using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core
{
    /// <summary>克眼运动与血材质演出库，状态共用</summary>
    internal static class EocMotion
    {
        #region 血色调色板（深红/血色/暗酒红，禁白热常驻）
        /// <summary>静脉暗红，拖尾外鞘</summary>
        internal static Color VenousDark => new(61, 6, 11);
        /// <summary>动脉血红，拖尾主体</summary>
        internal static Color Arterial => new(142, 15, 26);
        /// <summary>鲜血亮红，芯线与飞溅</summary>
        internal static Color BrightBlood => new(212, 33, 46);
        /// <summary>酒红雾色</summary>
        internal static Color MistWine => new(96, 14, 22);
        /// <summary>虹膜警示红</summary>
        internal static Color IrisRed => new(255, 60, 48);
        /// <summary>变轨欺诈的苍白闪色，只许 ≤4 帧脉冲</summary>
        internal static Color FeintPale => new(232, 208, 196);
        #endregion

        #region 运动
        /// <summary>阻尼弹簧悬停</summary>
        public static void SpringHover(NPC npc, Vector2 target, float stiffness = 0.014f, float damping = 0.085f, float maxSpeed = 26f) {
            npc.velocity += (target - npc.Center) * stiffness;
            npc.velocity *= 1f - damping;
            if (npc.velocity.Length() > maxSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
        }

        /// <summary>呼吸浮动</summary>
        public static Vector2 BreathingOffset(float seed, float amplitude = 16f) {
            float time = Main.GlobalTimeWrappedHourly * 1.9f + seed;
            return new Vector2((float)Math.Sin(time * 0.7f) * amplitude * 0.45f, (float)Math.Sin(time) * amplitude);
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

        /// <summary>后撤蓄力，pow8 末段猛吸</summary>
        public static void ReelBack(NPC npc, Vector2 awayDir, float t01, float pullSpeed = 4.6f) {
            float pull = MathF.Pow(MathHelper.Clamp(t01, 0f, 1f), 8f);
            npc.velocity = npc.velocity * 0.84f + awayDir * pull * pullSpeed;
        }

        /// <summary>线性预判落点</summary>
        public static Vector2 PredictTarget(Player player, Vector2 from, float projSpeed, float leadFactor = 1f) {
            float flightTime = Vector2.Distance(from, player.Center) / Math.Max(projSpeed, 1f);
            return player.Center + player.velocity * flightTime * leadFactor;
        }
        #endregion

        #region 冲刺演出
        /// <summary>冲刺起步：一帧满速+血爆+方向震屏</summary>
        public static void DashLaunch(NPC npc, EocStateContext context, Vector2 direction, float speed, float strength = 1f) {
            npc.velocity = direction * speed;
            context.PushDashVisuals(1f, 1f);
            LaunchBurst(npc.Center, direction, strength);
        }

        /// <summary>起步血爆：横向冲击环+逆向血滴喷洒+湿吼</summary>
        public static void LaunchBurst(Vector2 pos, Vector2 direction, float strength = 1f) {
            if (!VaultUtils.isServer) {
                //正交冲击环
                PRTLoader.NewParticle<PRT_DWave>(pos, direction * 1.4f, Arterial, 0.24f * strength)?
                    .Configure(new Vector2(1.5f, 0.5f), direction.ToRotation() + MathHelper.PiOver2, 1.05f * strength, 15);
                //逆向血滴
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = -direction.RotatedBy(Main.rand.NextFloat(-0.85f, 0.85f)) * Main.rand.NextFloat(3f, 12f) * strength;
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(24f, 24f), vel,
                        Color.Lerp(Arterial, BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.9f))?
                        .Configure(Main.rand.Next(22, 38), 0.3f, 0.985f);
                }
                //起步血雾
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_EocBloodMist>(pos - direction * 20f, -direction * Main.rand.NextFloat(1f, 3f),
                        MistWine, Main.rand.NextFloat(0.8f, 1.3f) * strength)?.Configure(Main.rand.Next(26, 40), 0.5f);
                }
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.7f * strength, Pitch = -0.35f }, pos);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f * strength, Pitch = -0.1f }, pos);
            }
            Shake(pos, 4.2f * strength, 9, direction);
        }

        /// <summary>变轨预告：苍白瞬闪+裂响，谎言前的公平语言</summary>
        public static void FeintBlink(NPC npc, EocStateContext context) {
            context.PushIris(1f, FeintPale);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.75f, Pitch = 0.65f }, npc.Center);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + Main.rand.NextVector2Circular(30f, 30f), vel,
                    FeintPale, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        /// <summary>变轨瞬间：谎言残影+转向血浪</summary>
        public static void KinkBurst(NPC npc, EocStateContext context, Vector2 oldVelocity, bool phase2) {
            //谎言残影沿旧轨道继续飞
            EocRenderHelper.PushLiarGhost(npc.Center, oldVelocity, npc.rotation, context.FrameIndex, phase2);
            context.PushDashVisuals(1f, 1f);
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 newDir = npc.velocity.SafeNormalize(Vector2.UnitY);
            PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, BrightBlood, 0.2f)?
                .Configure(new Vector2(1.2f, 0.62f), newDir.ToRotation() + MathHelper.PiOver2, 0.8f, 12);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = oldVelocity.SafeNormalize(Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f))
                    * Main.rand.NextFloat(4f, 9f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center, vel,
                    Color.Lerp(Arterial, BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.6f))?
                    .Configure(Main.rand.Next(18, 30), 0.32f, 0.982f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.8f, Pitch = 0.15f }, npc.Center);
        }

        /// <summary>刹车血珠</summary>
        public static void BrakeDroplets(NPC npc) {
            if (VaultUtils.isServer || npc.velocity.Length() < 5f || !OnScreen(npc.Center)) {
                return;
            }
            Vector2 back = -npc.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2f, 7f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center + Main.rand.NextVector2Circular(28f, 28f), vel,
                    Arterial, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(16, 26), 0.34f, 0.98f);
            }
        }
        #endregion

        #region 血材质演出
        /// <summary>蓄力内聚血丝：外圈血滴被吸入体内</summary>
        public static void ConvergeStreaks(Vector2 center, float progress, float radius = 120f) {
            if (VaultUtils.isServer) {
                return;
            }
            //末 1/4 静默，尖叫前的吸气
            if (progress > 0.75f) {
                return;
            }
            Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(radius, radius) * (1f - progress * 0.4f);
            Vector2 vel = (center - spawnPos) * 0.085f;
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(spawnPos, vel,
                Color.Lerp(Arterial, BrightBlood, progress), Main.rand.NextFloat(0.9f, 1.5f) * (0.6f + progress * 0.6f))?
                .Configure(16, 0f, 1f);
        }

        /// <summary>血喷泉锥形喷洒</summary>
        public static void BloodSpray(Vector2 pos, Vector2 dir, int count, float speed, float spread = 0.6f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-spread, spread)) * Main.rand.NextFloat(0.5f, 1f) * speed;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    Color.Lerp(Arterial, BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 2f))?
                    .Configure(Main.rand.Next(26, 44), 0.36f, 0.985f);
            }
        }

        /// <summary>血雾团</summary>
        public static void MistPuff(Vector2 pos, int count, float scale = 1f, float alpha = 0.55f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_EocBloodMist>(pos + Main.rand.NextVector2Circular(30f, 30f),
                    Main.rand.NextVector2Circular(1.6f, 1.6f), MistWine,
                    Main.rand.NextFloat(0.8f, 1.5f) * scale)?.Configure(Main.rand.Next(30, 55), alpha);
            }
        }

        /// <summary>大血爆：环+血滴+雾+湿爆音</summary>
        public static void BloodBurst(Vector2 pos, float strength = 1f, bool playSound = true) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Arterial, 0.22f * strength)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.35f * strength, 18);
            int drops = (int)(20 * strength);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 13f) * strength;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    Color.Lerp(Arterial, BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 2.1f))?
                    .Configure(Main.rand.Next(24, 44), 0.34f, 0.985f);
            }
            MistPuff(pos, (int)(4 * strength), strength, 0.6f);
            Lighting.AddLight(pos, Arterial.ToVector3() * 1.2f * strength);
            if (playSound) {
                SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.85f * MathHelper.Clamp(strength, 0.4f, 1.2f), Pitch = -0.25f }, pos);
            }
        }
        #endregion

        #region 通用
        /// <summary>屏幕震动，方向可选，距离衰减</summary>
        public static void Shake(Vector2 pos, float strength, int frames, Vector2? direction = null) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 dir = direction.HasValue && direction.Value != Vector2.Zero
                ? direction.Value.SafeNormalize(Vector2.UnitY) : Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new PunchCameraModifier(pos, dir, strength, 7f, frames, 2300f, "EocMotion");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>屏内判定，含边距</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 300f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        /// <summary>雾步：旧位血雾爆散，闪现至玩家外圈，权威端调用</summary>
        public static void FogStep(NPC npc, Player target, float distance = 1050f) {
            Vector2 oldPos = npc.Center;
            Vector2 dir = (npc.Center - target.Center).SafeNormalize(-Vector2.UnitY);
            npc.Center = target.Center + dir * distance;
            npc.velocity = -dir * Math.Max(npc.velocity.Length(), 14f);
            npc.netUpdate = true;
            //两端粒子由各自帧内 MistPuff 覆盖，这里只做权威侧掉帧兜底
            MistPuff(oldPos, 6, 1.4f);
            MistPuff(npc.Center, 6, 1.4f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.6f, Pitch = -0.5f }, npc.Center);
            }
        }
        #endregion
    }
}
