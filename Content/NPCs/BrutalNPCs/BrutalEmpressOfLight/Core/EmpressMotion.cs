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
        /// <summary>棱彩取色，hue 0~1 全饱和光谱</summary>
        public static Color Prism(float hue, float lum = 0.62f) => Main.hslToRgb(hue % 1f, 1f, lum);

        /// <summary>昼形态取色偏白金，夜形态全光谱</summary>
        public static Color FormPrism(float hue, float dayBlend, float lum = 0.62f) {
            Color night = Prism(hue, lum);
            Color day = Color.Lerp(Prism(hue, 0.78f), new Color(255, 244, 214), 0.35f);
            return Color.Lerp(night, day, MathHelper.Clamp(dayBlend, 0f, 1f));
        }

        /// <summary>阻尼弹簧滑翔，优雅贴合目标点</summary>
        public static void SpringGlide(NPC npc, Vector2 target, float stiffness = 0.016f, float damping = 0.085f, float maxSpeed = 26f) {
            npc.velocity += (target - npc.Center) * stiffness;
            npc.velocity *= 1f - damping;
            if (npc.velocity.Length() > maxSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
        }

        /// <summary>呼吸浮动偏移，seed 错相</summary>
        public static Vector2 Breathing(float seed, float amplitude = 12f) {
            float t = Main.GlobalTimeWrappedHourly * 1.8f + seed;
            return new Vector2((float)Math.Sin(t * 0.63f) * amplitude * 0.5f, (float)Math.Sin(t) * amplitude);
        }

        /// <summary>后撤蓄势偏移：pow(t,8) 迟滞回吸（MOTION 反向预备）</summary>
        public static Vector2 ReelBack(Vector2 awayDir, float progress, float maxDist = 220f) {
            float p = MathHelper.Clamp(progress, 0f, 1f);
            return awayDir * (float)Math.Pow(p, 8) * maxDist;
        }

        /// <summary>棱彩位移闪现：旧位与新位各留一簇光尘与涟漪，掩盖瞬移感（纯表现，各端本地）</summary>
        public static void PrismStep(NPC npc, Vector2 newCenter) {
            Vector2 oldCenter = npc.Center;
            npc.Center = newCenter;
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 14; i++) {
                float hue = i / 14f;
                PRTLoader.NewParticle<PRT_EmpressSpark>(oldCenter + Main.rand.NextVector2Circular(38f, 52f),
                    Main.rand.NextVector2Circular(4f, 4f), Prism(hue), Main.rand.NextFloat(0.8f, 1.3f))?.Configure(24, hue);
                PRTLoader.NewParticle<PRT_EmpressSpark>(newCenter + Main.rand.NextVector2Circular(38f, 52f),
                    Main.rand.NextVector2Circular(2f, 2f), Prism(hue, 0.75f), Main.rand.NextFloat(0.7f, 1.1f))?.Configure(20, hue);
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(oldCenter, Vector2.Zero, Color.White, 0.8f)?.Configure(18, Main.rand.NextFloat());
            PRTLoader.NewParticle<PRT_EmpressRipple>(newCenter, Vector2.Zero, Color.White, 1.1f)?.Configure(22, Main.rand.NextFloat());
        }

        /// <summary>本地距离衰减震屏</summary>
        public static void Shake(Vector2 worldPos, float strength, int frames) {
            if (VaultUtils.isServer || !CalamityOverhaul.Common.CWRServerConfig.Instance.ScreenVibration) {
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

        /// <summary>运镜期间的震屏走运镜通道（普通CameraModifier会被运镜锁镜吞掉）</summary>
        public static void CinematicShake(Vector2 worldPos, float strength, int frames) {
            if (VaultUtils.isServer || !CalamityOverhaul.Common.CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            if (InnoVault.Cinematics.CutsceneDirector.CurrentClip is EmpressDeathCutscene) {
                InnoVault.Cinematics.CutsceneDirector.Shake(Vector2.Zero, strength, 0.9f, frames);
                return;
            }
            Shake(worldPos, strength, frames);
        }

        /// <summary>手部蓄力光尘：向掌心汇聚的各向异性光丝（客户端）</summary>
        public static void HandChargeDust(Vector2 hand, float progress, float dayBlend) {
            if (VaultUtils.isServer || progress <= 0.02f) {
                return;
            }
            //蓄至72%后静默，收势屏息（蓄力语法）
            if (progress > 0.72f) {
                return;
            }
            if (!Main.rand.NextBool(2)) {
                return;
            }
            float hue = Main.rand.NextFloat();
            Vector2 spawn = hand + Main.rand.NextVector2CircularEdge(90f, 90f) * (0.5f + progress * 0.5f);
            Vector2 vel = (hand - spawn) * 0.09f;
            PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, vel, FormPrism(hue, dayBlend, 0.7f),
                Main.rand.NextFloat(0.6f, 1f))?.Configure(16, hue);
        }

        /// <summary>周身逸散光羽（低频环境粒子，客户端）</summary>
        public static void AmbientGlow(NPC npc, float dayBlend) {
            if (VaultUtils.isServer || !Main.rand.NextBool(6)) {
                return;
            }
            float hue = Main.rand.NextFloat();
            PRTLoader.NewParticle<PRT_EmpressPetalDust>(npc.Center + Main.rand.NextVector2Circular(60f, 70f),
                new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.9f, -0.2f)),
                FormPrism(hue, dayBlend, 0.68f), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(46, hue);
        }
    }
}
