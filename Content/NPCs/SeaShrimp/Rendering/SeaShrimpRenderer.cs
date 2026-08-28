using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering
{
    /// <summary>
    /// 海虾部件装配绘制。次序（后→前）：远触角→远足→远螯→尾扇→体节→头→近足→近螯→近触角。
    /// 每部件按自身所在位置取光照，全部消费骨架输出，无独立状态
    /// </summary>
    internal static class SeaShrimpRenderer
    {
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpHead")]
        private static Asset<Texture2D> HeadTex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpBodySegment1")]
        private static Asset<Texture2D> Seg1Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpBodySegment2")]
        private static Asset<Texture2D> Seg2Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpBodySegment3")]
        private static Asset<Texture2D> Seg3Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpTailFan")]
        private static Asset<Texture2D> TailTex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpClaw")]
        private static Asset<Texture2D> ClawTex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpClawArm1")]
        private static Asset<Texture2D> Arm1Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpClawArm2")]
        private static Asset<Texture2D> Arm2Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpLeg1")]
        private static Asset<Texture2D> Leg1Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpLeg2")]
        private static Asset<Texture2D> Leg2Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpLeg3")]
        private static Asset<Texture2D> Leg3Tex = null;

        //==================== 贴图锚点（2x 像素坐标，验收期可调）====================

        /// <summary>臂节1：肩端锚点与轴长（贴图轴向 +Y）</summary>
        private static readonly Vector2 Arm1Anchor = new(25f, 10f);
        private const float Arm1AxisLen = 102f;
        /// <summary>臂节2：肘端锚点与轴长</summary>
        private static readonly Vector2 Arm2Anchor = new(28f, 10f);
        private const float Arm2AxisLen = 88f;
        /// <summary>螯：承窝锚点（贴图右上的关节托），主体朝 -Y 下垂</summary>
        private static readonly Vector2 ClawAnchor = new(76f, 42f);

        /// <summary>步足贴图髋/足端锚点与轴角（按站位取 Leg1/2/3）</summary>
        private static readonly Vector2[] LegHip = [new(32f, 5f), new(38f, 6f), new(45f, 4f)];
        private static readonly Vector2[] LegTip = [new(4f, 15f), new(5f, 18f), new(5f, 13f)];

        /// <summary>晶簇主色</summary>
        internal static readonly Color CrystalBlue = new(86, 148, 255);

        /// <summary>死亡黯淡乘子（Draw 开头从上下文取样）</summary>
        private static float gloomMul = 1f;
        /// <summary>蜕壳提亮量</summary>
        private static float moltWash;

        /// <summary>体节贴图访问口（壳屑弹幕复用：旧壳就是它自己）</summary>
        internal static Texture2D SegmentTexture(int variant) => variant switch {
            0 => Seg1Tex?.Value,
            1 => Seg2Tex?.Value,
            _ => Seg3Tex?.Value,
        };

        private static Color LightAt(Vector2 world, float alpha) {
            Color c = Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));
            return c * (alpha * gloomMul);
        }

        /// <summary>主入口：PreDraw 调用，接管全部绘制</summary>
        public static void Draw(SpriteBatch sb, SeaShrimpBoss owner) {
            ShrimpSkeleton sk = owner.Skeleton;
            SeaShrimpStateContext ctx = owner.Context;
            if (sk == null || ctx == null || HeadTex == null) {
                return;
            }
            float alpha = MathHelper.Clamp(ctx.BodyAlpha, 0f, 1f);
            if (alpha < 0.02f) {
                return;
            }
            gloomMul = 1f - MathHelper.Clamp(ctx.DeathGloom, 0f, 1f) * 0.52f;
            moltWash = MathHelper.Clamp(ctx.Molted01, 0f, 1f);

            //远侧层（压暗贴后）
            DrawAntenna(sb, sk, 1, alpha * 0.72f);
            DrawLegRow(sb, sk, row: 1, alpha, dark: 0.55f);
            DrawArm(sb, sk, 1, alpha, dark: 0.62f);

            //体链（尾→头，头压顶）
            DrawSpinePart(sb, TailTex.Value, sk.Nodes[4], alpha, 1f,
                scaleOverride: new Vector2(0.72f + 0.42f * sk.TailFlare, 1f));
            DrawSpinePart(sb, Seg3Tex.Value, sk.Nodes[3], alpha, 1f);
            DrawSpinePart(sb, Seg2Tex.Value, sk.Nodes[2], alpha, 1f);
            DrawSpinePart(sb, Seg1Tex.Value, sk.Nodes[1], alpha, 1f);
            DrawSpinePart(sb, HeadTex.Value, sk.Nodes[0], alpha, 1f);

            //近侧层
            DrawLegRow(sb, sk, row: 0, alpha, dark: 0.88f);
            DrawArm(sb, sk, 0, alpha, dark: 1f);
            DrawAntenna(sb, sk, 0, alpha);

            DrawBeams(sb, ctx);
            DrawCrystalGlow(sb, sk, ctx);
        }

        /// <summary>
        /// 预警/指示线：暗底描边 + 晶蓝亮芯（暗层真alpha可遮挡，亮芯读方向），
        /// 虚线段朝目标滚动传达威胁走向
        /// </summary>
        private static void DrawBeams(SpriteBatch sb, SeaShrimpStateContext ctx) {
            if (ctx.Beams.Count == 0) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            foreach (SeaShrimpStateContext.BeamMark mark in ctx.Beams) {
                if (mark.Alpha < 0.03f) {
                    continue;
                }
                Color dark = new Color(8, 14, 30) * (mark.Alpha * 0.85f);
                Color core = Color.Lerp(CrystalBlue, Color.White, mark.Hot * 0.4f) * mark.Alpha;
                float rot = mark.Dir.ToRotation();

                if (mark.Dash > 0.5f) {
                    //滚动虚线：18px 段 + 14px 隙
                    const float SegLen = 18f;
                    const float GapLen = 14f;
                    float scroll = Main.GlobalTimeWrappedHourly * 130f % (SegLen + GapLen);
                    for (float d = scroll - SegLen; d < mark.Length; d += SegLen + GapLen) {
                        float a = MathF.Max(d, 0f);
                        float b = MathF.Min(d + SegLen, mark.Length);
                        if (b - a < 2f) {
                            continue;
                        }
                        Vector2 start = mark.From + mark.Dir * a;
                        sb.Draw(pixel, start - Main.screenPosition, src, dark, rot,
                            new Vector2(0f, 0.5f), new Vector2(b - a, 7f), SpriteEffects.None, 0f);
                        sb.Draw(pixel, start - Main.screenPosition, src, core, rot,
                            new Vector2(0f, 0.5f), new Vector2(b - a, 2.6f), SpriteEffects.None, 0f);
                    }
                }
                else {
                    sb.Draw(pixel, mark.From - Main.screenPosition, src, dark, rot,
                        new Vector2(0f, 0.5f), new Vector2(mark.Length, 8f), SpriteEffects.None, 0f);
                    sb.Draw(pixel, mark.From - Main.screenPosition, src, core, rot,
                        new Vector2(0f, 0.5f), new Vector2(mark.Length, 3f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>体链部件：贴图上方 = 前向</summary>
        private static void DrawSpinePart(SpriteBatch sb, Texture2D tex, ShrimpSkeleton.Node node,
            float alpha, float dark, Vector2? scaleOverride = null) {
            if (tex == null) {
                return;
            }
            Color color = LightAt(node.Pos, alpha).MultiplyRGB(new Color(dark, dark, dark));
            Vector2 scale = scaleOverride ?? Vector2.One;
            sb.Draw(tex, node.Pos - Main.screenPosition, null, color,
                node.Dir + MathHelper.PiOver2, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            //蜕壳裸晶：半透晶蓝水洗提亮体色
            if (moltWash > 0.05f) {
                Color wash = new Color(120, 175, 255, 70) * (moltWash * 0.32f * alpha * gloomMul);
                sb.Draw(tex, node.Pos - Main.screenPosition, null, wash,
                    node.Dir + MathHelper.PiOver2, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>一排步足：髋→足取向，轻微轴向伸缩容差</summary>
        private static void DrawLegRow(SpriteBatch sb, ShrimpSkeleton sk, int row, float alpha, float dark) {
            for (int i = 0; i < sk.Gait.Legs.Length; i++) {
                ref readonly ShrimpLegGait.Leg leg = ref sk.Gait.Legs[i];
                if (leg.Row != row || !leg.Init) {
                    continue;
                }
                Texture2D tex = leg.Station switch {
                    0 => Leg1Tex?.Value,
                    1 => Leg2Tex?.Value,
                    _ => Leg3Tex?.Value,
                };
                if (tex == null) {
                    continue;
                }
                Vector2 hipPx = LegHip[leg.Station];
                Vector2 tipPx = LegTip[leg.Station];
                Vector2 axisPx = tipPx - hipPx;
                float axisLen = axisPx.Length();
                float axisAngle = axisPx.ToRotation();

                Vector2 hip = sk.Gait.HipWorld(in leg, sk);
                Vector2 toFoot = leg.Foot - hip;
                float dist = toFoot.Length();
                if (dist < 4f) {
                    continue;
                }
                float rotation = toFoot.ToRotation() - axisAngle;
                float stretch = MathHelper.Clamp(dist / axisLen, 0.8f, 1.28f);
                Color color = LightAt(hip, alpha).MultiplyRGB(new Color(dark, dark, dark));
                sb.Draw(tex, hip - Main.screenPosition, null, color, rotation,
                    hipPx, stretch, SpriteEffects.None, 0f);
            }
        }

        /// <summary>单侧螯臂：臂节1（肩→肘）→ 臂节2（肘→腕）→ 螯体（腕锚承窝）</summary>
        private static void DrawArm(SpriteBatch sb, ShrimpSkeleton sk, int armIndex, float alpha, float dark) {
            TwoBoneSolve solve = sk.ArmSolves[armIndex];
            Texture2D arm1 = Arm1Tex?.Value;
            Texture2D arm2 = Arm2Tex?.Value;
            Texture2D claw = ClawTex?.Value;
            if (arm1 == null || arm2 == null || claw == null) {
                return;
            }
            Color darkMul = new(dark, dark, dark);

            //臂节1：贴图轴 +Y，肩锚在顶部
            Color c1 = LightAt(solve.Shoulder, alpha).MultiplyRGB(darkMul);
            sb.Draw(arm1, solve.Shoulder - Main.screenPosition, null, c1,
                solve.UpperDir.ToRotation() - MathHelper.PiOver2, Arm1Anchor,
                new Vector2(1f, SeaShrimpDirector.ArmBone1 / Arm1AxisLen), SpriteEffects.None, 0f);

            //臂节2
            Color c2 = LightAt(solve.Elbow, alpha).MultiplyRGB(darkMul);
            sb.Draw(arm2, solve.Elbow - Main.screenPosition, null, c2,
                solve.ForeDir.ToRotation() - MathHelper.PiOver2, Arm2Anchor,
                new Vector2(1f, SeaShrimpDirector.ArmBone2 / Arm2AxisLen), SpriteEffects.None, 0f);

            //螯体：承窝挂腕，钳开合以轻微绕锚旋开表达
            float open = sk.ClawOpen[armIndex] * 0.34f;
            Color c3 = LightAt(solve.Wrist, alpha).MultiplyRGB(darkMul);
            sb.Draw(claw, solve.Wrist - Main.screenPosition, null, c3,
                sk.ClawRot[armIndex] - MathHelper.PiOver2 + open, ClawAnchor,
                1f, SpriteEffects.None, 0f);
        }

        /// <summary>触角：verlet 折线，逐段渐细，尖端泛晶蓝</summary>
        private static void DrawAntenna(SpriteBatch sb, ShrimpSkeleton sk, int side, float alpha) {
            ShrimpVerletStrand strand = sk.Antennae[side];
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Color rootColor = new Color(40, 46, 70) * alpha;
            Color tipColor = new Color(96, 150, 255) * (alpha * 0.85f);
            int n = strand.Count;
            for (int i = 0; i < n - 1; i++) {
                Vector2 a = strand[i];
                Vector2 b = strand[i + 1];
                Vector2 d = b - a;
                float len = d.Length();
                if (len < 0.01f) {
                    continue;
                }
                float t = i / (float)(n - 1);
                float thickness = MathHelper.Lerp(4.4f, 1.4f, t);
                Color col = Color.Lerp(rootColor, tipColor, t * t).MultiplyRGB(LightAt(a, 1f));
                sb.Draw(pixel, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), col,
                    d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len + 0.6f, thickness),
                    SpriteEffects.None, 0f);
            }
        }

        /// <summary>晶簇加色层：眼、须冠主晶、尾扇晶的常燃脉冲 + 蓄力增益</summary>
        private static void DrawCrystalGlow(SpriteBatch sb, ShrimpSkeleton sk, SeaShrimpStateContext ctx) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || ctx.BodyAlpha < 0.05f) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 gOrigin = glow.Size() * 0.5f;
            float seedPulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + ctx.Npc.whoAmI);
            float baseGain = (0.3f + 0.7f * ctx.CrystalGlow) * ctx.BodyAlpha * seedPulse
                * (1f + ctx.Molted01 * 0.45f) * (1f - MathHelper.Clamp(ctx.DeathGloom, 0f, 1f));

            void Spot(Vector2 pos, float radius, float strength) {
                sb.Draw(glow, pos - Main.screenPosition, null, CrystalBlue * (strength * baseGain), 0f,
                    gOrigin, new Vector2(radius * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            Vector2 headFwd = sk.Nodes[0].Forward;
            //复眼
            Spot(sk.Nodes[0].Pos + headFwd * 14f, 26f, 0.5f);
            //须冠主晶（头节后缘的大蓝晶）
            Spot(sk.Nodes[0].Pos - headFwd * 62f, 34f, 0.66f);
            //尾扇双晶
            Spot(sk.Nodes[4].Pos + sk.Nodes[4].Forward * 12f, 28f, 0.5f);
            //双螯尖
            for (int a = 0; a < 2; a++) {
                Spot(sk.ClawTip(a), 18f, a == 0 ? 0.45f : 0.3f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
