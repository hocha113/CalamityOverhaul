using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 防御工事烟尾:火舌熄灭后的暗烟与炮口余温热气。Masking/Fog 单帧真 alpha,
    /// AlphaBlend 直接染色;随机朝向+镜像防同贴纸盖章,上浮扩张渐散
    /// </summary>
    internal class PRT_DefSmoke : BasePRT
    {
        public override int InGame_World_MaxCount => 90;
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private Color initialColor;
        private SpriteEffects mirror;
        private float spin;

        public PRT_DefSmoke Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            mirror = Main.rand.NextBool() ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.015f, 0.015f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            mirror = SpriteEffects.None;
            spin = 0f;
        }

        public override void AI() {
            //烟减速上浮,缓慢自旋
            Velocity *= 0.95f;
            Velocity.Y -= 0.028f;
            Rotation += spin;

            float t = LifetimeCompletion;
            //淡入淡出:出生1/4淡入,余程淡出
            float env = MathF.Min(t / 0.25f, 1f) * MathF.Pow(1f - t, 1.2f);
            Color = initialColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            //随寿命膨胀
            float grow = 1f + LifetimeCompletion * 0.8f;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, tex.Size() * 0.5f,
                Scale * grow, mirror, 0f);
            return false;
        }
    }
}
