using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>汇聚符文划痕：被比例引力拉向目标点，到达即灭</summary>
    internal class PRT_CultistRune : BasePRT
    {
        public Color InitialColor;
        public Vector2 TargetPoint;
        public float PullRate;
        public override int InGame_World_MaxCount => 3000;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Line";

        public PRT_CultistRune Configure(Vector2 targetPoint, float pullRate, int lifetime) {
            InitialColor = Color;
            TargetPoint = targetPoint;
            PullRate = pullRate;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
            TargetPoint = default;
            PullRate = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //比例引力汇聚，越近越快
            Vector2 toTarget = TargetPoint - Position;
            float dist = toTarget.Length();
            Velocity = Vector2.Lerp(Velocity, toTarget * PullRate, 0.2f);
            Rotation = Velocity.ToRotation();

            float fadeIn = Math.Min(Time / 6f, 1f);
            float fadeOut = 1f - LifetimeCompletion;
            Color = InitialColor * (fadeIn * fadeOut);

            if (dist < 14f) {
                Lifetime = Time;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            //沿速度各向异性拉长
            float speedStretch = MathHelper.Clamp(Velocity.Length() * 0.06f, 0.5f, 2.6f);
            Vector2 scale = new Vector2(0.7f * speedStretch, 0.1f) * Scale;
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
                texture.Size() * 0.5f, scale, 0, 0f);
            return false;
        }
    }

    /// <summary>火烬：速度拉伸，后半程坠落冷却</summary>
    internal class PRT_CultistEmber : BasePRT
    {
        public Color InitialColor;
        public override int InGame_World_MaxCount => 4000;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "StarGlow01";

        public PRT_CultistEmber Configure(int lifetime) {
            InitialColor = Color;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float t = LifetimeCompletion;
            Velocity *= 0.965f;
            if (t > 0.45f) {
                Velocity.Y += 0.16f;
            }
            //金→红→焦暗冷却
            Color = Color.Lerp(InitialColor, new Color(120, 30, 16), t * t) * (1f - t * t);
            Scale *= 0.985f;
            Rotation = Velocity.ToRotation();
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.1f, 0.6f, 2.2f);
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
                texture.Size() * 0.5f, new Vector2(0.32f * stretch, 0.18f) * Scale, 0, 0f);
            return false;
        }
    }

    /// <summary>霜晶闪点：缓降+呼吸闪烁</summary>
    internal class PRT_CultistFrost : BasePRT
    {
        public Color InitialColor;
        public float TwinkleSeed;
        public override int InGame_World_MaxCount => 4000;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "StarGlow01";

        public PRT_CultistFrost Configure(int lifetime) {
            InitialColor = Color;
            Lifetime = lifetime;
            TwinkleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
            TwinkleSeed = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.94f;
            Velocity.Y += 0.045f;
            float t = LifetimeCompletion;
            float twinkle = 0.72f + 0.28f * (float)Math.Sin(Time * 0.5f + TwinkleSeed);
            Color = InitialColor * ((1f - t) * twinkle);
            Rotation += 0.03f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
                texture.Size() * 0.5f, new Vector2(0.24f, 0.24f) * Scale, 0, 0f);
            //正交细芒
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color * 0.6f, Rotation + MathHelper.PiOver4,
                texture.Size() * 0.5f, new Vector2(0.4f, 0.1f) * Scale, 0, 0f);
            return false;
        }
    }

    /// <summary>电花：逐帧抖向，短命锐利</summary>
    internal class PRT_CultistVolt : BasePRT
    {
        public Color InitialColor;
        public override int InGame_World_MaxCount => 4000;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Line";

        public PRT_CultistVolt Configure(int lifetime) {
            InitialColor = Color;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //每2帧折向一次，电性游走
            if (Time % 2 == 0) {
                Velocity = Velocity.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(0.82f, 1.05f);
            }
            float t = LifetimeCompletion;
            Color = InitialColor * (1f - t);
            Rotation = Velocity.ToRotation();
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.09f, 0.5f, 1.8f);
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
                texture.Size() * 0.5f, new Vector2(0.5f * stretch, 0.07f) * Scale, 0, 0f);
            return false;
        }
    }

    /// <summary>法阵碎晶：仪式碎裂/分身破灭用，旋转飘落</summary>
    internal class PRT_CultistShard : BasePRT
    {
        public Color InitialColor;
        public float SpinRate;
        public override int InGame_World_MaxCount => 2000;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Triangle";

        public PRT_CultistShard Configure(int lifetime) {
            InitialColor = Color;
            Lifetime = lifetime;
            SpinRate = Main.rand.NextFloat(-0.22f, 0.22f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
            SpinRate = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.955f;
            Velocity.Y += 0.09f;
            Rotation += SpinRate;
            float t = LifetimeCompletion;
            Color = InitialColor * ((1f - t) * (1f - t * 0.4f));
            Scale *= 0.99f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
                texture.Size() * 0.5f, 0.11f * Scale, 0, 0f);
            return false;
        }
    }
}
