using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>一具鬼奴亡躯的材质参数（KikasaServantGhost.fx 的 C# 侧镜像）</summary>
    internal struct KikasaGhostParams
    {
        /// <summary>实例随机相位</summary>
        internal float Seed;

        /// <summary>1=全液态血躯 0=落定鬼躯</summary>
        internal float Form;

        /// <summary>0=完好 1=蚀尽</summary>
        internal float Dissolve;

        /// <summary>1=出水纵扫 0=噪声斑驳</summary>
        internal float ScanMode;

        /// <summary>0..1 下缘液化强度（石壳低、软体高）</summary>
        internal float Liquefy;

        /// <summary>0..1 记忆脉冲强度（事件驱动，静止 0）</summary>
        internal float Pulse;

        /// <summary>0..1 脉冲带扫过进度（0=底 1=顶）</summary>
        internal float PulsePhase;

        /// <summary>0..1 原色残留（亮部保一丝本来的颜色）</summary>
        internal float Memory;
    }

    /// <summary>
    /// 鬼奴亡躯的共用绘制辅助：统一设置 KikasaServantGhost.fx 参数，
    /// 并按「衬边契约」绘制——本体 quad 四周外扩透明衬边，血丝与折射晃动画在衬边里；
    /// shader 以 uUvRect 认真帧、帧外一律视作空像素，衬边越入相邻动画帧也无害。
    /// SpriteBatch 的 Immediate 批开合仍由调用方自管（与旧 KikasaItemForm 用法一致）
    /// </summary>
    internal static class KikasaServantGhostDraw
    {
        //衬边比例（相对帧尺寸）：侧=折射晃动余量，下=血丝伸展区，上=少量防裁
        private const float PadSideRatio = 0.12f;
        private const float PadTopRatio = 0.08f;
        private const float PadBottomRatio = 0.26f;

        /// <summary>shader 与噪声贴图是否就绪；未就绪调用方走 CPU 血染回退</summary>
        internal static bool Ready => EffectLoader.KikasaServantGhost?.Value != null
            && CWRAsset.PerlinNoise?.Value != null;

        /// <summary>
        /// 应用亡躯材质：绑噪声、全参数显式设置、Apply 首个 pass。
        /// 须在 Immediate 批内调用，随后的本体绘制走 <see cref="DrawPadded"/> 拿衬边
        /// </summary>
        internal static void Apply(Texture2D tex, Rectangle frame, in KikasaGhostParams p) {
            Effect form = EffectLoader.KikasaServantGhost.Value;
            Main.instance.GraphicsDevice.Textures[1] = CWRAsset.PerlinNoise.Value;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            form.Parameters["uSeed"]?.SetValue(p.Seed);
            form.Parameters["uForm"]?.SetValue(p.Form);
            form.Parameters["uDissolve"]?.SetValue(p.Dissolve);
            form.Parameters["uScanMode"]?.SetValue(p.ScanMode);
            form.Parameters["uLiquefy"]?.SetValue(p.Liquefy);
            form.Parameters["uPulse"]?.SetValue(p.Pulse);
            form.Parameters["uPulsePhase"]?.SetValue(p.PulsePhase);
            form.Parameters["uMemory"]?.SetValue(p.Memory);
            form.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
            form.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>
        /// 衬边绘制：drawPos/origin/scale 语义与直接 sb.Draw(tex, pos, frame, ...) 完全一致，
        /// 只是 quad 实际外扩了衬边。源矩形越出纹理边界无妨（越界 uv 被 shader 帧界门归零）。
        /// FlipVertically 未适配——衬边假设帧空间向下为「下」
        /// </summary>
        internal static void DrawPadded(SpriteBatch sb, Texture2D tex, Rectangle frame,
            Vector2 drawPos, Color color, float rotation, Vector2 origin, Vector2 scale,
            SpriteEffects effects) {
            int padX = (int)MathF.Ceiling(frame.Width * PadSideRatio);
            int padTop = (int)MathF.Ceiling(frame.Height * PadTopRatio);
            int padBot = (int)MathF.Ceiling(frame.Height * PadBottomRatio);
            Rectangle padded = new(frame.X - padX, frame.Y - padTop,
                frame.Width + padX * 2, frame.Height + padTop + padBot);
            sb.Draw(tex, drawPos, padded, color, rotation,
                origin + new Vector2(padX, padTop), scale, effects, 0f);
        }
    }
}
