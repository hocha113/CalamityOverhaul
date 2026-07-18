using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>延伸枪托终端切割碎光：沿扫掠切线甩出的细长光屑，衰减时青/品红色散逐渐撕开</summary>
    internal class PRT_SHPCExtenderShred : BasePRT
    {
        public override string Texture => CWRConstant.Placeholder;
        public override int InGame_World_MaxCount => 2000;
        public override bool CanPool => true;

        //色散镶边固定为青/品红互补对，与 SHPCModExtenderCleave.fx 的边缘色散呼应
        private static readonly Color DispCyan = new(70, 240, 255);
        private static readonly Color DispMagenta = new(255, 90, 235);

        private Color edgeColor;
        private float initialScale;
        private Vector2 dispAxis;

        public PRT_SHPCExtenderShred Configure(Color edgeColor, int lifetime) {
            this.edgeColor = edgeColor;
            Lifetime = lifetime;
            initialScale = Scale;
            //色散偏移轴取初速度的垂线，飞行中两色沿该轴撕开
            dispAxis = Velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            return this;
        }

        public override void Reset() {
            base.Reset();
            edgeColor = default;
            initialScale = 0f;
            dispAxis = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.90f;
            if (Velocity.LengthSquared() > 0.01f) {
                Rotation = Velocity.ToRotation();
            }
            float life = LifetimeCompletion;
            Opacity = 1f - MathF.Pow(life, 1.8f);
            Scale = initialScale * (1f - life * 0.35f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f || Scale < 0.05f) return false;

            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f, 0.5f);

            //沿速度方向拉成细长光屑
            float len = MathHelper.Clamp(Velocity.Length() * 2.2f, 6f, 26f) * Scale;
            float thick = 2f * Scale;
            //色散随衰减撕开：青/品红沿垂直轴反向偏移
            Vector2 split = dispAxis * (LifetimeCompletion * 3.2f);

            spriteBatch.Draw(pixel, drawPos + split, src, DispCyan * Opacity * 0.5f, Rotation,
                origin, new Vector2(len, thick), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos - split, src, DispMagenta * Opacity * 0.5f, Rotation,
                origin, new Vector2(len, thick), SpriteEffects.None, 0f);
            //外层主题辉光
            spriteBatch.Draw(pixel, drawPos, src, edgeColor * Opacity * 0.55f, Rotation,
                origin, new Vector2(len * 1.15f, thick * 2.0f), SpriteEffects.None, 0f);
            //白亮芯线
            Color core = Color.Lerp(Color, Color.White, 0.55f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, src, core, Rotation,
                origin, new Vector2(len * 0.6f, thick * 0.6f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
