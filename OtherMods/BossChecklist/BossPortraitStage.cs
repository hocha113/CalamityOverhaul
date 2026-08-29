using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.OtherMods.BossChecklist
{
    /// <summary>
    /// customPortrait 一帧的舞台参数。演员在「虚拟场景坐标」（原点=画布中心）里模拟与绘制，
    /// <see cref="WorldMatrix"/> 负责把场景坐标映射进图鉴页画布（含 UIScale 与缩放）
    /// </summary>
    internal readonly struct PortraitFrame
    {
        /// <summary>画布（UI 空间，已避开 BossChecklist 叠画的标题区）</summary>
        public readonly Rectangle Canvas;
        /// <summary>场景坐标 → 屏幕的批次矩阵</summary>
        public readonly Matrix WorldMatrix;
        /// <summary>裁剪光栅态（演员中途重启批次时必须沿用，保持画布裁剪）</summary>
        public readonly RasterizerState Scissor;
        /// <summary>进度隐藏蒙版色（正常 White，隐藏 Black）</summary>
        public readonly Color Mask;
        /// <summary>剪影模式（蒙版近黑）：体色归黑、加色辉光层跳过</summary>
        public readonly bool Masked;
        /// <summary>画布在场景坐标下的可视半宽/半高（背景铺满用）</summary>
        public readonly Vector2 SceneHalf;

        public PortraitFrame(Rectangle canvas, Matrix worldMatrix, RasterizerState scissor,
            Color mask, bool masked, Vector2 sceneHalf) {
            Canvas = canvas;
            WorldMatrix = worldMatrix;
            Scissor = scissor;
            Mask = mask;
            Masked = masked;
            SceneHalf = sceneHalf;
        }

        /// <summary>体色蒙版乘算（剪影模式贴图形状保留、颜色归黑）</summary>
        public Color Tint(Color color) => Masked ? color.MultiplyRGB(Mask) : color;
    }

    /// <summary>
    /// 图鉴沙盒演员：一个 Boss 的实时演出（headless 模拟 + 绘制）。
    /// 只在图鉴选中页可见时被驱动，纯客户端表现，不碰任何世界/NPC 状态
    /// </summary>
    internal abstract class BossPortraitActor
    {
        /// <summary>场景半尺寸（场景坐标；舞台据此定缩放）</summary>
        public abstract Vector2 SceneHalfSize { get; }

        /// <summary>场景时钟（秒，重置归零）</summary>
        protected float Time { get; private set; }

        /// <summary>上次绘制墙钟毫秒（舞台步进与过期判定用）</summary>
        internal long LastDrawMs;

        internal void Step(float dt) {
            Time += dt;
            Update(dt);
        }

        internal void ResetScene() {
            Time = 0f;
            Reset();
        }

        /// <summary>推进一帧（dt 已限幅，秒）</summary>
        protected abstract void Update(float dt);

        /// <summary>场景重置（首次进入或翻页离开太久后重开演出）</summary>
        protected abstract void Reset();

        /// <summary>绘制：批次已按舞台矩阵开启，直接以场景坐标绘制</summary>
        public abstract void Draw(SpriteBatch sb, in PortraitFrame frame);
    }

    /// <summary>
    /// 图鉴头像沙盒舞台：接管 BossChecklist 移交的页面绘制权——
    /// 画布划定、裁剪、批次接管与恢复、进度隐藏蒙版、墙钟步进与过期重置。
    /// 暂停时动画照常呼吸（墙钟驱动）；只在图鉴页被绘制时产生开销
    /// </summary>
    internal static class BossPortraitStage
    {
        /// <summary>顶部预留：BossChecklist 会在页面上叠画 Boss 名、来源模组与右上头图标</summary>
        private const int TopReserve = 52;
        private const int EdgeInset = 8;
        /// <summary>单帧步进上限（秒）：低帧率下动画减速而不跳变</summary>
        private const float MaxDt = 1f / 30f;
        /// <summary>断绘重置阈值（毫秒）：翻页回来重新开演</summary>
        private const long StaleMs = 600;

        private static readonly RasterizerState scissorState = new() {
            CullMode = CullMode.None,
            ScissorTestEnable = true,
        };

        /// <summary>customPortrait 回调总入口</summary>
        public static void Draw(SpriteBatch sb, Rectangle pageRect, Color mask, BossPortraitActor actor) {
            if (Main.dedServ || actor == null || sb == null) {
                return;
            }
            Rectangle canvas = new(pageRect.X + EdgeInset, pageRect.Y + TopReserve,
                pageRect.Width - EdgeInset * 2, pageRect.Height - TopReserve - EdgeInset);
            if (canvas.Width < 60 || canvas.Height < 60) {
                return;
            }

            //墙钟步进：跨页/首帧断绘则重开演出
            long now = Environment.TickCount64;
            long gapMs = now - actor.LastDrawMs;
            actor.LastDrawMs = now;
            if (gapMs <= 0 || gapMs > StaleMs) {
                actor.ResetScene();
                actor.Step(1f / 60f);
            }
            else {
                actor.Step(MathF.Min(gapMs / 1000f, MaxDt));
            }

            Vector2 half = actor.SceneHalfSize;
            float zoom = MathF.Min(canvas.Width / (half.X * 2f), canvas.Height / (half.Y * 2f));
            if (zoom <= 0f) {
                return;
            }
            Vector2 center = canvas.Center.ToVector2();
            Matrix worldMatrix = Matrix.CreateScale(zoom, zoom, 1f)
                * Matrix.CreateTranslation(center.X, center.Y, 0f)
                * Main.UIScaleMatrix;

            bool masked = mask.R < 40 && mask.G < 40 && mask.B < 40;
            PortraitFrame frame = new(canvas, worldMatrix, scissorState, mask, masked,
                new Vector2(canvas.Width * 0.5f / zoom, canvas.Height * 0.5f / zoom));

            //接管批次：画布裁剪 + 场景矩阵；结束后恢复标准 UI 批次
            GraphicsDevice gd = sb.GraphicsDevice;
            Rectangle prevScissor = gd.ScissorRectangle;
            sb.End();
            gd.ScissorRectangle = UiToScreen(canvas, gd);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, worldMatrix);

            try {
                actor.Draw(sb, in frame);
            }
            finally {
                sb.End();
                gd.ScissorRectangle = prevScissor;
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            }
        }

        /// <summary>场景标准批次（演员从加色/着色器批切回时用）</summary>
        public static void BeginAlpha(SpriteBatch sb, in PortraitFrame frame) {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, frame.Scissor, null, frame.WorldMatrix);
        }

        /// <summary>加色批（辉光层；调用方负责先 End）</summary>
        public static void BeginAdditive(SpriteBatch sb, in PortraitFrame frame) {
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, frame.Scissor, null, frame.WorldMatrix);
        }

        /// <summary>着色器批（Immediate：允许逐 Draw 改参；调用方负责先 End）</summary>
        public static void BeginShader(SpriteBatch sb, in PortraitFrame frame, Effect effect) {
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, frame.Scissor, effect, frame.WorldMatrix);
        }

        /// <summary>UI 矩形 → 屏幕像素裁剪矩形（按 UIScale 变换并钳进视口）</summary>
        private static Rectangle UiToScreen(Rectangle ui, GraphicsDevice gd) {
            Vector2 tl = Vector2.Transform(new Vector2(ui.X, ui.Y), Main.UIScaleMatrix);
            Vector2 br = Vector2.Transform(new Vector2(ui.Right, ui.Bottom), Main.UIScaleMatrix);
            Rectangle rect = new((int)tl.X, (int)tl.Y,
                (int)MathF.Ceiling(br.X - tl.X), (int)MathF.Ceiling(br.Y - tl.Y));
            return Rectangle.Intersect(rect, gd.Viewport.Bounds);
        }
    }
}
