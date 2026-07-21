namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>全屏FX，客户端，Push*写入，Render调Update</summary>
    internal static class PrimeScreenEffects
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

        //冲击帧
        internal static float ImpactIntensity { get; private set; }
        internal static int ImpactAge { get; private set; }
        internal static int ImpactLife { get; private set; }
        internal static bool ImpactActive => ImpactAge < ImpactLife && ImpactIntensity > 0.01f;

        //冲刺热浪
        internal static float HeatIntensity { get; private set; }
        internal static Vector2 HeatWorldCenter { get; private set; }
        internal static float HeatDirection { get; private set; }

        public static bool HasAny {
            get {
                if (ImpactActive || HeatIntensity > 0.03f) {
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

        /// <summary>冲击帧，终爆一次</summary>
        public static void PushImpactFrame(float intensity = 1f, int lifeFrames = 26) {
            if (VaultUtils.isServer) {
                return;
            }
            ImpactIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            ImpactAge = 0;
            ImpactLife = System.Math.Max(lifeFrames, 8);
        }

        /// <summary>冲刺热浪每帧推高</summary>
        public static void PushHeatWake(Vector2 worldCenter, float directionRadians, float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            HeatWorldCenter = worldCenter;
            HeatDirection = directionRadians;
            HeatIntensity = MathHelper.Clamp(System.Math.Max(HeatIntensity, intensity), 0f, 1f);
        }

        /// <summary>每帧衰减（由渲染句柄驱动，仅客户端）</summary>
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

            if (ImpactAge < ImpactLife) {
                ImpactAge++;
            }

            HeatIntensity *= 0.86f;
            if (HeatIntensity < 0.03f) {
                HeatIntensity = 0f;
            }
        }

        /// <summary>卸载清空</summary>
        public static void Clear() {
            for (int i = 0; i < MaxRings; i++) {
                Rings[i].Active = false;
            }
            ImpactIntensity = 0f;
            ImpactAge = ImpactLife = 0;
            HeatIntensity = 0f;
        }
    }
}
