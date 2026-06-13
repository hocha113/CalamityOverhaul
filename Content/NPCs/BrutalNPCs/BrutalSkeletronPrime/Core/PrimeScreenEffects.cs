namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>机械骷髅王全屏 FX，仅客户端</summary>
    /// <para>三通道：冲击波环 <see cref="MaxRings"/>、冲击帧单实例、冲刺热浪</para>
    /// <para>Push* 写入；<see cref="Renders.PrimeScreenEffectRender"/> 调 <see cref="Update"/> 衰减</para>
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

        //冲击帧（黑白高对比，一场战斗只该触发一次）
        internal static float ImpactIntensity { get; private set; }
        internal static int ImpactAge { get; private set; }
        internal static int ImpactLife { get; private set; }
        internal static bool ImpactActive => ImpactAge < ImpactLife && ImpactIntensity > 0.01f;

        //冲刺热浪（状态每帧推高，渲染端自然衰减）
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

        /// <summary>触发一圈冲击波（折射环 + 色散），并发超限时顶替最老的环</summary>
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

        /// <summary>全屏冲击帧(负相→黑白→淡出)，死亡终爆专属，全场一次</summary>
        public static void PushImpactFrame(float intensity = 1f, int lifeFrames = 26) {
            if (VaultUtils.isServer) {
                return;
            }
            ImpactIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            ImpactAge = 0;
            ImpactLife = System.Math.Max(lifeFrames, 8);
        }

        /// <summary>冲刺状态每帧调用：推高热浪强度并刷新源位置与运动方向（弧度）</summary>
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

        /// <summary>世界切换/卸载时清空，防止残留效果跨场景闪现</summary>
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
