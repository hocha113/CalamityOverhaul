using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates
{
    /// <summary>
    /// 泼墨墨滴：罡气爆发甩出的暗墨圆滴，抛物坠落、沿速度方向微拉长。<br/>
    /// 与 <see cref="OniFinaleSlashs.PRT_OniShard"/> 的加色晶片不同，墨滴走
    /// AlphaBlend 染暗色（加色混合画不了黑），读作实体的墨点而非光屑
    /// </summary>
    internal class PRT_OniInkDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private Color initialColor;

        public PRT_OniInkDrop Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            if (Lifetime <= 0) {
                Lifetime = 26;
            }
        }

        public override void AI() {
            Velocity.X *= 0.97f;
            Velocity.Y += 0.35f;   //墨滴坠落
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Scale *= 0.985f;

            float t = LifetimeCompletion;
            //快进快出：出生 2 帧内实体化，末 35% 洇散淡出
            Opacity = MathF.Min(t / 0.08f, 1f) * (1f - SmoothStep01((t - 0.65f) / 0.35f));
            Color = initialColor;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            Rectangle frame = new(index % 2 * 512, index / 2 * 512, 512, 512);
            //沿速度方向微拉长的小墨点（帧 512px，0.16 基准缩到几十像素）
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 1f, 1.7f);
            Vector2 scale = new Vector2(0.80f, stretch) * Scale * 0.16f;
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
