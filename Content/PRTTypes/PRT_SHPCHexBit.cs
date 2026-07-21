using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>六边形轮廓飞屑，多格机匣</summary>
    internal class PRT_SHPCHexBit : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 2000;

        private const int SegmentCount = 6;

        private float initialScale;
        private float rotationSpeed;
        private Color edgeColor;
        private float flickerPhase;

        public override bool CanPool => true;

        public void Configure(Color edgeColor, int lifeTime) {
            this.edgeColor = edgeColor;
            Lifetime = lifeTime;
            initialScale = Scale;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            rotationSpeed = Main.rand.NextFloat(0.03f, 0.10f) * (Main.rand.NextBool() ? 1f : -1f);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            initialScale = 0f;
            rotationSpeed = 0f;
            edgeColor = default;
            flickerPhase = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.94f;
            Rotation += rotationSpeed;
            float life = LifetimeCompletion;
            //前胀后收
            Scale = initialScale * (life < 0.25f
                ? MathHelper.Lerp(0.5f, 1f, life / 0.25f)
                : 1f - MathF.Pow((life - 0.25f) / 0.75f, 2f));
            float flicker = 0.75f + 0.25f * MathF.Sin(Time * 0.9f + flickerPhase);
            Opacity = flicker * (1f - MathF.Pow(life, 2.5f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            float radius = 9f * Scale;
            float sideLen = radius; //正六边形边长=外接圆半径
            Color inner = Color * Opacity;
            Color outer = edgeColor * Opacity * 0.5f;
            for (int i = 0; i < SegmentCount; i++) {
                float midAngle = Rotation + MathHelper.TwoPi * (i + 0.5f) / SegmentCount;
                Vector2 mid = drawPos + midAngle.ToRotationVector2() * (radius * 0.866f);
                float segRot = midAngle + MathHelper.PiOver2;
                spriteBatch.Draw(pixel, mid, new Rectangle(0, 0, 1, 1), outer, segRot,
                    new Vector2(0.5f, 0.5f), new Vector2(sideLen * 1.15f, 2.4f), SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, mid, new Rectangle(0, 0, 1, 1), inner, segRot,
                    new Vector2(0.5f, 0.5f), new Vector2(sideLen, 1.2f), SpriteEffects.None, 0f);
            }
            Color core = Color.Lerp(inner, Color.White, 0.5f) * (Opacity * 0.6f);
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), core, Rotation,
                new Vector2(0.5f, 0.5f), new Vector2(3f * Scale, 3f * Scale), SpriteEffects.None, 0f);

            return false;
        }
    }
}
