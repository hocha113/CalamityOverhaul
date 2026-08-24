using CalamityOverhaul.Common;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core
{
    /// <summary>全屏FX推送通道，客户端由状态代码写入，渲染句柄消费</summary>
    internal static class PlanteraScreenFX
    {
        internal const int MaxRings = 4;

        /// <summary>绽放花环实例</summary>
        internal struct RingInstance
        {
            public Vector2 WorldCenter;
            public float MaxRadiusPx;
            public int Age;
            public int Life;
            public bool Phase2;
            public bool Active;
        }

        internal static readonly RingInstance[] Rings = new RingInstance[MaxRings];

        /// <summary>冲击帧(白绿闪)，一场戏一次</summary>
        internal static float FlashIntensity { get; private set; }
        internal static int FlashAge { get; private set; }
        internal static int FlashLife { get; private set; }
        internal static Vector2 FlashWorldCenter { get; private set; }
        internal static bool FlashActive => FlashAge < FlashLife && FlashIntensity > 0.01f;

        /// <summary>丛林暮色罩(蓄力压暗)，每帧推高自动衰减</summary>
        internal static float DuskIntensity { get; private set; }

        public static bool AnyRingActive {
            get {
                for (int i = 0; i < MaxRings; i++) {
                    if (Rings[i].Active) {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool HasAny => FlashActive || DuskIntensity > 0.02f || AnyRingActive;

        /// <summary>推一圈绽放花环，超限顶替最老</summary>
        public static void PushRing(Vector2 worldCenter, float maxRadiusPx, bool phase2, int lifeFrames = 30) {
            if (VaultUtils.isServer) {
                return;
            }
            int slot = -1;
            int oldestAge = -1;
            for (int i = 0; i < MaxRings; i++) {
                if (!Rings[i].Active) {
                    slot = i;
                    break;
                }
                if (Rings[i].Age > oldestAge) {
                    oldestAge = Rings[i].Age;
                    slot = i;
                }
            }
            Rings[slot] = new RingInstance {
                WorldCenter = worldCenter,
                MaxRadiusPx = maxRadiusPx,
                Age = 0,
                Life = System.Math.Max(lifeFrames, 10),
                Phase2 = phase2,
                Active = true,
            };
        }

        /// <summary>推冲击帧</summary>
        public static void PushFlash(Vector2 worldCenter, float intensity = 1f, int lifeFrames = 14) {
            if (VaultUtils.isServer) {
                return;
            }
            FlashWorldCenter = worldCenter;
            FlashIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            FlashAge = 0;
            FlashLife = System.Math.Max(lifeFrames, 6);
        }

        /// <summary>每帧推暮色，取更高者</summary>
        public static void PushDusk(float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            DuskIntensity = MathHelper.Clamp(System.Math.Max(DuskIntensity, intensity), 0f, 0.85f);
        }

        /// <summary>渲染句柄每帧驱动衰减</summary>
        public static void Update() {
            if (FlashAge < FlashLife) {
                FlashAge++;
            }
            DuskIntensity *= 0.9f;
            if (DuskIntensity < 0.02f) {
                DuskIntensity = 0f;
            }
            for (int i = 0; i < MaxRings; i++) {
                if (!Rings[i].Active) {
                    continue;
                }
                Rings[i].Age++;
                if (Rings[i].Age >= Rings[i].Life) {
                    Rings[i].Active = false;
                }
            }
        }

        public static void Clear() {
            FlashIntensity = 0f;
            FlashAge = FlashLife = 0;
            DuskIntensity = 0f;
            for (int i = 0; i < MaxRings; i++) {
                Rings[i].Active = false;
            }
        }

        /// <summary>震屏，尊重配置</summary>
        public static void CameraPunch(Vector2 pos, float strength, int frames, string uniqueId, Vector2? dir = null) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 direction = dir ?? Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new(pos, direction, strength, 7f, frames, 2200f, uniqueId);
            Main.instance.CameraModifiers.Add(modifier);
        }
    }
}
