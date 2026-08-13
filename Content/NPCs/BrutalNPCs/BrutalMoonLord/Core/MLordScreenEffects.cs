namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core
{
    /// <summary>天体级全屏后效缓冲，客户端 Push 写入，渲染句柄消费衰减</summary>
    internal static class MLordScreenEffects
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

        //超新星虹膜（终爆一次性）
        internal static float NovaIntensity { get; private set; }
        internal static Vector2 NovaWorldCenter { get; private set; }
        internal static int NovaAge { get; private set; }
        internal static int NovaLife { get; private set; }
        internal static bool NovaActive => NovaAge < NovaLife && NovaIntensity > 0.01f;

        //光被吸走的引力昏暗（坍缩期持续推高）
        internal static float GravityDim { get; private set; }
        internal static Vector2 GravityDimCenter { get; private set; }

        public static bool HasAny {
            get {
                if (NovaActive || GravityDim > 0.02f) {
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

        /// <summary>星光冲击环，超限顶替最老</summary>
        public static void PushStarRing(Vector2 worldCenter, float intensity = 1f, float maxRadiusPx = 620f, int lifeFrames = 30) {
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

        /// <summary>超新星虹膜，一场一次的终爆</summary>
        public static void PushNova(Vector2 worldCenter, float intensity = 1f, int lifeFrames = 46) {
            if (VaultUtils.isServer) {
                return;
            }
            NovaWorldCenter = worldCenter;
            NovaIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            NovaAge = 0;
            NovaLife = System.Math.Max(lifeFrames, 10);
        }

        /// <summary>引力昏暗每帧推高（坍缩/大招充能）</summary>
        public static void PushGravityDim(Vector2 worldCenter, float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            GravityDimCenter = worldCenter;
            GravityDim = MathHelper.Clamp(System.Math.Max(GravityDim, intensity), 0f, 1f);
        }

        /// <summary>每帧衰减，渲染句柄驱动，仅客户端</summary>
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

            if (NovaAge < NovaLife) {
                NovaAge++;
            }

            GravityDim *= 0.9f;
            if (GravityDim < 0.02f) {
                GravityDim = 0f;
            }
        }

        /// <summary>卸载清空</summary>
        public static void Clear() {
            for (int i = 0; i < MaxRings; i++) {
                Rings[i].Active = false;
            }
            NovaIntensity = 0f;
            NovaAge = NovaLife = 0;
            GravityDim = 0f;
        }
    }
}
