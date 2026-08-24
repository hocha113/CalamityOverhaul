using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// TBUG 裂缝故障切片；轴对齐横条，终端蓝为主、少量报错品红（与裂缝报错色同族），
    /// 移动走离散步进而不是平滑漂移，读作"屏幕撕裂的碎渣"
    /// </summary>
    internal class PRT_TBUGGlitch : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 8000;

        private static readonly Color BodyBlue = new(0.28f, 0.62f, 1f);
        private static readonly Color EdgeBlue = new(0.09f, 0.24f, 0.50f);
        private static readonly Color BodyMagenta = new(1f, 0.24f, 0.46f);
        private static readonly Color EdgeMagenta = new(0.45f, 0.06f, 0.22f);

        private float initialScale;
        private float aspectRatio;
        private Color edgeColor;
        private float flickerPhase;
        /// <summary>缓存的漂移速度；Velocity 置零后自管理步进</summary>
        private Vector2 driftVel;
        private int stepInterval;
        private int stepTimer;

        public override bool CanPool => true;

        public PRT_TBUGGlitch() {
            Color = BodyBlue;
            edgeColor = EdgeBlue;
            aspectRatio = 3f;
            stepInterval = 3;
        }

        public PRT_TBUGGlitch Configure(int lt) {
            Lifetime = lt;
            initialScale = Scale;
            //Velocity 交给步进自管理，基类别再平滑积分
            driftVel = Velocity;
            Velocity = Vector2.Zero;

            bool magenta = Main.rand.NextFloat() < 0.12f;
            Color = magenta ? BodyMagenta : BodyBlue;
            edgeColor = magenta ? EdgeMagenta : EdgeBlue;

            Rotation = 0f;
            //横条为主，少量竖窄条
            aspectRatio = Main.rand.NextBool(4)
                ? Main.rand.NextFloat(0.25f, 0.5f)
                : Main.rand.NextFloat(2.5f, 6f);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            stepInterval = Main.rand.Next(2, 5);
            stepTimer = Main.rand.Next(stepInterval);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialScale = 0f;
            aspectRatio = 3f;
            Color = BodyBlue;
            edgeColor = EdgeBlue;
            flickerPhase = 0f;
            driftVel = Vector2.Zero;
            stepInterval = 3;
            stepTimer = 0;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float life = LifetimeCompletion;

            //离散步进：攒够间隔一次挪完，量化到 2px 网格
            stepTimer++;
            if (stepTimer >= stepInterval) {
                stepTimer = 0;
                Vector2 moved = Position + driftVel * stepInterval;
                Position = new Vector2(MathF.Round(moved.X * 0.5f) * 2f, MathF.Round(moved.Y * 0.5f) * 2f);
                //偶发横向重定位一格，撕裂错帧感
                if (Hash(Time * 0.7f + flickerPhase) > 0.86f) {
                    Position += new Vector2(Main.rand.NextBool() ? 6f : -6f, 0f);
                }
            }

            //后 40% 缩短
            if (life > 0.6f) {
                Scale = initialScale * (1f - MathF.Pow((life - 0.6f) / 0.4f, 1.5f));
            }

            //硬开关闪烁：要么亮要么近熄，没有中间态
            float blink = Hash(Time * 0.33f + flickerPhase) > 0.25f ? 1f : 0.12f;
            Opacity = blink * (1f - MathF.Pow(life, 3f));
        }

        private static float Hash(float p) {
            p = MathF.Abs(p * 0.1031f % 1f);
            p *= p + 33.33f;
            p *= p + p;
            return MathF.Abs(p % 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.01f) {
                return false;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            float w = 6f * Scale;
            float h = 6f * Scale * aspectRatio;
            Vector2 size = new(w, h);
            Vector2 origin = new(0.5f, 0.5f);

            //轴对齐三层：暗沿、主体、亮芯线
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), edgeColor * Opacity * 0.6f, 0f,
                origin, size * 1.5f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), Color * Opacity, 0f,
                origin, size, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1),
                new Color(0.78f, 0.92f, 1f) * Opacity * 0.7f, 0f,
                origin, new Vector2(w * 0.8f, h * 0.22f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
