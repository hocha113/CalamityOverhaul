using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    internal enum OniParticleKind : byte
    {
        /// <summary>落花</summary>
        Petal,
        /// <summary>香灰</summary>
        Ash,
        /// <summary>墨粒</summary>
        InkMote,
        /// <summary>鬼火余烬</summary>
        Ember,
        /// <summary>鏨下火星(改铭台)</summary>
        Spark,
        /// <summary>锉下铁屑(改铭台)</summary>
        Filing,
        /// <summary>打粉白雾(改铭台)</summary>
        Powder,
    }

    /// <summary>池化 UI 粒子,三屏各一实例,像素矩形形体</summary>
    internal sealed class OniUIParticlePool
    {
        private struct Particle
        {
            public bool Active;
            public OniParticleKind Kind;
            public Vector2 Position;
            public Vector2 Velocity;
            /// <summary>墨粒的出发点</summary>
            public Vector2 Start;
            /// <summary>墨粒的收束目标</summary>
            public Vector2 Target;
            /// <summary>墨粒的弧线控制偏移</summary>
            public Vector2 ArcOffset;
            public float Life;
            public float MaxLife;
            /// <summary>墨粒起飞延迟帧</summary>
            public float Delay;
            public float Scale;
            public float Rotation;
            public float RotSpeed;
            public float Seed;
            public Color Color;
        }

        private readonly Particle[] particles;
        private int cursor;

        public OniUIParticlePool(int capacity = 120) {
            particles = new Particle[capacity];
        }

        private ref Particle Next() {
            cursor = (cursor + 1) % particles.Length;
            return ref particles[cursor];
        }

        public void Clear() => Array.Clear(particles, 0, particles.Length);

        /// <summary>落花,outward -1左/+1右/0直落</summary>
        public void SpawnPetal(Vector2 pos, float outward = 0f, float scale = 1f) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = OniParticleKind.Petal;
            p.Position = pos;
            p.Velocity = new Vector2(outward * Main.rand.NextFloat(0.02f, 0.07f), Main.rand.NextFloat(0.32f, 0.62f));
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(210f, 320f);
            p.Scale = Main.rand.NextFloat(0.8f, 1.25f) * scale;
            p.Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            p.RotSpeed = Main.rand.NextFloat(-0.02f, 0.02f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Delay = 0f;
        }

        /// <summary>香灰</summary>
        public void SpawnAsh(Vector2 pos) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = OniParticleKind.Ash;
            p.Position = pos + Main.rand.NextVector2Circular(1.2f, 0.6f);
            p.Velocity = new Vector2(Main.rand.NextFloat(-0.08f, 0.08f), Main.rand.NextFloat(0.22f, 0.45f));
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(50f, 95f);
            p.Scale = Main.rand.NextFloat(0.8f, 1.6f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Delay = 0f;
        }

        /// <summary>墨粒,delay 后飞向 target</summary>
        public void SpawnInkMote(Vector2 pos, Vector2 target, Color color, float delay = 0f) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = OniParticleKind.InkMote;
            p.Position = pos;
            p.Start = pos;
            p.Target = target;
            //垂直于飞行方向的弧线控制偏移,左右随机
            Vector2 dir = target - pos;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            p.ArcOffset = perp * Main.rand.NextFloat(-0.35f, 0.35f) * dir.Length();
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(26f, 44f);
            p.Delay = delay;
            p.Scale = Main.rand.NextFloat(1.4f, 2.8f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Color = color;
        }

        /// <summary>余烬上飘</summary>
        public void SpawnEmber(Vector2 pos) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = OniParticleKind.Ember;
            p.Position = pos;
            p.Velocity = new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -Main.rand.NextFloat(0.25f, 0.6f));
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(34f, 70f);
            p.Scale = Main.rand.NextFloat(0.9f, 1.8f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Delay = 0f;
        }

        /// <summary>鏨下火星,自凿点锥形上迸,受重力,白热→金→深红</summary>
        public void SpawnSpark(Vector2 pos) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = OniParticleKind.Spark;
            p.Position = pos;
            float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.9f, 0.9f);
            p.Velocity = ang.ToRotationVector2() * Main.rand.NextFloat(1.4f, 3.6f);
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(16f, 34f);
            p.Scale = Main.rand.NextFloat(0.7f, 1.4f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Delay = 0f;
        }

        /// <summary>锉下铁屑,坠落带翻转</summary>
        public void SpawnFiling(Vector2 pos) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = OniParticleKind.Filing;
            p.Position = pos + Main.rand.NextVector2Circular(3f, 1.2f);
            p.Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.3f, 0.9f));
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(30f, 62f);
            p.Scale = Main.rand.NextFloat(0.7f, 1.5f);
            p.Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            p.RotSpeed = Main.rand.NextFloat(-0.16f, 0.16f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Delay = 0f;
        }

        /// <summary>打粉白雾,缓浮缓散</summary>
        public void SpawnPowder(Vector2 pos) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = OniParticleKind.Powder;
            p.Position = pos + Main.rand.NextVector2Circular(5f, 3f);
            p.Velocity = Main.rand.NextVector2Circular(0.5f, 0.3f) - new Vector2(0f, Main.rand.NextFloat(0.1f, 0.35f));
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(26f, 52f);
            p.Scale = Main.rand.NextFloat(1.4f, 3.2f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Delay = 0f;
        }

        public void Update() {
            for (int i = 0; i < particles.Length; i++) {
                ref Particle p = ref particles[i];
                if (!p.Active) {
                    continue;
                }
                if (p.Delay > 0f) {
                    p.Delay -= 1f;
                    continue;
                }
                p.Life += 1f;
                if (p.Life >= p.MaxLife) {
                    p.Active = false;
                    continue;
                }

                switch (p.Kind) {
                    case OniParticleKind.Petal:
                        p.Position.Y += p.Velocity.Y * (0.85f + 0.15f * (float)Math.Sin(p.Life * 0.05f + p.Seed));
                        p.Position.X += (float)Math.Cos(p.Life * 0.045f + p.Seed) * 0.22f + p.Velocity.X;
                        p.Rotation += p.RotSpeed;
                        break;
                    case OniParticleKind.Ash:
                        p.Position += p.Velocity;
                        p.Position.X += (float)Math.Sin(p.Life * 0.11f + p.Seed) * 0.10f;
                        break;
                    case OniParticleKind.InkMote: {
                        //二次贝塞尔:start → start+arc → target,缓入缓出
                        float t = p.Life / p.MaxLife;
                        float e = t * t * (3f - 2f * t);
                        Vector2 mid = (p.Start + p.Target) * 0.5f + p.ArcOffset;
                        Vector2 a = Vector2.Lerp(p.Start, mid, e);
                        Vector2 b = Vector2.Lerp(mid, p.Target, e);
                        p.Position = Vector2.Lerp(a, b, e);
                        break;
                    }
                    case OniParticleKind.Ember:
                        p.Position += p.Velocity;
                        p.Velocity.Y *= 0.985f;
                        p.Position.X += (float)Math.Sin(p.Life * 0.14f + p.Seed) * 0.14f;
                        break;
                    case OniParticleKind.Spark:
                        p.Position += p.Velocity;
                        p.Velocity.Y += 0.09f;
                        p.Velocity *= 0.97f;
                        break;
                    case OniParticleKind.Filing:
                        p.Position += p.Velocity;
                        p.Velocity.Y += 0.045f;
                        p.Rotation += p.RotSpeed;
                        break;
                    case OniParticleKind.Powder:
                        p.Position += p.Velocity;
                        p.Velocity *= 0.96f;
                        p.Position.X += (float)Math.Sin(p.Life * 0.09f + p.Seed) * 0.08f;
                        break;
                }
            }
        }

        public void Draw(SpriteBatch sb, float alpha) {
            for (int i = 0; i < particles.Length; i++) {
                ref Particle p = ref particles[i];
                if (!p.Active || p.Delay > 0f) {
                    continue;
                }
                float t = p.Life / p.MaxLife;

                switch (p.Kind) {
                    case OniParticleKind.Petal: {
                        float fade = (float)Math.Pow(Math.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi), 0.8);
                        //翻飞透视:花瓣绕长轴翻转,视觉宽度呼吸;软边扁瓣,告别硬方块
                        float flip = MathHelper.Lerp(0.32f, 1f, Math.Abs((float)Math.Sin(p.Life * 0.07f + p.Seed)));
                        Vector2 body = new(6.4f * p.Scale * flip, 3.5f * p.Scale);
                        Vector2 tipOff = p.Rotation.ToRotationVector2() * (3.1f * p.Scale * flip);
                        OniBrush.DrawFeathered(sb, p.Position + new Vector2(0.8f, 1.1f), p.Rotation, body,
                            OnikiriUITheme.Dark, alpha * 0.34f * fade);
                        OniBrush.DrawFeathered(sb, p.Position, p.Rotation, body,
                            OnikiriUITheme.Paper, alpha * 0.60f * fade);
                        OniBrush.DrawSoftDot(sb, p.Position + tipOff, 2.4f * p.Scale * flip,
                            OnikiriUITheme.Bright, alpha * 0.42f * fade);
                        break;
                    }
                    case OniParticleKind.Ash: {
                        float fade = 1f - t;
                        Color col = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Ink, t);
                        OniBrush.DrawSoftDot(sb, p.Position, 1.5f * p.Scale, col, alpha * 0.7f * fade);
                        break;
                    }
                    case OniParticleKind.InkMote: {
                        //抵近收小:头 20% 淡入,尾 30% 缩没
                        float fadeIn = MathHelper.Clamp(t / 0.2f, 0f, 1f);
                        float shrink = 1f - MathHelper.Clamp((t - 0.7f) / 0.3f, 0f, 1f) * 0.8f;
                        float r = p.Scale * shrink * 0.85f;
                        OniBrush.DrawSoftDot(sb, p.Position, r * 1.55f, p.Color, alpha * 0.35f * fadeIn);
                        OniBrush.DrawSoftDot(sb, p.Position, r, p.Color, alpha * 0.85f * fadeIn);
                        break;
                    }
                    case OniParticleKind.Ember: {
                        float flick = 0.6f + 0.4f * (float)Math.Sin(p.Life * 0.5f + p.Seed);
                        float fade = (1f - t) * flick;
                        OniBrush.DrawSoftDot(sb, p.Position, 2.4f * p.Scale, OnikiriUITheme.BurnDim, alpha * 0.5f * fade);
                        OniBrush.DrawSoftDot(sb, p.Position, 1.2f * p.Scale, OnikiriUITheme.BurnHot, alpha * 0.8f * fade);
                        break;
                    }
                    case OniParticleKind.Spark: {
                        //速度拉伸:火星是划出来的短线,两端没入
                        float fade = 1f - t;
                        float speed = p.Velocity.Length();
                        float rot = p.Velocity.ToRotation();
                        float len = (2.2f + speed * 1.6f) * p.Scale;
                        Color col = t < 0.25f ? OnikiriUITheme.HotWhite
                            : t < 0.6f ? OnikiriUITheme.GoldInlay : OnikiriUITheme.BurnDim;
                        OniBrush.DrawSoftStreak(sb, p.Position, rot, Math.Max(3f, len), Math.Max(1f, 1.1f * p.Scale),
                            col, alpha * 0.9f * fade);
                        break;
                    }
                    case OniParticleKind.Filing: {
                        float fade = 1f - t * t;
                        OniBrush.DrawSoftStreak(sb, p.Position, p.Rotation, Math.Max(3f, 3.2f * p.Scale),
                            Math.Max(1f, 1.0f * p.Scale), OnikiriUITheme.Ink, alpha * 0.85f * fade, 0.35f);
                        break;
                    }
                    case OniParticleKind.Powder: {
                        float fade = (float)Math.Pow(Math.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi), 0.7);
                        float grow = 1f + t * 1.6f;
                        OniBrush.DrawSoftDot(sb, p.Position, 2.2f * p.Scale * grow,
                            OnikiriUITheme.Paper, alpha * 0.22f * fade);
                        break;
                    }
                }
            }
        }
    }
}
