using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering
{
    /// <summary>海水水珠：速度拉伸+重力抛物线，材质是水不是能量</summary>
    internal class PRT_FishronSpray : BasePRT
    {
        public override int InGame_World_MaxCount => 4000;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private Color initialColor;
        private float gravity;

        public PRT_FishronSpray Configure(int lifetime, float gravityAccel) {
            initialColor = Color;
            Lifetime = lifetime;
            gravity = gravityAccel;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity.X *= 0.985f;
            Velocity.Y += gravity;
            //末段收缩淡出
            float t = LifetimeCompletion;
            Scale *= 0.985f;
            Color = Color.Lerp(initialColor, Color.Transparent, t * t);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            //沿速度拉伸，快=长条，慢=近圆珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.09f, 0.6f, 2.6f);
            Vector2 scale = new Vector2(0.4f, 0.42f * stretch) * Scale;
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
                texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            //高光芯更窄
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color * 0.6f, Rotation,
                texture.Size() * 0.5f, scale * new Vector2(0.45f, 0.9f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>泡沫团：Fog 贴图上飘散逸，用作水面搅动/龙卷预兆/浪迹余痕</summary>
    internal class PRT_FishronFoam : BasePRT
    {
        public override int InGame_World_MaxCount => 1200;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Fog";

        private Color initialColor;
        private float spinRate;

        public PRT_FishronFoam Configure(int lifetime, float spin) {
            initialColor = Color;
            Lifetime = lifetime;
            spinRate = spin;
            //Fog 是不对称烟羽，镜像+随机初转防贴纸感
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            ai[0] = Main.rand.Next(2);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spinRate = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() {
            Velocity *= 0.96f;
            Rotation += spinRate;
            float t = LifetimeCompletion;
            //先浮现后消散
            float envelope = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
            Color = initialColor * envelope;
            Scale += 0.006f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            SpriteEffects flip = ai[0] == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
                texture.Size() * 0.5f, Scale * 0.6f, flip, 0f);
            return false;
        }
    }
}
