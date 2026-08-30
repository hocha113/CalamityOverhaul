using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.OtherMods.BossChecklist;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering
{
    /// <summary>
    /// 渊晶海虾图鉴沙盒：headless 骨架在深渊水体里持续巡游。
    /// 独立 <see cref="ShrimpSkeleton"/> + 桩上下文（挂 dummy NPC），运动纯脚本驱动，
    /// 绘制复用 <see cref="SeaShrimpRenderer"/> 的装配管线（BeginPortrait 接管光照与矩阵）
    /// </summary>
    internal sealed class SeaShrimpPortraitActor : BossPortraitActor
    {
        public static SeaShrimpPortraitActor Instance => instance ??= new SeaShrimpPortraitActor();
        private static SeaShrimpPortraitActor instance;

        private readonly ShrimpSkeleton skeleton = new();
        private SeaShrimpStateContext ctx;
        private Vector2 headPos;
        private float heading;

        /// <summary>深渊环境光：偏冷微亮，暗底上体表贴图仍可读</summary>
        private static readonly Color AbyssAmbient = new(150, 178, 216);

        public override Vector2 SceneHalfSize => new(330f, 255f);

        protected override void Reset() {
            ctx = new SeaShrimpStateContext {
                Npc = new NPC(),
                BodyAlpha = 1f,
            };
            skeleton.BindSeed(2.61f);
            headPos = new Vector2(60f, -10f);
            heading = MathHelper.Pi;
            skeleton.Rebuild(headPos, heading);
        }

        protected override void Update(float dt) {
            //骨架接口以帧为时间单位（60fps 基准）
            float frames = dt * 60f;
            float t = Time;

            //利萨茹巡游：横向为主的 8 字，读作在水体里从容折返
            Vector2 want = new(
                MathF.Sin(t * 0.40f) * 168f,
                MathF.Sin(t * 0.80f + 1.35f) * 58f - 8f);
            Vector2 delta = want - headPos;
            Vector2 vel = delta * 0.05f;
            float speed = vel.Length();
            const float MaxSpeed = 7.5f;
            if (speed > MaxSpeed) {
                vel *= MaxSpeed / speed;
                speed = MaxSpeed;
            }
            headPos += vel * frames;
            if (speed > 0.25f) {
                heading = heading.AngleLerp(vel.ToRotation(), MathHelper.Clamp(0.075f * frames, 0f, 1f));
            }

            //姿态通道：每帧复位再断言（与战斗态同一套上下文语义）
            ctx.BeginFrameDefaults();
            ctx.WaveGain = 1.2f;
            ctx.TailFlare = 0.35f + 0.28f * (0.5f + 0.5f * MathF.Sin(t * 1.05f));
            ctx.SpineCurl = MathF.Sin(t * 0.47f) * 0.14f;
            ctx.CrystalGlow = 0.42f + 0.38f * (0.5f + 0.5f * MathF.Sin(t * 0.85f));

            //周期性亮螯：巡游间隙双螯前伸开钳展示，晶光同步拉高
            float cyc = t % 8.5f;
            if (cyc is > 5.9f and < 7.3f) {
                float k = MathF.Sin((cyc - 5.9f) / 1.4f * MathHelper.Pi);
                Vector2 fwd = heading.ToRotationVector2();
                for (int a = 0; a < 2; a++) {
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Hold,
                        Target = headPos + fwd * (150f + 55f * k) + skeleton.Lateral(a) * 66f,
                        Spring = 0.2f,
                        Damping = 0.7f,
                        ClawOpen = 0.15f + 0.75f * k,
                    };
                }
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.55f + 0.45f * k);
            }

            skeleton.Update(ctx, headPos, heading, vel.SafeNormalize(Vector2.Zero), speed);
        }

        public override void Draw(SpriteBatch sb, in PortraitFrame frame) {
            DrawBackdrop(sb, in frame);

            //lambda 不能捕获 in 参数：光照色先落地成局部值
            Color light = frame.Masked ? Color.Black : AbyssAmbient;
            SeaShrimpRenderer.PortraitEnv env = new() {
                ViewOffset = Vector2.Zero,
                Light = _ => light,
                BatchMatrix = frame.WorldMatrix,
                Rasterizer = frame.Scissor,
            };
            SeaShrimpRenderer.BeginPortrait(env);
            try {
                SeaShrimpRenderer.DrawPortrait(sb, skeleton, ctx, glow: !frame.Masked);
            }
            finally {
                SeaShrimpRenderer.EndPortrait();
            }
        }

        /// <summary>深渊衬底：垂直渐变水体 + 生物光薄雾 + 缓升气泡群</summary>
        private void DrawBackdrop(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = frame.SceneHalf;
            //剪影模式：暗水衬底保留但压暗（不透明压暗，别让图鉴纸底透出来），黑剪影浮在其上
            float dimMul = frame.Masked ? 0.45f : 1f;

            const int Bands = 24;
            float bandH = half.Y * 2f / Bands;
            for (int i = 0; i < Bands; i++) {
                float t = i / (float)(Bands - 1);
                Color grad = Color.Lerp(new Color(14, 32, 58), new Color(3, 7, 16), t);
                Color c = new((int)(grad.R * dimMul), (int)(grad.G * dimMul), (int)(grad.B * dimMul), 255);
                sb.Draw(pixel, new Vector2(-half.X, -half.Y + i * bandH), src, c, 0f,
                    Vector2.Zero, new Vector2(half.X * 2f, bandH + 1f), SpriteEffects.None, 0f);
            }

            if (frame.Masked) {
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            //生物光薄雾（SoftGlow 黑底图，A=0 走加色读数）
            sb.Draw(glow, new Vector2(0f, -half.Y * 0.35f), null,
                SeaShrimpRenderer.CrystalBlue with { A = 0 } * 0.16f, 0f,
                glow.Size() * 0.5f,
                new Vector2(half.X * 2.4f / glow.Width, half.Y * 1.7f / glow.Height),
                SpriteEffects.None, 0f);

            //缓升气泡群：黄金角确定性散布，纵向循环，中段最亮
            for (int i = 0; i < 14; i++) {
                float ph = i * 2.399f;
                float x = MathF.Sin(ph) * half.X * 0.92f + MathF.Sin(Time * 0.35f + ph * 3.1f) * 14f;
                float rise = (Time * (16f + 7f * MathF.Sin(ph * 5f)) + ph * 97f) % (half.Y * 2f);
                float y = half.Y - rise;
                float size = 5f + 4.5f * (0.5f + 0.5f * MathF.Sin(ph * 7f));
                float a = 0.30f * MathF.Sin(rise / (half.Y * 2f) * MathHelper.Pi);
                sb.Draw(glow, new Vector2(x, y), null,
                    SeaShrimpVFX.Foam with { A = 0 } * a, 0f, glow.Size() * 0.5f,
                    size * 2f / glow.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
