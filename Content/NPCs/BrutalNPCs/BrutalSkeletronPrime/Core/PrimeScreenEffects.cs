using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>机械骷髅王全屏后处理效果类型</summary>
    internal enum PrimeScreenEffectType
    {
        None = 0,
        ShockRing,
        ImpactFrame,
        HeatWake,
    }

    /// <summary>
    /// 机械骷髅王屏幕着色器效果运行时状态。
    /// 由头部状态/弹幕写入，<see cref="Content.Renders.PrimeScreenEffectRender"/> 消费。
    /// </summary>
    internal static class PrimeScreenEffects
    {
        public static PrimeScreenEffectType ActiveType { get; private set; }
        public static float Intensity { get; private set; }
        public static float Progress { get; private set; }
        public static Vector2 WorldCenter { get; private set; }
        public static float Radius { get; private set; }
        public static int RemainingFrames { get; private set; }

        public static void Push(PrimeScreenEffectType type, Vector2 worldCenter, float intensity, float progress, int frames, float radius = 480f) {
            ActiveType = type;
            WorldCenter = worldCenter;
            Intensity = MathHelper.Clamp(intensity, 0f, 1f);
            Progress = MathHelper.Clamp(progress, 0f, 1f);
            Radius = radius;
            RemainingFrames = System.Math.Max(frames, 1);
        }

        public static void PushShockRing(Vector2 worldCenter, float intensity, float progress, int frames = 18) {
            Push(PrimeScreenEffectType.ShockRing, worldCenter, intensity, progress, frames);
        }

        public static void PushImpactFrame(float intensity, int frames = 12) {
            Push(PrimeScreenEffectType.ImpactFrame, Vector2.Zero, intensity, 1f, frames);
        }

        public static void PushHeatWake(Vector2 worldCenter, float intensity, float progress, int frames = 6) {
            Push(PrimeScreenEffectType.HeatWake, worldCenter, intensity, progress, frames, 320f);
        }

        public static void Tick() {
            if (RemainingFrames <= 0) {
                ActiveType = PrimeScreenEffectType.None;
                return;
            }
            RemainingFrames--;
            if (RemainingFrames <= 0) {
                ActiveType = PrimeScreenEffectType.None;
                Intensity = 0f;
            }
        }

        public static bool HasActive => ActiveType != PrimeScreenEffectType.None && RemainingFrames > 0;
    }
}
