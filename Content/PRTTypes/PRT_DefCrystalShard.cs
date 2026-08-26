using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 防御工事共用晶质碎片:护盾膜破碎/受击碎光与冻结冰晶爆裂共用,
    /// Extra_98 真 alpha(黑底贴图会糊黑底)。翻滚下坠渐隐,末段收缩读作能量耗散
    /// </summary>
    internal class PRT_DefCrystalShard : BasePRT
    {
        public override int InGame_World_MaxCount => 120;
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float spin;
        private float gravity;

        public PRT_DefCrystalShard Configure(int lifetime, float spinRate, float gravityPerFrame = 0.05f) {
            Lifetime = lifetime;
            initialColor = Color;
            spin = spinRate;
            gravity = gravityPerFrame;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
            gravity = 0f;
        }

        public override void AI() {
            Velocity *= 0.965f;
            Velocity.Y += gravity;
            Rotation += spin;

            float t = LifetimeCompletion;
            Scale *= 0.988f;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 1.8f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //两枚错向窄梭交叠读作碎玻璃片,亮芯提一层
            Vector2 scale = new Vector2(0.22f, 0.5f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.7f, Rotation + 1.25f, origin, scale * 0.72f, SpriteEffects.None, 0f);
            Color core = Color.White * (Color.A / 255f * 0.45f);
            spriteBatch.Draw(tex, pos, null, core, Rotation, origin, scale * 0.4f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
