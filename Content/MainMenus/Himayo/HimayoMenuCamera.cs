using System;
using Terraria;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>全景相机：鼠标牵引目标视角 + 指数平滑 + 怠速缓漂，输出视线基向量与层视差偏移</summary>
    internal static class HimayoMenuCamera
    {
        /// <summary>垂直 FOV 半角正切，约 64°；视场稍宽可降低底图放大倍率，缓解发糊</summary>
        public const float TanHalfFov = 0.625f;

        //鼠标可牵引的最大偏航/俯仰（弧度）
        private const float MouseYawRange = 0.489f;
        private const float MousePitchRange = 0.175f;
        //怠速漂移幅度与总俯仰夹角（远离等距柱状图极区拉伸）
        private const float DriftYawAmp = 0.105f;
        private const float DriftPitchAmp = 0.030f;
        private const float PitchClamp = 0.279f;
        //每 tick 追随率
        private const float Follow = 0.055f;

        private static float yaw, pitch, prevYaw, prevPitch;
        private static float driftTime;

        public static void Reset() {
            yaw = pitch = prevYaw = prevPitch = 0f;
            driftTime = 0f;
        }

        /// <summary>固定 60tick 推进一步（菜单绘制路径内 mouseX/screenWidth 均为 UI 空间且成对一致）</summary>
        public static void Tick() {
            prevYaw = yaw;
            prevPitch = pitch;
            driftTime += 1f / 60f;

            //失焦时鼠标值不可靠，只保留怠速漂移分量
            float nx = 0f, ny = 0f;
            if (Main.hasFocus && Main.screenWidth > 0 && Main.screenHeight > 0) {
                nx = MathHelper.Clamp(Main.mouseX / (float)Main.screenWidth * 2f - 1f, -1f, 1f);
                ny = MathHelper.Clamp(Main.mouseY / (float)Main.screenHeight * 2f - 1f, -1f, 1f);
            }
            float targetYaw = nx * MouseYawRange
                + MathF.Sin(driftTime * MathHelper.TwoPi / 31f) * DriftYawAmp;
            float targetPitch = -ny * MousePitchRange
                + MathF.Sin(driftTime * MathHelper.TwoPi / 47f) * DriftPitchAmp;
            targetPitch = MathHelper.Clamp(targetPitch, -PitchClamp, PitchClamp);

            yaw += (targetYaw - yaw) * Follow;
            pitch += (targetPitch - pitch) * Follow;
        }

        public static float LerpYaw(float alpha) => MathHelper.Lerp(prevYaw, yaw, alpha);
        public static float LerpPitch(float alpha) => MathHelper.Lerp(prevPitch, pitch, alpha);

        /// <summary>视线正交基，供全景着色器；yaw=0 pitch=0 时朝向 equirect 图水平中心</summary>
        public static void GetBasis(float alpha, out Vector3 forward, out Vector3 right, out Vector3 up) {
            float y = LerpYaw(alpha), p = LerpPitch(alpha);
            float cp = MathF.Cos(p), sp = MathF.Sin(p);
            float cy = MathF.Cos(y), sy = MathF.Sin(y);
            forward = new Vector3(sy * cp, sp, cy * cp);
            right = new Vector3(cy, 0f, -sy);
            up = Vector3.Cross(forward, right);
        }

        /// <summary>层视差：相机右转画面内容左移，factor 近大远小，返回 UI 空间像素偏移</summary>
        public static Vector2 ParallaxOffset(float alpha, float factor) {
            return new Vector2(-LerpYaw(alpha) * 240f, LerpPitch(alpha) * 170f) * factor;
        }
    }
}
