using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>重型枪管凿击剥片：白热金属碎屑受重力坠落，由炽白冷却为暗铁</summary>
    internal class PRT_SHPCHeavySpall : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 3000;
        public override bool CanPool => true;

        private Color hotColor;
        private Color coolColor;
        private float initialScale;
        private float gravity;

        public PRT_SHPCHeavySpall Configure(Color coolColor, int lifetime, float gravity = 0.24f) {
            hotColor = Color;
            this.coolColor = coolColor;
            Lifetime = lifetime;
            this.gravity = gravity;
            initialScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            hotColor = default;
            coolColor = default;
            initialScale = 0f;
            gravity = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity.X *= 0.97f;
            Velocity.Y += gravity;
            Rotation = Velocity.ToRotation();

            float life = LifetimeCompletion;
            //冷却曲线：前段保持白热，中后段迅速转向暗铁色
            Color = Color.Lerp(hotColor, coolColor, MathF.Pow(life, 1.3f));
            Opacity = 1f - MathF.Pow(life, 2.2f);
            //末段缩小，模拟碎屑熄灭
            Scale = life > 0.7f ? initialScale * (1f - (life - 0.7f) / 0.3f * 0.6f) : initialScale;

            if (life < 0.5f) {
                Lighting.AddLight(Position, Color.ToVector3() * 0.28f * Opacity);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f || Scale < 0.05f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f, 0.5f);

            //剥片沿速度方向拉长成炽热飞屑，速度越快拖尾越长
            float speedLen = MathHelper.Clamp(Velocity.Length() * 1.4f, 4f, 22f) * Scale;
            float thick = 2.4f * Scale;

            //外层余温辉光
            spriteBatch.Draw(pixel, drawPos, src, coolColor * Opacity * 0.45f, Rotation,
                origin, new Vector2(speedLen * 1.25f, thick * 2.2f), SpriteEffects.None, 0f);
            //中层当前温度
            spriteBatch.Draw(pixel, drawPos, src, Color * Opacity, Rotation,
                origin, new Vector2(speedLen, thick), SpriteEffects.None, 0f);
            //白热芯线
            Color core = Color.Lerp(Color, Color.White, 0.65f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, src, core, Rotation,
                origin, new Vector2(speedLen * 0.55f, thick * 0.45f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
