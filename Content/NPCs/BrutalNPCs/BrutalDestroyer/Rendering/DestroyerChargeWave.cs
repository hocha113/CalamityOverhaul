using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering
{
    /// <summary>充能波，头whoAmI索引，体节按比例读</summary>
    internal static class DestroyerChargeWave
    {
        private struct WaveData
        {
            /// <summary>波峰 0头1尾</summary>
            public float Phase;
            /// <summary>波峰宽(体节比例)</summary>
            public float Width;
            /// <summary>波峰强度 0~1</summary>
            public float Intensity;
            /// <summary>全环均匀发光，忽略Phase/Width</summary>
            public bool FullBody;
            /// <summary>末次推送帧</summary>
            public uint LastPushFrame;
        }

        private static readonly WaveData[] waves = new WaveData[Main.maxNPCs];

        /// <summary>每帧推送波形，纯视觉</summary>
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

        /// <summary>读体节波强，过期回0</summary>
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
            //高斯波峰
            float falloff = (float)Math.Exp(-(dist * dist) / (wave.Width * wave.Width) * 4f);
            return wave.Intensity * falloff;
        }
    }
}
