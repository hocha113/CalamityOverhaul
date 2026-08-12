using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼奴史莱姆皇后的血晶碎屑：细长晶棱翻滚坠落，
    /// 新鲜期晶面偶发锐利闪光（失泽后不再闪），随寿命凝暗、尾段陡淡。
    /// 晶格雷碎裂、晶片弹命中、俯冲晶爆与溶解失泽共用
    /// </summary>
    internal class PRT_KikasaQueenSlimeFacet : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 260;

        private Color initialColor;
        private float gravity;
        private float spin;
        private float glintSeed;
        /// <summary>光泽系数：1=新鲜带晶面反光，0=失泽哑光（溶解演出传低值）</summary>
        private float luster;

        public PRT_KikasaQueenSlimeFacet Configure(int lifetime, float gravityPerFrame = 0.2f, float spinSpeed = 0.09f, float lusterK = 1f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            spin = spinSpeed;
            luster = lusterK;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            spin = 0f;
            glintSeed = 0f;
            luster = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            glintSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 26;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= 0.985f;
            Velocity.Y = MathF.Min(Velocity.Y + gravity, 13f);
            spin *= 0.985f;
            Rotation += spin;

            float t = LifetimeCompletion;
            //晶体不融不缩，只凝暗：先失光泽再沉入暗血色
            Color = Color.Lerp(initialColor, new Color(52, 14, 24), MathF.Pow(t, 1.5f) * 0.72f);
            Opacity = 1f - MathF.Pow(t, 2.8f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float t = LifetimeCompletion;

            Color body = Color * Opacity;
            Color rim = Color.Lerp(Color, new Color(40, 10, 20), 0.6f) * Opacity;

            //细长晶棱：暗缘略宽给厚度，亮芯极窄读出硬质折面
            Vector2 sliver = new Vector2(0.17f, 0.5f) * Scale;
            spriteBatch.Draw(tex, pos, null, rim, Rotation, origin, sliver * new Vector2(1.4f, 1.05f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, sliver, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body * 0.85f, Rotation, origin, sliver * new Vector2(0.42f, 0.92f), SpriteEffects.None, 0f);

            //晶面闪光：翻滚到反光角才亮一下（正弦阈值做稀疏化），失泽后熄灭
            float fresh = luster * (1f - MathHelper.Clamp(t * 1.7f, 0f, 1f));
            if (fresh > 0.05f) {
                float tw = MathF.Sin(Main.GlobalTimeWrappedHourly * 9.5f + glintSeed + Rotation * 2f);
                float flash = MathF.Max(0f, tw);
                flash = flash * flash * flash;
                if (flash > 0.25f) {
                    Texture2D star = CWRAsset.StarGlow01?.Value;
                    if (star != null) {
                        //A=0 加色：晶面锐利反光点
                        Color glint = new Color(255, 214, 224, 0) * (flash * fresh * 0.7f * Opacity);
                        spriteBatch.Draw(star, pos, null, glint, Rotation,
                            star.Size() * 0.5f, 0.14f * Scale, SpriteEffects.None, 0f);
                    }
                }
            }
            return false;
        }
    }
}
