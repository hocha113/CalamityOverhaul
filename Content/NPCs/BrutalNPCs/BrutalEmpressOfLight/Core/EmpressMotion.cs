using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>光之女皇运动库与调色，状态共用</summary>
    internal static class EmpressMotion
    {
        #region 调色
        /// <summary>棱彩取色，hue 0~1 全饱和光谱（夜）</summary>
        public static Color Prism(float hue, float lum = 0.62f) => Main.hslToRgb(hue % 1f, 1f, lum);

        /// <summary>辐光金白族（昼）：t 0 深金→1 近白</summary>
        public static Color Sun(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return Color.Lerp(new Color(255, 212, 150), new Color(255, 246, 224), t);
        }

        /// <summary>辐光暗部（昼弹幕暗边/夜束芯，真 alpha 层用）</summary>
        public static readonly Color SunDark = new(120, 60, 20);

        /// <summary>按形态取色：昼归金白族，夜走光谱；hue 只在夜用</summary>
        public static Color FormColor(float hue, float dayBlend, float lum = 0.62f) {
            Color night = Prism(hue, lum);
            Color day = Sun(MathHelper.Clamp((lum - 0.4f) * 2f, 0f, 1f));
            return Color.Lerp(night, day, MathHelper.Clamp(dayBlend, 0f, 1f));
        }

        /// <summary>兼容旧调用名</summary>
        public static Color FormPrism(float hue, float dayBlend, float lum = 0.62f) => FormColor(hue, dayBlend, lum);
        #endregion

        #region 运动
        /// <summary>阻尼弹簧滑翔，优雅贴合目标点</summary>
        public static void SpringGlide(NPC npc, Vector2 target, float stiffness = 0.016f, float damping = 0.085f, float maxSpeed = 26f) {
            npc.velocity += (target - npc.Center) * stiffness;
            npc.velocity *= 1f - damping;
            if (npc.velocity.Length() > maxSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
        }

        /// <summary>
        /// 贴身追击（连接段用）：目标点=玩家 - 朝向×225 - (50·sign, 80)，速度插值 0.92，前 5 帧起步。
        /// 是追不是飘，连接段本身有压迫
        /// </summary>
        public static void DashTo(NPC npc, Vector2 targetPos, Vector2 targetVel, int frame, bool fast) {
            Vector2 back = npc.DirectionTo(targetPos) * -225f;
            targetPos.Y -= 80f;
            targetPos.X -= MathF.Sign(npc.DirectionTo(targetPos).X) * 50f;
            targetPos += back;
            targetVel *= fast ? 0.75f : 0.4f;
            float ramp = MathHelper.Clamp(frame / 5f, 0f, 1f);
            Vector2 desired = ((targetPos - npc.Center) * (fast ? 0.225f : 0.2f) + targetVel) * ramp;
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.92f);
        }

        /// <summary>转向追击：朝"玩家速度 + 朝向×50"的目标速度每帧修正 accel</summary>
        public static void Pursue(NPC npc, Player target, float accel, float lead = 50f) {
            Vector2 desired = target.velocity + npc.DirectionTo(target.Center) * lead;
            npc.velocity += (desired - npc.velocity).SafeNormalize(Vector2.Zero) * accel;
        }

        /// <summary>迭代解拦截点：按弹速与目标当前速度预测相遇位置，1.1 倍轻微过量</summary>
        public static Vector2 Intercept(Vector2 from, Player target, float speed) {
            Vector2 aim = target.Center;
            Vector2 vel = target.velocity;
            for (int i = 0; i < 10; i++) {
                aim = target.Center + vel * (Vector2.Distance(from, aim) / MathF.Max(speed, 1f));
            }
            return target.Center + vel * (Vector2.Distance(from, aim) * 1.1f / MathF.Max(speed, 1f));
        }

        /// <summary>角度向目标方向转动，单帧上限 maxDelta 弧度</summary>
        public static float TurnToward(float angle, float targetAngle, float maxDelta) {
            float diff = MathHelper.WrapAngle(targetAngle - angle);
            return angle + MathHelper.Clamp(diff, -maxDelta, maxDelta);
        }

        /// <summary>呼吸浮动偏移，seed 错相</summary>
        public static Vector2 Breathing(float seed, float amplitude = 12f) {
            float t = Main.GlobalTimeWrappedHourly * 1.8f + seed;
            return new Vector2((float)Math.Sin(t * 0.63f) * amplitude * 0.5f, (float)Math.Sin(t) * amplitude);
        }

        /// <summary>后撤蓄势偏移：pow(t,8) 迟滞回吸</summary>
        public static Vector2 ReelBack(Vector2 awayDir, float progress, float maxDist = 220f) {
            float p = MathHelper.Clamp(progress, 0f, 1f);
            return awayDir * (float)Math.Pow(p, 8) * maxDist;
        }

        /// <summary>棱彩位移闪现：旧位与新位各留一簇光尘与涟漪（纯表现，各端本地）</summary>
        public static void PrismStep(NPC npc, Vector2 newCenter, float dayBlend = 0f) {
            Vector2 oldCenter = npc.Center;
            npc.Center = newCenter;
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 14; i++) {
                float hue = i / 14f;
                Color c = FormColor(hue, dayBlend, 0.66f);
                PRTLoader.NewParticle<PRT_EmpressSpark>(oldCenter + Main.rand.NextVector2Circular(38f, 52f),
                    Main.rand.NextVector2Circular(4f, 4f), c, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(24, hue, dayBlend);
                PRTLoader.NewParticle<PRT_EmpressSpark>(newCenter + Main.rand.NextVector2Circular(38f, 52f),
                    Main.rand.NextVector2Circular(2f, 2f), c, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(20, hue, dayBlend);
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(oldCenter, Vector2.Zero, Color.White, 0.8f)?.Configure(18, Main.rand.NextFloat());
            PRTLoader.NewParticle<PRT_EmpressRipple>(newCenter, Vector2.Zero, Color.White, 1.1f)?.Configure(22, Main.rand.NextFloat());
        }
        #endregion

        #region 震屏
        /// <summary>本地距离衰减震屏</summary>
        public static void Shake(Vector2 worldPos, float strength, int frames) {
            if (VaultUtils.isServer || !CalamityOverhaul.Common.CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            float dist = Main.LocalPlayer.Distance(worldPos);
            if (dist > 1600f) {
                return;
            }
            float fade = MathHelper.Clamp(1f - dist / 1600f, 0.2f, 1f);
            PunchCameraModifier modifier = new(worldPos, Main.rand.NextVector2Unit(), strength * fade, 8f, frames, 1400f, "BrutalEmpress");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>定向震屏（沿 dir），大招落拍用</summary>
        public static void ShakeAlong(Vector2 worldPos, Vector2 dir, float strength, int frames) {
            if (VaultUtils.isServer || !CalamityOverhaul.Common.CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            float dist = Main.LocalPlayer.Distance(worldPos);
            if (dist > 2400f) {
                return;
            }
            float fade = MathHelper.Clamp(1f - dist / 2400f, 0.15f, 1f);
            PunchCameraModifier modifier = new(worldPos, dir.SafeNormalize(Vector2.UnitY), strength * fade, 9f, frames, 2200f, "BrutalEmpress");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>运镜期间的震屏走运镜通道（普通CameraModifier会被运镜锁镜吞掉）</summary>
        public static void CinematicShake(Vector2 worldPos, float strength, int frames) {
            if (VaultUtils.isServer || !CalamityOverhaul.Common.CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (InnoVault.Cinematics.CutsceneDirector.CurrentClip is EmpressDeathCutscene or EmpressGrabCutscene) {
                InnoVault.Cinematics.CutsceneDirector.Shake(Vector2.Zero, strength, 0.9f, frames);
                return;
            }
            Shake(worldPos, strength, frames);
        }
        #endregion

        #region 粒子
        /// <summary>手部蓄力光尘：向掌心汇聚的各向异性光丝（客户端），蓄至72%后静默</summary>
        public static void HandChargeDust(Vector2 hand, float progress, float dayBlend) {
            if (VaultUtils.isServer || progress <= 0.02f || progress > 0.72f) {
                return;
            }
            if (!Main.rand.NextBool(2)) {
                return;
            }
            float hue = Main.rand.NextFloat();
            Vector2 spawn = hand + Main.rand.NextVector2CircularEdge(90f, 90f) * (0.5f + progress * 0.5f);
            Vector2 vel = (hand - spawn) * 0.09f;
            PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, vel, FormColor(hue, dayBlend, 0.7f),
                Main.rand.NextFloat(0.6f, 1f))?.Configure(16, hue, dayBlend);
        }

        /// <summary>周身逸散光羽（低频环境粒子，客户端）</summary>
        public static void AmbientGlow(NPC npc, float dayBlend) {
            if (VaultUtils.isServer || !Main.rand.NextBool(6)) {
                return;
            }
            float hue = Main.rand.NextFloat();
            PRTLoader.NewParticle<PRT_EmpressPetalDust>(npc.Center + Main.rand.NextVector2Circular(60f, 70f),
                new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.9f, -0.2f)),
                FormColor(hue, dayBlend, 0.68f), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(46, hue, dayBlend);
        }

        /// <summary>一簇按形态取色的火花（发射/命中拍通用）</summary>
        public static void SparkBurst(Vector2 pos, Vector2 dir, int count, float speedMin, float speedMax, float dayBlend, float spread = 0.6f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                float hue = Main.rand.NextFloat();
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-spread, spread)) * Main.rand.NextFloat(speedMin, speedMax);
                PRTLoader.NewParticle<PRT_EmpressSpark>(pos, vel, FormColor(hue, dayBlend, 0.7f),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(12, 20), hue, dayBlend);
            }
        }
        #endregion
    }
}
