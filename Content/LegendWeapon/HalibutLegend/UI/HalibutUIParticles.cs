using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal enum HalibutParticleKind : byte
    {
        /// <summary>上浮气泡，带水平摇摆</summary>
        Bubble,
        /// <summary>海雪，缓慢下沉的浮游碎屑</summary>
        Snow,
        /// <summary>火花，径向爆发后衰减</summary>
        Spark,
        /// <summary>扩散脉冲环</summary>
        RingPulse,
    }

    /// <summary>
    /// 池化的UI粒子系统，取代旧UI中十余个手写粒子类
    /// 每个视图持有一个实例，自行驱动 <see cref="Update"/> 与 <see cref="Draw"/>
    /// </summary>
    internal class HalibutUIParticlePool
    {
        private struct Particle
        {
            public bool Active;
            public HalibutParticleKind Kind;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public float RotSpeed;
            public float Seed;
            public Color Color;
        }

        private readonly Particle[] particles;
        private int cursor;

        /// <summary>
        ///飞行图标，贝塞尔+到达回调，解锁/收纳演出
        /// </summary>
        private readonly List<FlyingIcon> flyingIcons = [];

        public HalibutUIParticlePool(int capacity = 160) {
            particles = new Particle[capacity];
        }

        /// <summary>
        /// 是否还有任何活跃的飞行图标
        /// </summary>
        public bool HasFlyingIcons => flyingIcons.Count > 0;

        private ref Particle Next() {
            cursor = (cursor + 1) % particles.Length;
            return ref particles[cursor];
        }

        public void SpawnBubble(Vector2 pos, float scale = 1f, float? lifeOverride = null) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = HalibutParticleKind.Bubble;
            p.Position = pos;
            p.Velocity = new Vector2(0f, -Main.rand.NextFloat(0.35f, 0.95f));
            p.Life = 0f;
            p.MaxLife = lifeOverride ?? Main.rand.NextFloat(80f, 150f);
            p.Scale = Main.rand.NextFloat(1.6f, 4.6f) * scale;
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Color = HalibutTheme.Glow;
        }

        public void SpawnSnow(Vector2 pos, float scale = 1f) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = HalibutParticleKind.Snow;
            p.Position = pos;
            p.Velocity = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(0.18f, 0.5f));
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(120f, 220f);
            p.Scale = Main.rand.NextFloat(0.8f, 1.9f) * scale;
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Color = HalibutTheme.Caustic;
        }

        public void SpawnSpark(Vector2 pos, Vector2 velocity, Color color, float scale = 1f) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = HalibutParticleKind.Spark;
            p.Position = pos;
            p.Velocity = velocity;
            p.Life = 0f;
            p.MaxLife = Main.rand.NextFloat(26f, 48f);
            p.Scale = Main.rand.NextFloat(0.7f, 1.4f) * scale;
            p.Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            p.RotSpeed = Main.rand.NextFloat(-0.12f, 0.12f);
            p.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Color = color;
        }

        /// <summary>
        /// 径向火花爆发
        /// </summary>
        public void SpawnBurst(Vector2 center, Color color, int count, float speed = 3f, float scale = 1f) {
            for (int i = 0; i < count; i++) {
                float angle = i / (float)count * MathHelper.TwoPi + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 vel = HalibutRenderer.AngleDir(angle) * Main.rand.NextFloat(speed * 0.5f, speed);
                SpawnSpark(center, vel, color, scale);
            }
        }

        public void SpawnRingPulse(Vector2 center, Color color, float maxRadius, float thickness = 2f) {
            ref Particle p = ref Next();
            p.Active = true;
            p.Kind = HalibutParticleKind.RingPulse;
            p.Position = center;
            p.Velocity = Vector2.Zero;
            p.Life = 0f;
            p.MaxLife = 36f;
            p.Scale = maxRadius;
            p.Rotation = thickness;
            p.Color = color;
        }

        /// <summary>
        /// 发射一个贝塞尔飞行图标；icon传null时绘制为发光光粒
        /// </summary>
        public void SpawnFlyingIcon(Texture2D icon, Vector2 start, Vector2 end, Action onArrive = null, float delay = 0f) {
            flyingIcons.Add(new FlyingIcon(icon, start, end, onArrive, delay));
        }

        /// <summary>
        /// 发射一个贝塞尔飞行光粒（无图标版本）
        /// </summary>
        public void SpawnFlyingMote(Vector2 start, Vector2 end, Action onArrive = null, float delay = 0f) {
            flyingIcons.Add(new FlyingIcon(null, start, end, onArrive, delay));
        }

        public void Clear() {
            for (int i = 0; i < particles.Length; i++) {
                particles[i].Active = false;
            }
            flyingIcons.Clear();
        }

        public void Update() {
            for (int i = 0; i < particles.Length; i++) {
                ref Particle p = ref particles[i];
                if (!p.Active) {
                    continue;
                }
                p.Life++;
                if (p.Life >= p.MaxLife) {
                    p.Active = false;
                    continue;
                }
                switch (p.Kind) {
                    case HalibutParticleKind.Bubble:
                        p.Position += p.Velocity;
                        p.Position.X += MathF.Sin(p.Life * 0.05f + p.Seed) * 0.32f;
                        break;
                    case HalibutParticleKind.Snow:
                        p.Position += p.Velocity;
                        p.Position.X += MathF.Sin(p.Life * 0.03f + p.Seed) * 0.2f;
                        break;
                    case HalibutParticleKind.Spark:
                        p.Position += p.Velocity;
                        p.Velocity *= 0.94f;
                        p.Rotation += p.RotSpeed;
                        break;
                    case HalibutParticleKind.RingPulse:
                        break;
                }
            }

            for (int i = flyingIcons.Count - 1; i >= 0; i--) {
                if (flyingIcons[i].Update()) {
                    flyingIcons[i].OnArrive?.Invoke();
                    flyingIcons.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch sb, float globalAlpha) {
            if (globalAlpha < 0.01f) {
                return;
            }
            Texture2D px = HalibutRenderer.Pixel;
            for (int i = 0; i < particles.Length; i++) {
                ref Particle p = ref particles[i];
                if (!p.Active) {
                    continue;
                }
                float lifeT = p.Life / p.MaxLife;
                float fade;
                switch (p.Kind) {
                    case HalibutParticleKind.Bubble: {
                        fade = MathF.Sin(lifeT * MathHelper.Pi);
                        Color c = p.Color * (fade * 0.4f * globalAlpha);
                        sb.Draw(px, p.Position, new Rectangle(0, 0, 1, 1), c, 0f,
                            new Vector2(0.5f), p.Scale, SpriteEffects.None, 0f);
                        //气泡高光
                        sb.Draw(px, p.Position - new Vector2(p.Scale * 0.22f), new Rectangle(0, 0, 1, 1),
                            HalibutTheme.Caustic * (fade * 0.3f * globalAlpha), 0f,
                            new Vector2(0.5f), p.Scale * 0.3f, SpriteEffects.None, 0f);
                        break;
                    }
                    case HalibutParticleKind.Snow: {
                        fade = MathF.Sin(lifeT * MathHelper.Pi);
                        sb.Draw(px, p.Position, new Rectangle(0, 0, 1, 1),
                            p.Color * (fade * 0.32f * globalAlpha), 0f,
                            new Vector2(0.5f), p.Scale, SpriteEffects.None, 0f);
                        break;
                    }
                    case HalibutParticleKind.Spark: {
                        fade = 1f - lifeT;
                        Color c = p.Color * (fade * globalAlpha);
                        sb.Draw(px, p.Position, new Rectangle(0, 0, 1, 1), c, p.Rotation,
                            new Vector2(0.5f), new Vector2(p.Scale * 3.2f, p.Scale), SpriteEffects.None, 0f);
                        sb.Draw(px, p.Position, new Rectangle(0, 0, 1, 1), c * 0.5f, p.Rotation + MathHelper.PiOver2,
                            new Vector2(0.5f), new Vector2(p.Scale * 2f, p.Scale * 0.6f), SpriteEffects.None, 0f);
                        break;
                    }
                    case HalibutParticleKind.RingPulse: {
                        float eased = VaultUtils.EaseOutCubic(lifeT);
                        float radius = p.Scale * eased;
                        float alpha = (1f - eased) * 0.8f * globalAlpha;
                        HalibutRenderer.DrawRing(sb, p.Position, radius,
                            MathHelper.Lerp(p.Rotation, 1f, eased), p.Color * alpha);
                        break;
                    }
                }
            }

            foreach (FlyingIcon icon in flyingIcons) {
                icon.Draw(sb, globalAlpha);
            }
        }

        /// <summary>
        /// 贝塞尔轨迹飞行的图标实体，取代旧的 SkillIconEntity / LibrarySkillFlyEntity / ImproveFlyParticle
        /// </summary>
        private sealed class FlyingIcon
        {
            private readonly Texture2D icon;
            private readonly Vector2 start;
            private readonly Vector2 end;
            private readonly Vector2 control1;
            private readonly Vector2 control2;
            private float delay;
            private int life;
            private const int MaxLife = 52;
            private float rotation;
            private readonly float rotSpeed;
            public readonly Action OnArrive;

            public FlyingIcon(Texture2D icon, Vector2 start, Vector2 end, Action onArrive, float delay) {
                this.icon = icon;
                this.start = start;
                this.end = end;
                this.delay = delay;
                OnArrive = onArrive;
                float distance = Vector2.Distance(start, end);
                control1 = start + new Vector2(distance * 0.18f, -distance * 0.38f);
                control2 = (start + end) * 0.5f + new Vector2(distance * 0.08f, -distance * 0.26f);
                rotSpeed = Main.rand.NextFloat(-0.08f, 0.08f);
            }

            public Vector2 Position { get; private set; }

            public bool Update() {
                if (delay > 0f) {
                    delay--;
                    Position = start;
                    return false;
                }
                life++;
                float t = VaultUtils.EaseOutCubic(MathHelper.Clamp(life / (float)MaxLife, 0f, 1f));
                Position = VaultUtils.CubicBezier(t, start, control1, control2, end);
                rotation += rotSpeed;
                return life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float globalAlpha) {
                if (delay > 0f) {
                    return;
                }
                float t = MathHelper.Clamp(life / (float)MaxLife, 0f, 1f);
                float scale = t < 0.3f
                    ? MathHelper.Lerp(0.55f, 1.05f, t / 0.3f)
                    : MathHelper.Lerp(1.05f, 0.72f, (t - 0.3f) / 0.7f);
                float alpha = (t < 0.85f ? 1f : 1f - (t - 0.85f) / 0.15f) * globalAlpha;

                if (icon == null) {
                    //无图标、光粒+短拖尾
                    HalibutRenderer.DrawSoftGlow(sb, Position, 9f * scale, HalibutTheme.Glow * (alpha * 0.7f));
                    HalibutRenderer.DrawDisc(sb, Position, 2.2f * scale, 2f, HalibutTheme.Caustic * alpha);
                    return;
                }

                Vector2 origin = icon.Size() * 0.5f;
                //发光残影
                Color glow = HalibutTheme.Glow with { A = 0 } * (alpha * 0.55f);
                for (int i = 0; i < 4; i++) {
                    Vector2 offset = HalibutRenderer.AngleDir(MathHelper.TwoPi * i / 4f + rotation) * 2.6f;
                    sb.Draw(icon, Position + offset, null, glow, rotation, origin, scale * 1.08f, SpriteEffects.None, 0f);
                }
                sb.Draw(icon, Position, null, Color.White * alpha, rotation, origin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
