using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 焦黑枯手的静态绘制函数库：全程 <c>VaultAsset.placeholder2</c> 像素矩形拼装（框架占位绘制同源技法），
    /// 零新纹理。掌=双层堆叠矩形，5 指各 3 节，爬行波浪相位差 i*0.9（副运动），
    /// 蜷缩=开合双姿态按曲线插值。<see cref="GhostHandActor"/> 与 <c>PRT_GhostGrasp</c> 共用
    /// </summary>
    internal static class GhostHandDrawHelper
    {
        /// <summary>焦炭主色</summary>
        public static readonly Color Charcoal = new(30, 26, 24);
        /// <summary>余烬橙（裂纹/眼色同源）</summary>
        public static readonly Color Ember = new(214, 92, 32);

        //五指个体差异:长度系数(食指到小指的弧)与掌前缘根位
        private static readonly float[] FingerLength = [0.82f, 1.00f, 1.10f, 0.98f, 0.78f];
        private static readonly float[] FingerRootY = [-13f, -6.5f, 0f, 6.5f, 13f];

        //开掌姿态:三节角度(弧度,相对+X 前向,正=向下弯),指尖拱向地面的爬行架
        private static readonly float[] OpenPose = [0.55f, 1.05f, 1.45f];
        //蜷缩姿态:指节向掌心收拢(向上再向后翻卷)
        private static readonly float[] CurlPose = [-0.85f, -2.05f, -2.60f];

        /// <summary>蜷缩缓动：poly(8) 急合——前段几乎不动，末拍猛然收拢（收势拍后的打击拍）</summary>
        public static float CurlEase(float t) => MathF.Pow(MathHelper.Clamp(t, 0f, 1f), 8f);

        /// <summary>
        /// 手体全装配绘制（屏幕空间，批次须已在世界变换下开好）。
        /// facing=指尖朝向（±1）；crawlPhase 驱动五指波浪；curl 0=张开爬行架、1=攥拳；
        /// emberGlow 余烬裂纹增亮（速度门控修饰）；flickerPhase 驱动裂纹明灭
        /// </summary>
        public static void DrawHand(SpriteBatch sb, Vector2 screenCenter, int facing, float crawlPhase,
            float curl, float alpha, float scale, float emberGlow, float flickerPhase) {
            if (alpha <= 0.004f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);
            float fx = facing >= 0 ? 1f : -1f;

            Color palmDark = Charcoal * (alpha * 0.96f);
            Color palmMid = new Color(46, 39, 34) * (alpha * 0.85f);

            //腕桩:掌后没入来处的残臂,朝后渐淡
            for (int i = 0; i < 3; i++) {
                Vector2 pos = screenCenter + new Vector2(fx * (-20f - i * 7f), 6f - i * 1.5f) * scale;
                float a = alpha * (0.55f - i * 0.16f);
                sb.Draw(pixel, pos, src, Charcoal * a, 0f, half, new Vector2(9f - i * 1.5f, 9f - i * 2f) * scale, SpriteEffects.None, 0f);
            }

            //掌:双层堆叠,蜷缩时整体略前倾
            float palmTilt = fx * curl * 0.18f;
            Vector2 palmCenter = screenCenter + new Vector2(fx * -6f, 3f) * scale;
            sb.Draw(pixel, palmCenter, src, palmDark, palmTilt, half, new Vector2(27f, 15f) * scale, SpriteEffects.None, 0f);
            sb.Draw(pixel, palmCenter + new Vector2(fx * 1f, -6f) * scale, src, palmMid, palmTilt, half, new Vector2(21f, 9f) * scale, SpriteEffects.None, 0f);

            //五指:根位沿掌前缘展开(俯视五指并排),各指三节链式装配
            for (int i = 0; i < 5; i++) {
                float wave = MathF.Sin(crawlPhase + i * 0.9f);
                Vector2 root = palmCenter + new Vector2(fx * (7f + MathF.Abs(FingerRootY[i]) * -0.12f), FingerRootY[i] * 0.9f) * scale;
                DrawFinger(sb, pixel, src, half, root, fx, wave, curl, FingerLength[i], alpha, scale);
            }

            //余烬裂纹:6 条掌面细线随 flickerPhase 明灭,速度门控增亮
            if (emberGlow > 0.01f) {
                for (int i = 0; i < 6; i++) {
                    float flick = 0.25f + 0.75f * MathF.Abs(MathF.Sin(flickerPhase * 2.1f + i * 1.73f));
                    float cx = -16f + i * 5.4f + MathF.Sin(i * 7.31f) * 2f;
                    float cy = MathF.Sin(i * 3.97f) * 5f + 2f;
                    float len = 5f + (i % 3) * 2.5f;
                    float ang = MathF.Sin(i * 5.11f) * 0.5f;
                    Vector2 pos = screenCenter + new Vector2(fx * cx, cy) * scale;
                    sb.Draw(pixel, pos, src, Ember * (alpha * emberGlow * flick * 0.85f), ang, half,
                        new Vector2(len, 1.4f) * scale, SpriteEffects.None, 0f);
                }
                //指根一点余烬芯:标记"活炭"而非死石
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Color core = Ember with { A = 0 };
                    sb.Draw(glow, palmCenter + new Vector2(fx * 6f, -2f) * scale, null,
                        core * (alpha * emberGlow * 0.30f), 0f, glow.Size() * 0.5f, 0.34f * scale, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>单指三节链式装配：开掌↔蜷缩姿态插值，wave 叠加爬行摆幅</summary>
        private static void DrawFinger(SpriteBatch sb, Texture2D pixel, Rectangle src, Vector2 half,
            Vector2 root, float fx, float wave, float curl, float lengthScale, float alpha, float scale) {
            Vector2 joint = root;
            float accumulated = 0f;
            for (int seg = 0; seg < 3; seg++) {
                //姿态插值 + 波浪只在张开时起效(攥紧的手不再走路)
                float open = OpenPose[seg] + wave * (0.16f + seg * 0.10f) * (1f - curl);
                float angle = MathHelper.Lerp(open, CurlPose[seg], curl);
                accumulated += angle;
                float segLen = (8.5f - seg * 1.6f) * lengthScale * scale;
                float thick = (3.6f - seg * 0.55f) * scale;
                float worldAngle = fx >= 0f ? accumulated : MathHelper.Pi - accumulated;
                Vector2 dir = worldAngle.ToRotationVector2();
                Vector2 segCenter = joint + dir * segLen * 0.5f;
                float shade = 0.92f - seg * 0.12f;
                sb.Draw(pixel, segCenter, src, Charcoal * (alpha * shade), worldAngle, half,
                    new Vector2(segLen, thick), SpriteEffects.None, 0f);
                joint += dir * segLen;
            }
        }
    }
}
