using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil
{
    /// <summary>
    /// 风雪流丝：暴雪吟的主粒子。Extra_98 真 alpha 梭形沿风向拉伸，
    /// 快时成丝、慢时成絮，带轻微纵向摆动，端部靠贴图自带梭形衰减收口
    /// </summary>
    internal class PRT_FrostveilFlake : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 320;

        private Color initialColor;
        private float wobblePhase;
        private float windX;

        public PRT_FrostveilFlake Configure(int lifetime, float wind) {
            Lifetime = lifetime;
            windX = wind;
            initialColor = Color;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            wobblePhase = 0f;
            windX = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 70;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //横向缓慢向风速靠拢，纵向轻摆+缓降
            Velocity.X = MathHelper.Lerp(Velocity.X, windX, 0.04f);
            Velocity.Y = MathHelper.Lerp(Velocity.Y,
                1.4f + MathF.Sin(wobblePhase + Time * 0.11f) * 0.9f, 0.06f);
            if (Velocity.LengthSquared() > 0.05f) {
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            }

            float t = LifetimeCompletion;
            float env = MathHelper.Clamp(t / 0.14f, 0f, 1f)
                * MathHelper.Clamp((1f - t) / 0.28f, 0f, 1f);
            Color = initialColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //快成丝、慢成絮：拉伸随速度走
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.07f, 0.15f, 1f);
            Vector2 body = new Vector2(0.10f * (1f - stretch * 0.3f),
                0.26f * (1f + stretch * 1.9f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, body, SpriteEffects.None, 0f);
            //窄芯提亮一层，读作雪丝的反光
            spriteBatch.Draw(tex, pos, null, Color * 0.55f, Rotation, origin,
                body * new Vector2(0.42f, 1.04f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 呼气白雾：白毛风暴露度的第一层反馈。Fog 真 alpha 烟羽，
    /// 出口小而实，随即膨胀、随风飘散、快速透明化
    /// </summary>
    internal class PRT_FrostveilBreath : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 48;

        private Color initialColor;
        private float spinRate;
        private float growRate;

        public PRT_FrostveilBreath Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            //生成帧不跑 AI：首帧直绘用的 Color 预乘首帧包络（t=0 时为 0），防单帧闪现
            Color = initialColor * 0f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spinRate = Main.rand.NextFloat(-0.012f, 0.012f);
            growRate = Main.rand.NextFloat(0.010f, 0.016f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spinRate = 0f;
            growRate = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 46;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity *= 0.94f;
            Velocity.Y -= 0.008f;//余温微升
            Rotation += spinRate;
            Scale += growRate;

            float t = LifetimeCompletion;
            //快进慢出：呼出的一瞬最实，随后散成薄雾
            float env = MathHelper.Clamp(t / 0.10f, 0f, 1f) * (1f - t) * (1f - t);
            Color = initialColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color,
                Rotation, origin, Scale * 0.16f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
