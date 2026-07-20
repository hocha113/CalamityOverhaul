using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>速射枪管灼热火渣：沿速度拉伸的炽热碎屑，横向阻尼+热浮上升，色温由白炽冷却到暗红熄灭</summary>
    internal class PRT_SHPCRedlineCinder : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 2000;

        private Color hotColor;
        private Color coolColor;
        private float initialScale;
        private bool buoyant;

        public override bool CanPool => true;

        public PRT_SHPCRedlineCinder Configure(Color coolColor, int lifeTime, bool buoyant = true) {
            this.coolColor = coolColor;
            this.buoyant = buoyant;
            hotColor = Color;
            Lifetime = lifeTime;
            initialScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            hotColor = default;
            coolColor = default;
            initialScale = 0f;
            buoyant = false;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity.X *= 0.93f;
            if (buoyant) {
                //蒸腾热浮：横向衰减后缓慢上飘，模拟被热流卷起的火渣
                Velocity.Y = MathF.Max(Velocity.Y - 0.05f, -2.2f);
            }
            else {
                Velocity.Y *= 0.93f;
            }

            float life = LifetimeCompletion;
            Color = Color.Lerp(hotColor, coolColor, MathF.Pow(life, 1.3f));
            Scale = initialScale * (1f - MathF.Pow(life, 2.4f));
            Opacity = 1f - MathF.Pow(life, 2.2f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = new(0.5f, 0.5f);
            float speed = Velocity.Length();
            //沿速度方向拉伸成短焰条，静止时回落为方点
            float stretch = 1f + MathF.Min(speed * 0.45f, 3.2f);
            float rot = speed > 0.2f ? Velocity.ToRotation() : 0f;
            float w = 3.0f * Scale;

            //外层热晕
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), Color * (Opacity * 0.32f),
                rot, origin, new Vector2(w * stretch * 1.8f, w * 2.2f), SpriteEffects.None, 0f);
            //炽核
            Color core = Color.Lerp(Color, Color.White, 0.4f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), core,
                rot, origin, new Vector2(w * stretch, w * 0.8f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
