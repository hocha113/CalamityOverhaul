using CalamityOverhaul.Common;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core
{
    /// <summary>石巨人全屏FX状态，客户端 Push* 写入，渲染层消费</summary>
    internal static class GolemScreenEffects
    {
        internal const int MaxRings = 3;

        internal struct RingInstance
        {
            public Vector2 WorldCenter;
            public float Intensity;
            public float MaxRadiusPx;
            public int Age;
            public int Life;
            public bool Active;
        }

        internal static readonly RingInstance[] Rings = new RingInstance[MaxRings];

        //太阳白闪（大招终爆/宝石谢幕）
        internal static float FlashIntensity { get; private set; }
        internal static Vector2 FlashWorldCenter { get; private set; }
        internal static int FlashAge { get; private set; }
        internal static int FlashLife { get; private set; }
        internal static bool FlashActive => FlashAge < FlashLife && FlashIntensity > 0.01f;

        //冲击帧（一场一次）
        internal static float ImpactIntensity { get; private set; }
        internal static int ImpactAge { get; private set; }
        internal static int ImpactLife { get; private set; }
        internal static bool ImpactActive => ImpactAge < ImpactLife && ImpactIntensity > 0.01f;

        public static bool HasAny {
            get {
                if (FlashActive || ImpactActive) {
                    return true;
                }
                for (int i = 0; i < MaxRings; i++) {
                    if (Rings[i].Active) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>震屏（尊重配置开关）</summary>
        public static void Shake(float intensity) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(intensity);
        }

        /// <summary>冲击波环，超限顶替最老</summary>
        public static void PushShockRing(Vector2 worldCenter, float intensity = 1f, float maxRadiusPx = 560f, int lifeFrames = 26) {
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
                Intensity = MathHelper.Clamp(intensity, 0f, 1.2f),
                MaxRadiusPx = maxRadiusPx,
                Age = 0,
                Life = System.Math.Max(lifeFrames, 8),
                Active = true,
            };
        }

        /// <summary>太阳白闪</summary>
        public static void PushSunFlash(Vector2 worldCenter, float intensity = 1f, int lifeFrames = 30) {
            if (VaultUtils.isServer) {
                return;
            }
            FlashWorldCenter = worldCenter;
            FlashIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            FlashAge = 0;
            FlashLife = System.Math.Max(lifeFrames, 8);
        }

        /// <summary>冲击帧，一场只给终爆</summary>
        public static void PushImpactFrame(float intensity = 1f, int lifeFrames = 14) {
            if (VaultUtils.isServer) {
                return;
            }
            ImpactIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            ImpactAge = 0;
            ImpactLife = System.Math.Max(lifeFrames, 8);
        }

        /// <summary>每帧衰减，由渲染句柄驱动（仅客户端）</summary>
        public static void Update() {
            for (int i = 0; i < MaxRings; i++) {
                if (!Rings[i].Active) {
                    continue;
                }
                Rings[i].Age++;
                if (Rings[i].Age >= Rings[i].Life) {
                    Rings[i].Active = false;
                }
            }

            if (FlashAge < FlashLife) {
                FlashAge++;
            }
            if (ImpactAge < ImpactLife) {
                ImpactAge++;
            }
        }

        /// <summary>卸载清空</summary>
        public static void Clear() {
            for (int i = 0; i < MaxRings; i++) {
                Rings[i].Active = false;
            }
            FlashIntensity = 0f;
            FlashAge = FlashLife = 0;
            ImpactIntensity = 0f;
            ImpactAge = ImpactLife = 0;
        }
    }
}
