using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 双屏换乘:门点击后接管一场约 0.75s 的定向转场——
    /// 墨扫自行进方向扫入盖屏(OniInkWipe.fx,缺则 CPU 墨带),盖到两成半时开新屏(旧屏互斥静默收),
    /// 揭开时新屏自带的落定编舞(刀落定/卷展开)正好接棒;
    /// 两屏内容随 <see cref="SlideOf"/> 横滑让出"移步"感,顶梁与门挂物不滑(同一夜屋的骨架)
    /// </summary>
    internal sealed class OniLedgerSwapFX : UIHandle
    {
        public static OniLedgerSwapFX Instance => UIHandleLoader.GetUIHandleOfType<OniLedgerSwapFX>();

        /// <summary>盖在两屏之上,让位于教程层与悬浮说明(10)</summary>
        public override float RenderPriority => 6f;
        public override Vector2 MousePosition => OnikiriUITheme.UIMouse;
        public override bool Active => running;

        private const float TotalFrames = 46f;
        /// <summary>行进到此比例时开新屏(旧屏被互斥静默收台)</summary>
        private const float OpenAt = 0.24f;

        private static bool running;
        private static float progress;
        private static float travelDir;
        private static OniLedgerView target;
        private static bool opened;
        private static float seed;

        /// <summary>换乘进行中(期间两门与两屏交互挂起)</summary>
        public static bool Running => running;

        /// <summary>发起换乘;to=目的驿站。东去改铭台,西回点鬼簿</summary>
        public static void Begin(OniLedgerView to) {
            if (running) {
                return;
            }
            running = true;
            progress = 0f;
            opened = false;
            target = to;
            travelDir = to == OniLedgerView.Mei ? 1f : -1f;
            seed = Main.rand.NextFloat(20f);
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.15f, Volume = 0.3f, MaxInstances = 1 });
        }

        private static void OpenTarget() {
            opened = true;
            UIHandle incoming = target == OniLedgerView.Mei
                ? OniMeiUI.Instance
                : OniRegisterUI.Instance;
            incoming?.Open();
        }

        /// <summary>
        /// 本帧 view 屏的内容横滑量:去屏向行进反向让开,来屏自行进方向滑入落位。
        /// 各屏在 LayoutCompute 里把它加进主体坐标(顶梁/门挂物不加)
        /// </summary>
        public static float SlideOf(OniLedgerView view) {
            if (!running) {
                return 0f;
            }
            if (view == target) {
                float t = MathHelper.Clamp((progress - 0.40f) / 0.60f, 0f, 1f);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                return travelDir * 64f * (1f - ease);
            }
            float tOut = MathHelper.Clamp(progress / 0.5f, 0f, 1f);
            return -travelDir * 46f * tOut * tOut;
        }

        public override void LogicUpdate() {
            if (!running) {
                return;
            }
            progress += 1f / TotalFrames;
            if (!opened && progress >= OpenAt) {
                OpenTarget();
            }
            if (progress >= 1f) {
                //兜底:万一 OpenAt 窗口因异常跳过,结束前确保目的屏已开
                if (!opened) {
                    OpenTarget();
                }
                running = false;
                progress = 0f;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!running) {
                return;
            }
            float sw = OnikiriUITheme.UIScreenW;
            float sh = OnikiriUITheme.UIScreenH;
            Effect effect = EffectLoader.OniInkWipe?.Value;
            if (effect != null) {
                Rectangle full = new(-2, -2, (int)sw + 4, (int)sh + 4);
                effect.Parameters["uTime"]?.SetValue(GlobalTimer);
                effect.Parameters["uAlpha"]?.SetValue(1f);
                effect.Parameters["uProgress"]?.SetValue(progress);
                effect.Parameters["uDir"]?.SetValue(travelDir);
                effect.Parameters["uSeed"]?.SetValue(seed);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(full.Width, full.Height));
                effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
                effect.Parameters["uColDark"]?.SetValue(OnikiriUITheme.Dark.ToVector3());
                effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
                effect.Parameters["uColBright"]?.SetValue(OnikiriUITheme.Bright.ToVector3());
                effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                spriteBatch.Draw(VaultAsset.placeholder2.Value, full, new Rectangle(0, 0, 1, 1), Color.White);
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
                return;
            }
            DrawFallbackWipe(spriteBatch, sw, sh);
        }

        /// <summary>CPU 降级墨扫:实墨带+前后沿羽化条+笔锋亮线,读得出方向即可</summary>
        private void DrawFallbackWipe(SpriteBatch sb, float sw, float sh) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float cover = MathHelper.Clamp(progress / 0.45f, 0f, 1f);
            cover = cover * cover * (3f - 2f * cover);
            float reveal = MathHelper.Clamp((progress - 0.52f) / 0.48f, 0f, 1f);
            reveal = reveal * reveal * (3f - 2f * reveal);

            //s=0 在入边(行进方向那一侧):dir=+1 自东扫入
            float FrontX(float s) => travelDir > 0f ? sw * (1f - s) : sw * s;
            float coverX = FrontX(cover * 1.04f);
            float revealX = FrontX(reveal * 1.04f);
            float left = Math.Min(coverX, revealX);
            float right = Math.Max(coverX, revealX);
            if (right - left > 1f) {
                sb.Draw(pixel, new Rectangle((int)left, -2, (int)(right - left), (int)sh + 4), src,
                    OnikiriUITheme.Ink * 0.97f);
            }
            //两道前沿羽化与笔锋
            for (int i = 0; i < 4; i++) {
                float w = 10f + i * 12f;
                float fa = 0.5f - i * 0.11f;
                float fx = coverX + (travelDir > 0f ? -w * 0.5f : w * 0.5f);
                sb.Draw(pixel, new Vector2(fx, sh * 0.5f), src, OnikiriUITheme.Ink * (fa * 0.9f),
                    0f, new Vector2(0.5f), new Vector2(w, sh + 4f), SpriteEffects.None, 0f);
            }
            if (cover > 0.01f && cover < 0.999f) {
                sb.Draw(pixel, new Vector2(coverX, sh * 0.5f), src, OnikiriUITheme.Bright * 0.55f,
                    0f, new Vector2(0.5f), new Vector2(2.6f, sh + 4f), SpriteEffects.None, 0f);
                sb.Draw(pixel, new Vector2(coverX, sh * 0.5f), src, OnikiriUITheme.HotWhite * 0.35f,
                    0f, new Vector2(0.5f), new Vector2(1.2f, sh + 4f), SpriteEffects.None, 0f);
            }
            if (reveal > 0.01f && reveal < 0.999f) {
                sb.Draw(pixel, new Vector2(revealX, sh * 0.5f), src, OnikiriUITheme.Deep * 0.5f,
                    0f, new Vector2(0.5f), new Vector2(2f, sh + 4f), SpriteEffects.None, 0f);
            }
        }
    }
}
