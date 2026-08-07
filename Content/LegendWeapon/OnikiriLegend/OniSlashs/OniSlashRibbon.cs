using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSlashs
{
    /// <summary>
    /// 刀路运动历史条带,撕开跨越的行程由它承载(swoosh 替刀)<br/>
    /// 每样本记录手心/刀角/投影刀长,切片宽度=该瞬间刀身长度,深度免费继承;<br/>
    /// 材质复用 <see cref="OniSlashRenderer"/> 着色器的应力线 technique——
    /// 刀刃拖过,世界膜被压出暗红应力痕,随后缝才撕开(因果链:挥动→撕裂)
    /// </summary>
    internal sealed class OniSlashRibbon
    {
        private struct Sample
        {
            public Vector2 Hand;
            public float Rot;
            public float Len;
            public float Depth;
            public int Life;
            public float Strength;
        }

        private const int Capacity = 26;
        private const int LifeFrames = 7;
        /// <summary>条带内端占刀长比例,根部留白避免糊在手上</summary>
        private const float RootFrac = 0.26f;

        private readonly Sample[] samples = new Sample[Capacity];
        private int head;

        private static readonly VertexPositionColorTexture[] vertexScratch = new VertexPositionColorTexture[Capacity * 2];

        /// <summary>衰减一帧(整画冻结时不调用)</summary>
        public void Update() {
            for (int i = 0; i < samples.Length; i++) {
                if (samples[i].Life > 0) {
                    samples[i].Life--;
                }
            }
        }

        public void Clear() {
            for (int i = 0; i < samples.Length; i++) {
                samples[i].Life = 0;
            }
        }

        public void Push(Vector2 hand, float rot, float len, float depth, float strength) {
            samples[head] = new Sample {
                Hand = hand,
                Rot = rot,
                Len = len,
                Depth = depth,
                Life = LifeFrames,
                Strength = strength,
            };
            head = (head + 1) % Capacity;
        }

        public bool AnyAlive() {
            for (int i = 0; i < samples.Length; i++) {
                if (samples[i].Life > 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>提交条带,须处于 <see cref="OniSlashRenderer.BeginDraw"/> 的设备状态内</summary>
        public void Draw(GraphicsDevice device, Effect fx, float seed) {
            //环内自旧到新收集存活样本
            int count = 0;
            float maxStrength = 0f;
            for (int i = 0; i < Capacity; i++) {
                Sample s = samples[(head + i) % Capacity];
                if (s.Life <= 0) {
                    continue;
                }
                float ageT = 1f - s.Life / (float)LifeFrames;
                float fade = s.Strength * (1f - ageT);
                if (fade <= 0.02f) {
                    continue;
                }
                Vector2 dir = s.Rot.ToRotationVector2();
                Vector2 root = s.Hand + dir * (s.Len * RootFrac);
                Vector2 tip = s.Hand + dir * (s.Len * 0.99f);
                byte alpha = (byte)(MathHelper.Clamp(fade, 0f, 1f) * 255f);
                Color data = new(128, 255, 255, alpha);
                float uc = count / (float)(Capacity - 1);
                vertexScratch[count * 2] = new VertexPositionColorTexture(root.ToVector3()
                    , data, new Vector2(uc, 0f));
                vertexScratch[count * 2 + 1] = new VertexPositionColorTexture(tip.ToVector3()
                    , data, new Vector2(uc, 1f));
                maxStrength = MathF.Max(maxStrength, fade);
                count++;
            }
            if (count < 2) {
                return;
            }

            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uDetailSeed"]?.SetValue(seed);
            fx.Parameters["uBurr"]?.SetValue(0f);
            fx.Parameters["uGlowIn"]?.SetValue(0f);
            fx.Parameters["uGapeT"]?.SetValue(1f);
            fx.Parameters["uOpacity"]?.SetValue(1f);
            fx.Parameters["uFarSel"]?.SetValue(0f);
            fx.Parameters["uFarDim"]?.SetValue(0f);
            fx.Parameters["uU0"]?.SetValue(0f);
            fx.Parameters["uU1"]?.SetValue(count / (float)(Capacity - 1));
            fx.Parameters["uEmber"]?.SetValue(0f);
            fx.Parameters["uTelegraph"]?.SetValue(0.9f + maxStrength * 0.5f);

            fx.CurrentTechnique = fx.Techniques["TelegraphTech"];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertexScratch, 0, count * 2 - 2);
            }
        }
    }
}
