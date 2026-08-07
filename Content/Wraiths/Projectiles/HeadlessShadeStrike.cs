using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Projectiles
{
    /// <summary>一道斩痕。世界空间的微弓直线切口，寿命内自己走完揭开→错位→愈合</summary>
    internal struct ShadeCut
    {
        public Vector2 Center;
        public float Angle;
        public float HalfLength;
        public float HalfWidth;
        /// <summary>矢高（带符号），刀路不是尺子画的直线</summary>
        public float Bow;
        /// <summary>厚度力点位置 0..1，入刀端针尖、出刀端毛尾</summary>
        public float Peak;
        /// <summary>±1，毛口在哪一侧</summary>
        public float Flip;
        public float Seed;
        public int Age;
        public int Life;
        /// <summary>出生延迟，交叉刀靠它错开</summary>
        public int Delay;
    }

    /// <summary>飞散的影屑。扁平暗片，沿速度拉伸并翻滚</summary>
    internal struct ShadeShard
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Angle;
        public float Spin;
        public float Length;
        public float Width;
        /// <summary>新鲜撕口的骨白量，几帧内冷掉</summary>
        public float Bone;
        public int Age;
        public int Life;
    }

    /// <summary>
    /// 无头鬼影的斩痕场与影屑场，纯客户端表现层。
    /// 斩痕活得比这一击久，是"这里被斩过"的唯一证据，不随本体回位一起收走。
    /// </summary>
    internal sealed class ShadeStrikeField
    {
        private const int MaxCuts = 10;
        private const int CutSamples = 12;
        private const int MaxShards = 48;

        //斩痕节拍：1 帧过冲到位、5 帧内骨白冷掉、尾段 34 帧针尖向中心捏合
        private const int TearFrames = 5;
        private const int HealFrames = 34;
        private const int FadeFrames = 20;

        private readonly ShadeCut[] cuts = new ShadeCut[MaxCuts];
        private readonly ShadeShard[] shards = new ShadeShard[MaxShards];
        private readonly Vector2[] cutSpine = new Vector2[CutSamples];
        private readonly VertexPositionColorTexture[] cutVertices = new VertexPositionColorTexture[CutSamples * 2];
        private readonly VertexPositionColorTexture[] shardVertices = new VertexPositionColorTexture[MaxShards * 6];

        private int cutCount;
        private int shardCount;

        internal bool HasCuts => cutCount > 0;

        internal void Clear() {
            cutCount = 0;
            shardCount = 0;
        }

        internal void AddCut(Vector2 center, float angle, float halfLength, float halfWidth,
            int life, int delay = 0) {
            int slot = cutCount;
            if (slot >= MaxCuts) {
                //满了顶掉最老的一道
                slot = 0;
                for (int i = 1; i < cutCount; i++) {
                    if (cuts[i].Age > cuts[slot].Age) {
                        slot = i;
                    }
                }
            }
            else {
                cutCount++;
            }

            cuts[slot] = new ShadeCut {
                Center = center,
                Angle = angle,
                HalfLength = MathF.Max(halfLength, 8f),
                HalfWidth = MathF.Max(halfWidth, 6f),
                Bow = Main.rand.NextFloat(0.045f, 0.085f) * halfLength * (Main.rand.NextBool() ? 1f : -1f),
                Peak = Main.rand.NextFloat(0.28f, 0.40f),
                Flip = Main.rand.NextBool() ? 1f : -1f,
                Seed = Main.rand.NextFloat(),
                Age = 0,
                Life = Math.Max(life, 12),
                Delay = Math.Max(delay, 0),
            };
        }

        internal void AddShard(Vector2 pos, Vector2 velocity, float length, float width,
            float bone, int life) {
            if (shardCount >= MaxShards) {
                return;
            }
            shards[shardCount++] = new ShadeShard {
                Pos = pos,
                Vel = velocity,
                Angle = velocity.ToRotation() + Main.rand.NextFloat(-0.5f, 0.5f),
                Spin = Main.rand.NextFloat(-0.22f, 0.22f),
                Length = length,
                Width = width,
                Bone = bone,
                Age = 0,
                Life = life,
            };
        }

        internal void Update() {
            for (int i = cutCount - 1; i >= 0; i--) {
                if (cuts[i].Delay > 0) {
                    cuts[i].Delay--;
                    continue;
                }
                if (++cuts[i].Age >= cuts[i].Life) {
                    cuts[i] = cuts[--cutCount];
                }
            }

            for (int i = shardCount - 1; i >= 0; i--) {
                ref ShadeShard shard = ref shards[i];
                shard.Pos += shard.Vel;
                shard.Vel *= 0.93f;
                shard.Vel.Y += 0.055f;
                shard.Angle += shard.Spin;
                shard.Spin *= 0.955f;
                if (++shard.Age >= shard.Life) {
                    shards[i] = shards[--shardCount];
                }
            }
        }

        internal void DrawCuts(GraphicsDevice device, Effect effect, Texture2D noise) {
            if (cutCount == 0 || effect == null || noise == null) {
                return;
            }

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uColVoid"]?.SetValue(new Vector3(0.004f, 0.004f, 0.007f));
            effect.Parameters["uColBody"]?.SetValue(new Vector3(0.020f, 0.020f, 0.026f));
            effect.Parameters["uColFray"]?.SetValue(new Vector3(0.072f, 0.060f, 0.104f));
            effect.Parameters["uColRim"]?.SetValue(new Vector3(0.72f, 0.80f, 0.85f));

            for (int i = 0; i < cutCount; i++) {
                ref ShadeCut cut = ref cuts[i];
                if (cut.Delay > 0) {
                    continue;
                }
                BuildCutVertices(in cut);

                int healFrames = Math.Min(HealFrames, cut.Life - 4);
                int healStart = cut.Life - healFrames;
                int fadeFrames = Math.Min(FadeFrames, cut.Life / 2);
                int fadeStart = cut.Life - fadeFrames;

                float open = cut.Age == 0
                    ? 0.62f
                    : 1.06f - MathHelper.Clamp((cut.Age - 1) / 5f, 0f, 1f) * 0.08f;
                float tear = 1f - MathHelper.Clamp(cut.Age / (float)TearFrames, 0f, 1f);
                float heal = VaultUtils.EaseOutCubic(
                    MathHelper.Clamp((cut.Age - healStart) / (float)healFrames, 0f, 1f));
                float fade = 1f - MathHelper.Clamp((cut.Age - fadeStart) / (float)fadeFrames, 0f, 1f);

                effect.Parameters["uOpen"]?.SetValue(open);
                effect.Parameters["uHeal"]?.SetValue(heal);
                effect.Parameters["uSweepEdge"]?.SetValue(
                    MathHelper.Clamp(cut.Age / 3.2f, 0f, 1f) * 1.25f);
                effect.Parameters["uTear"]?.SetValue(tear * tear);
                effect.Parameters["uOpacity"]?.SetValue(fade);
                effect.Parameters["uFlip"]?.SetValue(cut.Flip);
                effect.Parameters["uSlide"]?.SetValue(
                    MathHelper.Clamp(cut.Age / 14f, 0f, 1f) * 0.055f);
                effect.Parameters["uPeak"]?.SetValue(cut.Peak);
                effect.Parameters["uSeed"]?.SetValue(cut.Seed * 7.3f);

                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, cutVertices, 0,
                        (CutSamples - 1) * 2);
                }
            }
        }

        private void BuildCutVertices(in ShadeCut cut) {
            Vector2 direction = cut.Angle.ToRotationVector2();
            Vector2 baseNormal = new(-direction.Y, direction.X);
            for (int i = 0; i < CutSamples; i++) {
                float t = i / (CutSamples - 1f);
                float signed = t * 2f - 1f;
                cutSpine[i] = cut.Center + direction * (signed * cut.HalfLength)
                    + baseNormal * (cut.Bow * (1f - signed * signed));
            }

            for (int i = 0; i < CutSamples; i++) {
                Vector2 tangent = i == 0
                    ? cutSpine[1] - cutSpine[0]
                    : i == CutSamples - 1
                        ? cutSpine[i] - cutSpine[i - 1]
                        : cutSpine[i + 1] - cutSpine[i - 1];
                Vector2 normal = tangent.SafeNormalize(direction).RotatedBy(MathHelper.PiOver2);
                float u = i / (CutSamples - 1f);
                cutVertices[i * 2] = new VertexPositionColorTexture(
                    (cutSpine[i] - normal * cut.HalfWidth).ToVector3(), Color.White, new Vector2(u, 0f));
                cutVertices[i * 2 + 1] = new VertexPositionColorTexture(
                    (cutSpine[i] + normal * cut.HalfWidth).ToVector3(), Color.White, new Vector2(u, 1f));
            }
        }

        /// <summary>影屑一次性攒成 TriangleList，交给肢体技法画（同一套毛口材质）</summary>
        internal void DrawShards(GraphicsDevice device, Effect effect, Texture2D noise, float opacity) {
            if (shardCount == 0 || effect == null || noise == null) {
                return;
            }

            int written = 0;
            for (int i = 0; i < shardCount; i++) {
                ref ShadeShard shard = ref shards[i];
                float life = MathHelper.Clamp(shard.Age / (float)shard.Life, 0f, 1f);
                float alpha = 1f - life * life * (3f - 2f * life);
                if (alpha <= 0.02f) {
                    continue;
                }

                float speed = shard.Vel.Length();
                float length = shard.Length * (0.72f + MathHelper.Clamp(speed * 0.13f, 0f, 1.1f));
                float bone = shard.Bone * MathHelper.Clamp(1f - shard.Age / 6f, 0f, 1f);
                Vector2 direction = shard.Angle.ToRotationVector2();
                Vector2 normal = new(-direction.Y, direction.X);
                Vector2 tail = shard.Pos - direction * (length * 0.5f);
                Vector2 tip = shard.Pos + direction * (length * 0.5f);
                float tailHalf = shard.Width;
                float tipHalf = shard.Width * 0.18f;
                Color color = new(bone, 0f, 0f, alpha);

                VertexPositionColorTexture v0 = new((tail - normal * tailHalf).ToVector3(), color, new Vector2(0f, 0f));
                VertexPositionColorTexture v1 = new((tail + normal * tailHalf).ToVector3(), color, new Vector2(0f, 1f));
                VertexPositionColorTexture v2 = new((tip - normal * tipHalf).ToVector3(), color, new Vector2(1f, 0f));
                VertexPositionColorTexture v3 = new((tip + normal * tipHalf).ToVector3(), color, new Vector2(1f, 1f));

                shardVertices[written++] = v0;
                shardVertices[written++] = v1;
                shardVertices[written++] = v2;
                shardVertices[written++] = v1;
                shardVertices[written++] = v3;
                shardVertices[written++] = v2;
            }

            if (written == 0) {
                return;
            }

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uOpacity"]?.SetValue(opacity);
            effect.Parameters["uRimFlash"]?.SetValue(0f);
            effect.Parameters["uTipSolid"]?.SetValue(1f);
            effect.Parameters["uFray"]?.SetValue(1.05f);
            effect.Parameters["uPhase"]?.SetValue(0.35f);
            effect.Parameters["uSeed"]?.SetValue(3.1f);
            effect.Parameters["uDissolve"]?.SetValue(0f);

            HeadlessShadeRig.UseTechnique(effect, "TechLimb");
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, shardVertices, 0, written / 3);
            }
        }

        /// <summary>
        /// 穿体命中的鬼影自有语汇：主刀 + 两道交叉切口写"撕成数段"，
        /// 落点几道极短毛刺就是骨白撕口爆点，影屑向来路反溅。钢/肉分流只改手感参数，不改材质。
        /// </summary>
        internal void SpawnImpact(Vector2 center, Vector2 direction, float sizeMul, bool steel) {
            float mainHalf = MathHelper.Clamp(96f * sizeMul, 70f, 190f);
            float mainAngle = direction.ToRotation();
            AddCut(center, mainAngle, mainHalf, 46f * sizeMul, 64);

            //交叉刀错开 2/4 帧落下，读作一次扑杀撕了好几道
            float crossA = mainAngle + MathHelper.ToRadians(Main.rand.NextFloat(48f, 68f));
            float crossB = mainAngle - MathHelper.ToRadians(Main.rand.NextFloat(48f, 68f));
            Vector2 offsetA = direction.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-18f, 18f);
            Vector2 offsetB = direction * Main.rand.NextFloat(-24f, 24f);
            AddCut(center + offsetA, crossA, mainHalf * 0.66f, 38f * sizeMul, 58, 2);
            AddCut(center + offsetB, crossB, mainHalf * 0.58f, 34f * sizeMul, 54, 4);

            int burrs = steel ? 5 : 3;
            for (int i = 0; i < burrs; i++) {
                Vector2 spot = center + Main.rand.NextVector2Circular(30f, 30f) * sizeMul;
                AddCut(spot, Main.rand.NextFloat(MathHelper.TwoPi),
                    Main.rand.NextFloat(16f, 32f) * sizeMul, 16f * sizeMul,
                    Main.rand.Next(22, 32), Main.rand.Next(0, 4));
            }

            int shardCountToSpawn = steel ? 20 : 15;
            for (int i = 0; i < shardCountToSpawn; i++) {
                Vector2 velocity = -direction.RotatedByRandom(steel ? 0.85f : 1.25f)
                    * Main.rand.NextFloat(steel ? 3.4f : 2.1f, steel ? 9.5f : 6.4f);
                Vector2 spot = center + Main.rand.NextVector2Circular(26f, 26f) * sizeMul;
                AddShard(spot, velocity,
                    Main.rand.NextFloat(22f, 54f) * sizeMul,
                    Main.rand.NextFloat(2.6f, 6.2f) * sizeMul,
                    Main.rand.NextBool(4) ? Main.rand.NextFloat(0.55f, 1f) : 0f,
                    Main.rand.Next(24, 42));
            }

            //低频那一层：无彩暗烟，跟高频影屑不是一个空间频率
            for (int i = 0; i < 6; i++) {
                Vector2 velocity = -direction * Main.rand.NextFloat(0.8f, 2.6f)
                    + Main.rand.NextVector2Circular(1.4f, 1.4f);
                PRTLoader.NewParticle<PRT_Smoke>(center + Main.rand.NextVector2Circular(24f, 24f),
                    velocity, new Color(17, 17, 22), Main.rand.NextFloat(0.09f, 0.16f))
                    ?.Configure(Main.rand.Next(24, 40), 0.46f, Main.rand.NextFloat(-0.025f, 0.025f));
            }
        }
    }
}
