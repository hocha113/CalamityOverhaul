using System;
using Terraria;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>全景相机：鼠标牵引目标视角 + 指数平滑 + 怠速缓漂，输出视线基向量与柱面投影</summary>
    internal static class HimayoMenuCamera
    {
        /// <summary>垂直 FOV 半角正切，约 64°；视场稍宽可降低底图放大倍率，缓解发糊</summary>
        public const float TanHalfFov = 0.625f;

        /// <summary>全景底图经度压缩：底图为伪全景（中央按平面构图绘制），按不足 360° 解读收窄横向，
        /// 防人物拉宽；1.30 由灯笼/人脸比例的重投影校准得出</summary>
        public const float PanoLonScale = 1.30f;
        /// <summary>全景底图纬度压缩，绕赤道对称；校准中纵向无异常，维持 1</summary>
        public const float PanoLatScale = 1f;

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

        /// <summary>相机原点世界坐标 → UI 空间屏幕坐标，与 HimayoPanorama 的针孔透视完全一致，
        /// 保证转头时花瓣与背景流速相同（柱面等角试过一版：中央放大读作凸透镜，已回退）。
        /// depth=沿视轴深度，供透视缩放与景深；返回 false=视野外（仍应继续运动，只是不画）</summary>
        public static bool Project(Vector3 world, float alpha, out Vector2 screen, out float depth) {
            GetBasis(alpha, out Vector3 forward, out Vector3 right, out Vector3 up);
            float pF = Vector3.Dot(world, forward);
            float pR = Vector3.Dot(world, right);
            float pU = Vector3.Dot(world, up);
            depth = pF;
            screen = default;
            //视轴后方或过近直接出局
            if (pF < 0.08f) {
                return false;
            }
            float w = Main.screenWidth, h = Main.screenHeight;
            float ndcX = pR / pF / (TanHalfFov * (w / h));
            float ndcY = -(pU / pF) / TanHalfFov;
            screen = new Vector2((ndcX * 0.5f + 0.5f) * w, (ndcY * 0.5f + 0.5f) * h);
            //留边裕量吃掉花瓣自身尺寸
            return MathF.Abs(ndcX) < 1.18f && MathF.Abs(ndcY) < 1.30f;
        }

        /// <summary><see cref="Project"/> 的逆：UI 屏幕坐标 + 指定视轴深度 → 世界坐标（接瓣掌心用）</summary>
        public static Vector3 Unproject(Vector2 screen, float depth, float alpha) {
            GetBasis(alpha, out Vector3 forward, out Vector3 right, out Vector3 up);
            float w = Main.screenWidth, h = Main.screenHeight;
            float ndcX = screen.X / w * 2f - 1f;
            float ndcY = screen.Y / h * 2f - 1f;
            return (forward + right * (ndcX * TanHalfFov * (w / h)) - up * (ndcY * TanHalfFov)) * depth;
        }
    }
}
