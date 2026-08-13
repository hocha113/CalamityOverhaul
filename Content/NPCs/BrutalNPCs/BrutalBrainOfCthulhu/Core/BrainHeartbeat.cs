using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>
    /// 心跳压迫系统：全屏脉动/低频音/二阶段血幕/骤停黑幕，客户端静态存储
    /// 由 BrainOfCthulhuAI 每帧推送状态，BrainScreenRender 消费
    /// </summary>
    internal static class BrainHeartbeat
    {
        /// <summary>本帧心跳脉冲包络 0~1（收缩期瞬间冲高后指数衰减）</summary>
        public static float Pulse { get; private set; }
        /// <summary>脑所在世界坐标（脉动中心）</summary>
        public static Vector2 WorldCenter { get; private set; }
        /// <summary>整体强度 0~1（随距离与阶段衰减）</summary>
        public static float Intensity { get; private set; }
        /// <summary>二阶段血幕 0~1</summary>
        public static float Veil { get; private set; }
        /// <summary>骤停黑幕 0~1（心搏骤停/死亡演出死寂）</summary>
        public static float Blackout { get; private set; }
        /// <summary>死亡终爆负片帧 0~1</summary>
        public static float ImpactFlash { get; private set; }
        /// <summary>本帧是否有活跃的脑在推送</summary>
        public static bool ActiveThisFrame { get; private set; }

        private static int framesSincePush;
        private static float pulseDecay = 0.90f;

        public static bool HasAny => ActiveThisFrame || Pulse > 0.02f || Veil > 0.02f || Blackout > 0.02f || ImpactFlash > 0.02f;

        /// <summary>每帧由脑 AI 推送（非服务端）；veil/blackout 用趋近避免硬跳</summary>
        public static void Push(Vector2 worldCenter, float intensity, float veilTarget, float blackoutTarget) {
            if (VaultUtils.isServer) {
                return;
            }
            WorldCenter = worldCenter;
            Intensity = MathHelper.Clamp(intensity, 0f, 1f);
            Veil = MathHelper.Lerp(Veil, MathHelper.Clamp(veilTarget, 0f, 1f), 0.06f);
            Blackout = MathHelper.Lerp(Blackout, MathHelper.Clamp(blackoutTarget, 0f, 1f), blackoutTarget > Blackout ? 0.10f : 0.05f);
            ActiveThisFrame = true;
            framesSincePush = 0;
        }

        /// <summary>触发一次收缩拍（视觉冲高），strength 允许 >1 表示重拍</summary>
        public static void Thump(float strength, float decay = 0.90f) {
            if (VaultUtils.isServer) {
                return;
            }
            Pulse = Math.Max(Pulse, MathHelper.Clamp(strength, 0f, 1.5f));
            pulseDecay = decay;
        }

        /// <summary>死亡终爆负片帧</summary>
        public static void PushImpactFlash(float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            ImpactFlash = Math.Max(ImpactFlash, MathHelper.Clamp(strength, 0f, 1f));
        }

        /// <summary>播放一次 lub-dub 双响心音（调用端负责节拍去重）</summary>
        public static void PlayThumpSound(Vector2 pos, float volume, float pitchShift = 0f) {
            if (VaultUtils.isServer || volume <= 0.02f) {
                return;
            }
            //lub：闷重低频
            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with {
                Volume = volume,
                Pitch = -0.82f + pitchShift,
                MaxInstances = 3,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, pos);
            //dub：稍高的第二音，靠 DD2_MonkStaffGroundImpact 的短促质感
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = volume * 0.62f,
                Pitch = -0.55f + pitchShift,
                MaxInstances = 3,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, pos);
        }

        /// <summary>每帧衰减（渲染句柄驱动，仅客户端）</summary>
        public static void Update() {
            Pulse *= pulseDecay;
            if (Pulse < 0.01f) {
                Pulse = 0f;
            }
            ImpactFlash *= 0.90f;
            if (ImpactFlash < 0.01f) {
                ImpactFlash = 0f;
            }

            //脑消失后残留渐散
            if (++framesSincePush > 2) {
                ActiveThisFrame = false;
                Intensity *= 0.95f;
                Veil *= 0.96f;
                Blackout *= 0.94f;
                if (Intensity < 0.02f) {
                    Intensity = 0f;
                }
                if (Veil < 0.02f) {
                    Veil = 0f;
                }
                if (Blackout < 0.02f) {
                    Blackout = 0f;
                }
            }
        }

        /// <summary>卸载/世界切换清空</summary>
        public static void Clear() {
            Pulse = 0f;
            Intensity = 0f;
            Veil = 0f;
            Blackout = 0f;
            ImpactFlash = 0f;
            ActiveThisFrame = false;
        }
    }
}
