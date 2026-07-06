using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    internal enum OniParticleKind : byte
    {
        /// <summary>落花：横摆下落 + 宽度呼吸的翻飞透视</summary>
        Petal,
        /// <summary>香灰：从线香燃点剥落，微摆缓沉</summary>
        Ash,
        /// <summary>墨粒：沿微弧线收束到目标点（烟凝成字）</summary>
        InkMote,
        /// <summary>鬼火余烬：青色小火星上飘低闪</summary>
        Ember,
    }

    /// <summary>
    /// 池化的鬼切 UI 粒子系统，点鬼簿三屏各持有一个实例自行驱动。<br/>
    /// 全部形体由像素矩形拼成，不依赖贴图轮廓
    /// </summary>
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

        /// <summary>落花。outward 为水平漂移方向(-1 左 / +1 右 / 0 直落)</summary>
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

        /// <summary>香灰，从线香燃点剥落</summary>
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

        /// <summary>墨粒：delay 帧后沿微弧线飞向 target，抵近时缩小熄灭</summary>
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

        /// <summary>鬼火余烬，自 pos 上飘</summary>
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
                }
            }
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f, 0.5f);

            for (int i = 0; i < particles.Length; i++) {
                ref Particle p = ref particles[i];
                if (!p.Active || p.Delay > 0f) {
                    continue;
                }
                float t = p.Life / p.MaxLife;

                switch (p.Kind) {
                    case OniParticleKind.Petal: {
                        float fade = (float)Math.Pow(Math.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi), 0.8);
                        //翻飞透视:花瓣绕长轴翻转,视觉宽度呼吸
                        float flip = MathHelper.Lerp(0.32f, 1f, Math.Abs((float)Math.Sin(p.Life * 0.07f + p.Seed)));
                        Vector2 body = new(6.4f * p.Scale * flip, 3.5f * p.Scale);
                        Vector2 tipOff = p.Rotation.ToRotationVector2() * (3.1f * p.Scale * flip);
                        sb.Draw(pixel, p.Position + new Vector2(0.8f, 1.1f), src, OnikiriUITheme.Dark * (alpha * 0.34f * fade), p.Rotation, half, body, SpriteEffects.None, 0f);
                        sb.Draw(pixel, p.Position, src, OnikiriUITheme.Paper * (alpha * 0.60f * fade), p.Rotation, half, body, SpriteEffects.None, 0f);
                        sb.Draw(pixel, p.Position + tipOff, src, OnikiriUITheme.Bright * (alpha * 0.42f * fade), p.Rotation, half, new Vector2(2.5f * p.Scale * flip, 2.0f * p.Scale), SpriteEffects.None, 0f);
                        break;
                    }
                    case OniParticleKind.Ash: {
                        float fade = 1f - t;
                        Color col = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Ink, t) * (alpha * 0.7f * fade);
                        sb.Draw(pixel, p.Position, src, col, 0f, half, new Vector2(1.6f * p.Scale, 1.2f * p.Scale), SpriteEffects.None, 0f);
                        break;
                    }
                    case OniParticleKind.InkMote: {
                        //抵近收小:头 20% 淡入,尾 30% 缩没
                        float fadeIn = MathHelper.Clamp(t / 0.2f, 0f, 1f);
                        float shrink = 1f - MathHelper.Clamp((t - 0.7f) / 0.3f, 0f, 1f) * 0.8f;
                        Color col = p.Color * (alpha * 0.85f * fadeIn);
                        sb.Draw(pixel, p.Position, src, col, p.Seed + p.Life * 0.1f, half, new Vector2(p.Scale * shrink), SpriteEffects.None, 0f);
                        break;
                    }
                    case OniParticleKind.Ember: {
                        float flick = 0.6f + 0.4f * (float)Math.Sin(p.Life * 0.5f + p.Seed);
                        float fade = (1f - t) * flick;
                        sb.Draw(pixel, p.Position, src, OnikiriUITheme.GhostDim * (alpha * 0.5f * fade), 0f, half, new Vector2(2.2f * p.Scale), SpriteEffects.None, 0f);
                        sb.Draw(pixel, p.Position, src, OnikiriUITheme.GhostFire * (alpha * 0.8f * fade), 0f, half, new Vector2(1.1f * p.Scale), SpriteEffects.None, 0f);
                        break;
                    }
                }
            }
        }
    }
}
