using CalamityOverhaul.Common;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core
{
    /// <summary>诅咒之幕全屏FX通道，客户端，Push*写入，Render 调 Update</summary>
    internal static class SkeletronScreenEffects
    {
        internal const int MaxRings = 2;

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

        /// <summary>黑暗领域强度 0~1（由状态观察驱动，缓动收敛）</summary>
        internal static float DomainIntensity { get; private set; }
        private static float domainTarget;

        /// <summary>骨白冲击帧（整场只在死亡终爆用一次）</summary>
        internal static float FlashIntensity { get; private set; }
        internal static int FlashAge { get; private set; }
        internal static int FlashLife { get; private set; }
        internal static bool FlashActive => FlashAge < FlashLife && FlashIntensity > 0.01f;

        public static bool HasAny {
            get {
                if (DomainIntensity > 0.012f || FlashActive) {
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

        /// <summary>本帧期望的领域强度（各观察者取最大值）</summary>
        public static void RequestDomain(float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            if (intensity > domainTarget) {
                domainTarget = MathHelper.Clamp(intensity, 0f, 1f);
            }
        }

        /// <summary>冲击波环，超限顶替最老</summary>
        public static void PushShockRing(Vector2 worldCenter, float intensity = 1f, float maxRadiusPx = 520f, int lifeFrames = 26) {
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

        /// <summary>骨白冲击帧，死亡终爆一次</summary>
        public static void PushBoneFlash(float intensity = 1f, int lifeFrames = 26) {
            if (VaultUtils.isServer) {
                return;
            }
            FlashIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            FlashAge = 0;
            FlashLife = System.Math.Max(lifeFrames, 8);
        }

        /// <summary>本地震屏，随距离衰减，尊重震屏设置</summary>
        public static void PushShake(Vector2 worldCenter, float intensity, float falloffPx = 1500f) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }
            float dist = player.Center.Distance(worldCenter);
            float atten = MathHelper.Clamp(1f - dist / falloffPx, 0f, 1f);
            if (atten <= 0.01f) {
                return;
            }
            player.CWR()?.GetScreenShake(intensity * atten);
        }

        /// <summary>每帧收敛（由渲染句柄驱动，仅客户端）</summary>
        public static void Update() {
            //领域强度向目标缓动，目标每帧由观察者重新申报
            DomainIntensity = MathHelper.Lerp(DomainIntensity, domainTarget, domainTarget > DomainIntensity ? 0.07f : 0.045f);
            if (DomainIntensity < 0.012f && domainTarget <= 0f) {
                DomainIntensity = 0f;
            }
            domainTarget = 0f;

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
        }

        /// <summary>卸载清空</summary>
        public static void Clear() {
            DomainIntensity = 0f;
            domainTarget = 0f;
            for (int i = 0; i < MaxRings; i++) {
                Rings[i].Active = false;
            }
            FlashIntensity = 0f;
            FlashAge = FlashLife = 0;
        }
    }
}
