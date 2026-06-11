using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering
{
    /// <summary>
    /// 逐体节充能波状态：以头部 whoAmI 为索引存储一条沿躯体传导的"能量波"，
    /// 体节绘制时按自身在蠕虫上的位置比例读取波强度并调制热感着色器，
    /// 形成"电流沿躯体奔跑"的可见充能波。纯视觉共享状态，各端独立推进，无需同步。
    /// </summary>
    internal static class DestroyerChargeWave
    {
        private struct WaveData
        {
            /// <summary>波峰位置（0=头部, 1=尾部）</summary>
            public float Phase;
            /// <summary>波峰宽度（体节比例单位）</summary>
            public float Width;
            /// <summary>波峰强度 0~1</summary>
            public float Intensity;
            /// <summary>整体均匀发光（全环闪烁用），为true时忽略Phase/Width</summary>
            public bool FullBody;
            /// <summary>最后一次推送的帧号，用于过期判断</summary>
            public uint LastPushFrame;
        }

        private static readonly WaveData[] waves = new WaveData[Main.maxNPCs];

        /// <summary>
        /// 每帧由状态推送当前波形（所有端都可调用，纯视觉）
        /// </summary>
        public static void Push(int controllerId, float phase, float width, float intensity, bool fullBody = false) {
            if (controllerId < 0 || controllerId >= waves.Length) {
                return;
            }
            waves[controllerId] = new WaveData {
                Phase = phase,
                Width = Math.Max(width, 0.01f),
                Intensity = MathHelper.Clamp(intensity, 0f, 1f),
                FullBody = fullBody,
                LastPushFrame = Main.GameUpdateCount
            };
        }

        /// <summary>
        /// 体节读取自身位置（0=头, 1=尾）处的波强度，未推送或已过期返回0
        /// </summary>
        public static float Read(int controllerId, float bodyFraction) {
            if (controllerId < 0 || controllerId >= waves.Length) {
                return 0f;
            }
            ref readonly WaveData wave = ref waves[controllerId];
            if (wave.Intensity <= 0.001f || Main.GameUpdateCount - wave.LastPushFrame > 4) {
                return 0f;
            }
            if (wave.FullBody) {
                return wave.Intensity;
            }

            float dist = Math.Abs(bodyFraction - wave.Phase);
            //高斯衰减的波峰
            float falloff = (float)Math.Exp(-(dist * dist) / (wave.Width * wave.Width) * 4f);
            return wave.Intensity * falloff;
        }
    }
}
