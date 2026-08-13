using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering
{
    /// <summary>藤蔓绘制参数</summary>
    internal struct VineParams
    {
        /// <summary>静息长度，短于实际距离=绷紧，长于=下垂</summary>
        public float RestLength;
        /// <summary>根部半宽 px</summary>
        public float HalfWidth;
        /// <summary>张力 0~1</summary>
        public float Taut;
        /// <summary>蓄力脉冲 0~1</summary>
        public float Pulse;
        /// <summary>行波方向 +1根→梢 -1梢→根</summary>
        public float PulseDir;
        /// <summary>生长进度 0~1</summary>
        public float Grow;
        public float Fade;
        public bool Phase2;
        public float Seed;

        public static VineParams Default => new() {
            RestLength = 0f,
            HalfWidth = 11f,
            Taut = 0f,
            Pulse = 0f,
            PulseDir = -1f,
            Grow = 1f,
            Fade = 1f,
            Phase2 = false,
            Seed = 0.37f,
        };
    }

    /// <summary>活体藤蔓条带绘制：贝塞尔垂度+着色器纤维/荧光脉络，无着色器回退原版链贴图</summary>
    internal static class PlanteraVineRenderer
    {
        private const int Samples = 16;

        /// <summary>蓄力脉冲通道，钩爪whoAmI 索引，客户端表现</summary>
        private static readonly float[] pulseChannel = new float[Main.maxNPCs];

        public static void PushPulse(int npcWhoAmI, float strength) {
            if (VaultUtils.isServer || npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
                return;
            }
            pulseChannel[npcWhoAmI] = MathHelper.Clamp(Math.Max(pulseChannel[npcWhoAmI], strength), 0f, 1f);
        }

        /// <summary>读取并衰减脉冲，绘制端每帧调用</summary>
        public static float ReadAndDecayPulse(int npcWhoAmI) {
            if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
                return 0f;
            }
            float value = pulseChannel[npcWhoAmI];
            pulseChannel[npcWhoAmI] = value * 0.9f;
            return value;
        }

        public static void Clear() => Array.Clear(pulseChannel, 0, pulseChannel.Length);

        /// <summary>画一根藤：SpriteBatch 必须处于激活态(会End/重Begin还原NPC批参数)</summary>
        public static void DrawVine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, in VineParams p) {
            float dist = Vector2.Distance(start, end);
            if (dist < 8f || p.Fade <= 0.01f) {
                return;
            }

            Effect effect = EffectLoader.PlanteraVine?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            if (effect == null || noise == null) {
                DrawFallbackChain(spriteBatch, start, end, p);
                return;
            }

            spriteBatch.End();
            DrawVineStrip(effect, noise, start, end, p);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无活动批版本(顶点层直画)，供弹幕图元层调用</summary>
        public static void DrawVineRaw(Vector2 start, Vector2 end, in VineParams p) {
            float dist = Vector2.Distance(start, end);
            if (dist < 8f || p.Fade <= 0.01f) {
                return;
            }

            Effect effect = EffectLoader.PlanteraVine?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }
            DrawVineStrip(effect, noise, start, end, p);
        }

        private static void DrawVineStrip(Effect effect, Texture2D noise, Vector2 start, Vector2 end, in VineParams p) {
            float dist = Vector2.Distance(start, end);

            //垂度：松弛下垂，绷紧拉直
            float slack = Math.Max(0f, p.RestLength - dist);
            float sag = MathHelper.Clamp(slack * 0.45f + 14f, 8f, 180f) * (1f - p.Taut * 0.86f);
            Vector2 dir = (end - start) / dist;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //垂向偏重力，横向占小份保持有机弯曲
            Vector2 sagVec = (Vector2.UnitY * 0.8f + perp * 0.2f * (float)Math.Sin(p.Seed * 17f)) * sag;
            Vector2 control = (start + end) * 0.5f + sagVec;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[(Samples + 1) * 2];
            for (int i = 0; i <= Samples; i++) {
                float t = i / (float)Samples;
                //二次贝塞尔
                Vector2 pos = Vector2.Lerp(Vector2.Lerp(start, control, t), Vector2.Lerp(control, end, t), t);
                Vector2 tangent = (Vector2.Lerp(control, end, t) - Vector2.Lerp(start, control, t));
                Vector2 n = tangent.SafeNormalize(perp).RotatedBy(MathHelper.PiOver2);

                //quad 比目标宽度大一档，边缘毛口由着色器啃出
                float halfW = p.HalfWidth * 1.3f;

                Color light = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f));
                //藤蔓自持最低亮度，深丛林不至于全黑
                Color vColor = new(
                    (byte)Math.Max(light.R, (byte)70),
                    (byte)Math.Max(light.G, (byte)80),
                    (byte)Math.Max(light.B, (byte)60),
                    (byte)255);

                verts[i * 2] = new VertexPositionColorTexture((pos + n * halfW).ToVector3(), vColor, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pos - n * halfW).ToVector3(), vColor, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(p.Fade);
            effect.Parameters["uTaut"]?.SetValue(p.Taut);
            effect.Parameters["uPulse"]?.SetValue(p.Pulse);
            effect.Parameters["uPulseDir"]?.SetValue(p.PulseDir);
            effect.Parameters["uGrow"]?.SetValue(p.Grow);
            effect.Parameters["uPhase2"]?.SetValue(p.Phase2 ? 1f : 0f);
            effect.Parameters["seed"]?.SetValue(p.Seed);
            //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, Samples * 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>回退：原版链贴图沿贝塞尔铺(镜像原版画藤)</summary>
        private static void DrawFallbackChain(SpriteBatch spriteBatch, Vector2 start, Vector2 end, in VineParams p) {
            Texture2D chain = TextureAssets.Chain26.Value;
            float dist = Vector2.Distance(start, end);
            float slack = Math.Max(0f, p.RestLength - dist);
            float sag = MathHelper.Clamp(slack * 0.45f + 14f, 8f, 180f) * (1f - p.Taut * 0.86f);
            Vector2 control = (start + end) * 0.5f + Vector2.UnitY * sag;

            int links = Math.Max((int)(dist / 16f), 2);
            Vector2 prev = start;
            for (int i = 1; i <= links; i++) {
                float t = i / (float)links * p.Grow;
                Vector2 pos = Vector2.Lerp(Vector2.Lerp(start, control, t), Vector2.Lerp(control, end, t), t);
                float rot = (pos - prev).ToRotation() - MathHelper.PiOver2;
                Color color = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f)) * p.Fade;
                spriteBatch.Draw(chain, prev - Main.screenPosition, null, color, rot,
                    new Vector2(chain.Width * 0.5f, 0f), 1f, SpriteEffects.None, 0f);
                prev = pos;
            }
        }
    }
}
