using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.OtherMods.BossChecklist
{
    /// <summary>
    /// 图鉴沙盒微粒池：喷沙/花瓣/灵液滴等一次性小演出（场景坐标，magic-pixel quad）。
    /// 纯表现，无上限外溢（超容直接丢弃新粒）
    /// </summary>
    internal sealed class PortraitMotes
    {
        private struct Mote
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public Vector2 Size;
            public float Life;
            public float MaxLife;
            public float Rot;
            public float RotVel;
            public float Gravity;
            public float Drag;
            public Color Color;
            /// <summary>A=0 加色读数（亮粒）；否则不透明体粒</summary>
            public bool Additive;
        }

        private const int Cap = 220;
        private readonly List<Mote> motes = new(96);

        public void Clear() => motes.Clear();

        public void Spawn(Vector2 pos, Vector2 vel, Vector2 size, Color color, float lifeSeconds,
            float gravity = 0f, float drag = 1f, float rot = 0f, float rotVel = 0f, bool additive = false) {
            if (motes.Count >= Cap) {
                return;
            }
            motes.Add(new Mote {
                Pos = pos,
                Vel = vel,
                Size = size,
                Color = color,
                Life = lifeSeconds,
                MaxLife = MathF.Max(lifeSeconds, 0.01f),
                Gravity = gravity,
                Drag = drag,
                Rot = rot,
                RotVel = rotVel,
                Additive = additive,
            });
        }

        /// <summary>推进一帧（frames = dt×60）</summary>
        public void Update(float frames) {
            for (int i = motes.Count - 1; i >= 0; i--) {
                Mote m = motes[i];
                m.Life -= frames / 60f;
                if (m.Life <= 0f) {
                    motes.RemoveAt(i);
                    continue;
                }
                m.Vel.Y += m.Gravity * frames;
                m.Vel *= MathF.Pow(m.Drag, frames);
                m.Pos += m.Vel * frames;
                m.Rot += m.RotVel * frames;
                motes[i] = m;
            }
        }

        public void Draw(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null || motes.Count == 0) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f, 0.5f);
            foreach (Mote m in motes) {
                float a = MathHelper.Clamp(m.Life / m.MaxLife, 0f, 1f);
                Color c = frame.Tint(m.Additive ? m.Color with { A = 0 } : m.Color) * a;
                sb.Draw(pixel, m.Pos, src, c, m.Rot, origin, m.Size, SpriteEffects.None, 0f);
            }
        }
    }
}
