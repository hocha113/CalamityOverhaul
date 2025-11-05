using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    /// <summary>
    /// 闪电效果
    /// </summary>
    internal class LightningBolt
    {
        public Vector2 StartPos;
        public Vector2 Direction;
        public float Length;
        public int MaxLife;
        public int Life;
        public List<Vector2> Points = new();

        public LightningBolt(Vector2 start, Vector2 direction, float length, int life) {
            StartPos = start;
            Direction = direction.SafeNormalize(Vector2.UnitX);
            Length = length;
            MaxLife = life;
            Life = 0;
            GeneratePoints();
        }

        private void GeneratePoints() {
            int segments = (int)(Length / 15f);
            Vector2 currentPos = StartPos;
            Points.Add(currentPos);

            for (int i = 0; i < segments; i++) {
                float segmentLength = Length / segments;
                Vector2 offset = Direction.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * segmentLength;
                offset += Main.rand.NextVector2Circular(12f, 12f);
                currentPos += offset;
                Points.Add(currentPos);
            }
        }

        public void Update() => Life++;

        public bool IsExpired() => Life >= MaxLife;

        public void Draw(SpriteBatch sb) {
            float alpha = 1f - Life / (float)MaxLife;
            if (alpha <= 0.05f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < Points.Count - 1; i++) {
                Vector2 start = Points[i];
                Vector2 end = Points[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();

                Color color = Color.Lerp(Color.Gold, Color.Yellow, Main.rand.NextFloat()) * alpha * 0.9f;

                sb.Draw(
                    pixel,
                    start - Main.screenPosition,
                    null,
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 4f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    /// <summary>
    /// 爆炸冲击波
    /// </summary>
    internal class ExplosionWave
    {
        public Vector2 Center;
        public float Radius;
        public float MaxRadius = 1000f;//缩小最大半径
        public int Life;
        public int MaxLife = 35;//缩短持续时间
        public Color WaveColor;
        public float StartDelay;

        public ExplosionWave(Vector2 center, float startDelay) {
            Center = center;
            Radius = 0f;
            Life = 0;
            StartDelay = startDelay;
            WaveColor = Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat());
        }

        public void Update() {
            Life++;
            if (Life < StartDelay) return;

            float progress = (Life - StartDelay) / MaxLife;
            Radius = MathHelper.Lerp(0f, MaxRadius, CWRUtils.EaseOutQuad(progress));
        }

        public bool ShouldRemove() => Life >= MaxLife + StartDelay;

        public void Draw(SpriteBatch sb) {
            if (Life < StartDelay) return;

            float progress = (Life - StartDelay) / MaxLife;
            float alpha = (1f - progress) * 0.6f;
            if (alpha <= 0.05f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = 60;//减少分段数
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 p1 = Center + angle1.ToRotationVector2() * Radius;
                Vector2 p2 = Center + angle2.ToRotationVector2() * Radius;

                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float rotation = diff.ToRotation();

                Color color = WaveColor * alpha;
                color.A = 0;

                sb.Draw(
                    pixel,
                    p1 - Main.screenPosition,
                    null,
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 4f + alpha * 3f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    /// <summary>
    /// 冲击火花
    /// </summary>
    internal class ImpactSpark
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float Alpha;
        public int Life;
        public int MaxLife;
        public Color SparkColor;

        public ImpactSpark(Vector2 position, Vector2 velocity) {
            Position = position;
            Velocity = velocity;
            Scale = Main.rand.NextFloat(0.6f, 1.2f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Alpha = 1f;
            Life = 0;
            MaxLife = Main.rand.Next(20, 35);
            SparkColor = Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat());
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.96f;
            Rotation += 0.1f;

            float progress = Life / (float)MaxLife;
            Alpha = (float)Math.Sin((1f - progress) * MathHelper.PiOver2);
            Scale *= 0.98f;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(SpriteBatch sb) {
            Texture2D sparkTex = CWRAsset.StarTexture.Value;
            Color drawColor = SparkColor * Alpha;
            drawColor.A = 0;

            sb.Draw(
                sparkTex,
                Position - Main.screenPosition,
                null,
                drawColor,
                Rotation,
                sparkTex.Size() / 2f,
                Scale * 0.15f,
                SpriteEffects.None,
                0f
            );
        }
    }
}
