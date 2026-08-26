using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 产出拍弹出的活凝胶团:半透胶体沿速度轴拉伸,落地弹性摊平渗入地面。
    /// Fog 真 alpha 承体,湿亮内芯走 A=0 加色技巧
    /// </summary>
    internal class PRT_FarmGelGlob : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 80;

        private bool mirror;
        private int landed;
        private Color initialColor;

        public PRT_FarmGelGlob Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            mirror = false;
            landed = 0;
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            mirror = Main.rand.NextBool();
            if (Lifetime <= 0) {
                Lifetime = 70;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            if (landed == 0) {
                Velocity.Y = MathF.Min(Velocity.Y + 0.30f, 9f);
                Velocity.X *= 0.99f;
                Rotation = Velocity.ToRotation();
                Opacity = MathF.Min(Time * 0.2f, 0.9f);
                if (Velocity.Y > 0f && Collision.SolidCollision(Position + new Vector2(-3f, 2f), 6, 6)) {
                    landed = 1;
                    Velocity = Vector2.Zero;
                    Rotation = 0f;
                    ai[0] = 0f;
                    //落地后剩余寿命固定,摊平节奏稳定
                    if (Lifetime - Time > 26) {
                        Lifetime = Time + 26;
                    }
                }
            }
            else {
                ai[0]++;
                Opacity *= 0.95f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float baseScale = Scale * 22f / tex.Width;
            Vector2 squish;
            if (landed == 0) {
                //飞行中沿速度轴拉伸,快则长慢则圆
                float v = Velocity.Length();
                squish = new Vector2(1f + v * 0.06f, MathHelper.Clamp(1f - v * 0.03f, 0.5f, 1f));
            }
            else {
                //弹性摊平:横向铺开纵向压缩,读作渗进地面
                squish = new Vector2(1f + ai[0] * 0.05f, MathF.Max(1f - ai[0] * 0.05f, 0.3f));
            }
            SpriteEffects fxs = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, drawPos, null, initialColor * (Opacity * 0.8f), Rotation, origin, baseScale * squish, fxs, 0f);
            //湿亮内芯
            spriteBatch.Draw(tex, drawPos, null, new Color(190, 255, 210, 0) * (Opacity * 0.35f),
                Rotation, origin, baseScale * squish * 0.55f, fxs, 0f);
            return false;
        }
    }
}
