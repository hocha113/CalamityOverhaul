using CalamityOverhaul.Common;
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
        /// <summary>螯：掌根锚点（左下粗壮处=臂关节位，二审网格实测）</summary>
        private static readonly Vector2 ClawAnchor = new(40f, 96f);
        /// <summary>螯贴图内在指向角：掌根→钳口。终裁 2026-08-28：-2.52
        /// （实机"向中间转90°"指令 + 离线 ±90° 双候选对比，齿口相向收拢者胜出；
        /// 镜像臂对此参数天然反号，单参数即完成双钳对称向心）。实机核对时只调这一个数</summary>
        private const float ClawTexAxis = -2.52f;

        /// <summary>步足贴图髋/足端锚点与轴角（按站位取 Leg1/2/3）</summary>
        private static readonly Vector2[] LegHip = [new(32f, 5f), new(38f, 6f), new(45f, 4f)];
        private static readonly Vector2[] LegTip = [new(4f, 15f), new(5f, 18f), new(5f, 13f)];

        /// <summary>晶簇主色</summary>
        internal static readonly Color CrystalBlue = new(86, 148, 255);

        /// <summary>死亡黯淡乘子（Draw 开头从上下文取样）</summary>
        private static float gloomMul = 1f;
        /// <summary>蜕壳提亮量</summary>
        private static float moltWash;

        //==================== 图鉴沙盒环境（默认跟随世界；BeginPortrait 期间由舞台接管）====================

        /// <summary>沙盒绘制环境：视口偏移、光照采样与批次重启参数</summary>
        internal sealed class PortraitEnv
        {
            /// <summary>视口偏移（沙盒场景坐标下给 Zero，映射交给批次矩阵）</summary>
            public Vector2 ViewOffset;
            /// <summary>光照采样（沙盒给固定环境光；剪影模式给黑）</summary>
            public Func<Vector2, Color> Light;
            /// <summary>中途 End/Begin 用的批次矩阵</summary>
            public Matrix BatchMatrix;
            /// <summary>中途 End/Begin 用的光栅态（沿用舞台裁剪）</summary>
            public RasterizerState Rasterizer;
        }

        private static PortraitEnv portrait;

        internal static void BeginPortrait(PortraitEnv env) => portrait = env;
        internal static void EndPortrait() => portrait = null;

        private static Vector2 ViewOff => portrait?.ViewOffset ?? Main.screenPosition;
        private static Matrix BatchMatrix => portrait?.BatchMatrix ?? Main.GameViewMatrix.TransformationMatrix;
        private static RasterizerState BatchRasterizer => portrait?.Rasterizer ?? RasterizerState.CullNone;
        private static SamplerState RestoreSampler => portrait != null ? SamplerState.LinearClamp : Main.DefaultSamplerState;

        /// <summary>体节贴图访问口（壳屑弹幕复用：旧壳就是它自己）</summary>
        internal static Texture2D SegmentTexture(int variant) => variant switch {
            0 => Seg1Tex?.Value,
            1 => Seg2Tex?.Value,
            _ => Seg3Tex?.Value,
        };

        private static Color LightAt(Vector2 world, float alpha) {
            Color c = portrait != null ? portrait.Light(world)
                : Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));
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

            //残影拖影：爆发段的同素材水影，压在全部部件之后
            DrawAfterimages(sb, owner, ctx, alpha);

            DrawBodyParts(sb, sk, alpha);

            DrawBeams(sb, ctx);
            DrawCrystalGlow(sb, sk, ctx);
            DrawRingEvents(sb, ctx);
            DrawColumnEvents(sb, ctx);
        }

        /// <summary>
        /// 图鉴沙盒入口：只画本体装配与晶簇辉光（残影/预警/事件层是战斗专属）。
        /// 调用方需先 BeginPortrait 接管环境，画完 EndPortrait 归还
        /// </summary>
        internal static void DrawPortrait(SpriteBatch sb, ShrimpSkeleton sk, SeaShrimpStateContext ctx, bool glow) {
            if (sk == null || ctx == null || HeadTex == null) {
                return;
            }
            float alpha = MathHelper.Clamp(ctx.BodyAlpha, 0f, 1f);
            if (alpha < 0.02f) {
                return;
            }
            gloomMul = 1f - MathHelper.Clamp(ctx.DeathGloom, 0f, 1f) * 0.52f;
            moltWash = MathHelper.Clamp(ctx.Molted01, 0f, 1f);
            DrawBodyParts(sb, sk, alpha);
            if (glow) {
                DrawCrystalGlow(sb, sk, ctx);
            }
        }

        /// <summary>本体装配（后→前）：远足→体链→近足→双螯</summary>
        private static void DrawBodyParts(SpriteBatch sb, ShrimpSkeleton sk, float alpha) {
            //远侧层（压暗贴后）
            DrawLegRow(sb, sk, row: 1, alpha, dark: 0.55f);

            //体链（尾→头，头压顶）；尾扇锚在前缘，弯折时前缘始终咬进体节3 不脱节
            DrawSpinePart(sb, TailTex.Value, sk.Nodes[4], alpha, 1f,
                scaleOverride: new Vector2(0.72f + 0.42f * sk.TailFlare, 1f),
                originOverride: new Vector2(83f, 16f));
            DrawSpinePart(sb, Seg3Tex.Value, sk.Nodes[3], alpha, 1f);
            DrawSpinePart(sb, Seg2Tex.Value, sk.Nodes[2], alpha, 1f);
            DrawSpinePart(sb, Seg1Tex.Value, sk.Nodes[1], alpha, 1f);
            DrawSpinePart(sb, HeadTex.Value, sk.Nodes[0], alpha, 1f);

            //近侧层
            DrawLegRow(sb, sk, row: 0, alpha, dark: 0.88f);

            //双螯压最上层：手撑向玩家所在的屏幕平面，是这套分镜的前景主角
            DrawArm(sb, sk, 1, alpha, dark: 0.68f);
            DrawArm(sb, sk, 0, alpha, dark: 1f);
        }

        /// <summary>一次性冲击环事件：ShockRing 参数化环展开（撕裂缘、非数学圆），消费后自清</summary>
        private static void DrawRingEvents(SpriteBatch sb, SeaShrimpStateContext ctx) {
            for (int i = ctx.RingEvents.Count - 1; i >= 0; i--) {
                SeaShrimpStateContext.RingEvent e = ctx.RingEvents[i];
                float t = (Main.GameUpdateCount - e.Birth) / (float)e.Life;
                if (t >= 1f) {
                    ctx.RingEvents.RemoveAt(i);
                    continue;
                }
                float ease = 1f - (1f - t) * (1f - t);
                float radius = e.FinalR * MathHelper.Lerp(0.12f, 1f, ease);
                float alpha = (1f - t) * 0.85f * gloomMul;
                ShockRingDraw.Draw(sb, e.Pos, radius, MathHelper.Lerp(24f, 9f, t),
                    SeaShrimpVFX.Foam, SeaShrimpVFX.Glow, SeaShrimpVFX.Body, alpha,
                    tearPx: 14f, squish: e.Squish, innerGlow: t < 0.35f ? 0.3f : 0f, timeSeed: e.Birth * 0.31f);
            }
        }

        /// <summary>
        /// 一次性水柱事件：FishronTornado 换深渊色板，起-盛-散包络，底锚不动
        /// （柱从地里长出来，quad 大幅宽于名义柱径，撕裂轮廓留在画布内侧）
        /// </summary>
        private static void DrawColumnEvents(SpriteBatch sb, SeaShrimpStateContext ctx) {
            if (ctx.ColumnEvents.Count == 0) {
                return;
            }
            Effect fx = EffectLoader.FishronTornado?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (fx == null || noise == null || pixel == null) {
                ctx.ColumnEvents.Clear();
                return;
            }
            for (int i = ctx.ColumnEvents.Count - 1; i >= 0; i--) {
                SeaShrimpStateContext.ColumnEvent e = ctx.ColumnEvents[i];
                float t = (Main.GameUpdateCount - e.Birth) / (float)e.Life;
                if (t >= 1f) {
                    ctx.ColumnEvents.RemoveAt(i);
                    continue;
                }
                //前 25% 快速蹿起，45% 后向消散
                float rise = MathHelper.Clamp(t / 0.25f, 0f, 1f);
                rise = 1f - (1f - rise) * (1f - rise);
                float fade = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
                float drawH = e.Height * 1.3f * MathHelper.Lerp(0.3f, 1f, rise);
                float drawW = e.Width * 3f;

                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(fade * 1.25f * gloomMul);
                fx.Parameters["uGrade"]?.SetValue(1f);
                fx.Parameters["uSeed"]?.SetValue(e.Seed);
                fx.Parameters["uDeepColor"]?.SetValue(SeaShrimpVFX.Deep.ToVector3());
                fx.Parameters["uSeaColor"]?.SetValue(SeaShrimpVFX.Body.ToVector3());
                fx.Parameters["uFoamColor"]?.SetValue(SeaShrimpVFX.Foam.ToVector3());

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, RestoreSampler,
                    DepthStencilState.None, BatchRasterizer, fx, BatchMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                fx.CurrentTechnique.Passes[0].Apply();

                Vector2 drawCenter = e.Base - new Vector2(0f, drawH * 0.5f);
                sb.Draw(pixel, drawCenter - ViewOff, null, Color.White, 0f,
                    pixel.Size() * 0.5f, new Vector2(drawW / pixel.Width, drawH / pixel.Height),
                    SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, RestoreSampler,
                    DepthStencilState.None, portrait?.Rasterizer ?? Main.Rasterizer, null, BatchMatrix);
            }
        }

        /// <summary>
        /// 残影：位姿环里的主剪影部件（体链+双螯）以深渊水色低透明重绘，
        /// 同素材残影（契约5），强度由 AfterimageStrength 门控——只在爆发段出现。
        /// 旧影更暗更沉，新影带一线青,读作被身体拖开的水
        /// </summary>
        private static void DrawAfterimages(SpriteBatch sb, SeaShrimpBoss owner, SeaShrimpStateContext ctx, float alpha) {
            float strength = ctx.AfterimageStrength;
            if (strength <= 0.06f) {
                return;
            }
            Texture2D arm1 = Arm1Tex?.Value;
            Texture2D arm2 = Arm2Tex?.Value;
            Texture2D claw = ClawTex?.Value;
            Texture2D tail = TailTex?.Value;
            if (tail == null) {
                return;
            }
            //旧→新叠画：新影盖旧影
            for (int age = ShrimpPoseTrail.Slots - 1; age >= 0; age--) {
                ShrimpPoseTrail.Snapshot snap = owner.PoseTrail.Get(age);
                if (snap == null) {
                    continue;
                }
                float slotFade = age switch { 0 => 0.34f, 1 => 0.24f, 2 => 0.16f, _ => 0.10f };
                float a = strength * slotFade * alpha * gloomMul;
                if (a <= 0.02f) {
                    continue;
                }
                //深水拖影底色，最新一层向青辉抬一点
                Color tint = Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, age == 0 ? 0.75f : 0.35f) * a;

                //体链（尾→头）
                DrawGhostPart(sb, tail, snap.NodePos[4], snap.NodeDir[4], tint,
                    new Vector2(0.72f + 0.42f * snap.TailFlare, 1f), new Vector2(83f, 16f));
                DrawGhostPart(sb, Seg3Tex?.Value, snap.NodePos[3], snap.NodeDir[3], tint, Vector2.One, null);
                DrawGhostPart(sb, Seg2Tex?.Value, snap.NodePos[2], snap.NodeDir[2], tint, Vector2.One, null);
                DrawGhostPart(sb, Seg1Tex?.Value, snap.NodePos[1], snap.NodeDir[1], tint, Vector2.One, null);
                DrawGhostPart(sb, HeadTex?.Value, snap.NodePos[0], snap.NodeDir[0], tint, Vector2.One, null);

                //双螯（前景主角，残影必须带上它）
                if (arm1 == null || arm2 == null || claw == null) {
                    continue;
                }
                for (int arm = 1; arm >= 0; arm--) {
                    bool mirror = arm == 0;
                    SpriteEffects fx = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    sb.Draw(arm1, snap.Shoulder[arm] - ViewOff, null, tint,
                        snap.UpperRot[arm] - MathHelper.PiOver2, Arm1Anchor,
                        new Vector2(1f, SeaShrimpDirector.ArmBone1 / Arm1AxisLen), fx, 0f);
                    sb.Draw(arm2, snap.Elbow[arm] - ViewOff, null, tint,
                        snap.ForeRot[arm] - MathHelper.PiOver2, Arm2Anchor,
                        new Vector2(1f, SeaShrimpDirector.ArmBone2 / Arm2AxisLen), fx, 0f);
                    float texAxis = mirror ? MathHelper.Pi - ClawTexAxis : ClawTexAxis;
                    Vector2 anchor = mirror ? new Vector2(claw.Width - ClawAnchor.X, ClawAnchor.Y) : ClawAnchor;
                    sb.Draw(claw, snap.Wrist[arm] - ViewOff, null, tint,
                        snap.ClawRot[arm] - texAxis, anchor, 1f, fx, 0f);
                }
            }
        }

        private static void DrawGhostPart(SpriteBatch sb, Texture2D tex, Vector2 pos, float dir,
            Color tint, Vector2 scale, Vector2? originOverride) {
            if (tex == null) {
                return;
            }
            Vector2 origin = originOverride ?? tex.Size() * 0.5f;
            sb.Draw(tex, pos - ViewOff, null, tint,
                dir + MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
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
                        sb.Draw(pixel, start - ViewOff, src, dark, rot,
                            new Vector2(0f, 0.5f), new Vector2(b - a, 7f), SpriteEffects.None, 0f);
                        sb.Draw(pixel, start - ViewOff, src, core, rot,
                            new Vector2(0f, 0.5f), new Vector2(b - a, 2.6f), SpriteEffects.None, 0f);
                    }
                }
                else {
                    sb.Draw(pixel, mark.From - ViewOff, src, dark, rot,
                        new Vector2(0f, 0.5f), new Vector2(mark.Length, 8f), SpriteEffects.None, 0f);
                    sb.Draw(pixel, mark.From - ViewOff, src, core, rot,
                        new Vector2(0f, 0.5f), new Vector2(mark.Length, 3f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>体链部件：贴图上方 = 前向；originOverride 可把锚点移到贴图前缘（尾扇咬合用）</summary>
        private static void DrawSpinePart(SpriteBatch sb, Texture2D tex, ShrimpSkeleton.Node node,
            float alpha, float dark, Vector2? scaleOverride = null, Vector2? originOverride = null) {
            if (tex == null) {
                return;
            }
            Color color = LightAt(node.Pos, alpha).MultiplyRGB(new Color(dark, dark, dark));
            Vector2 scale = scaleOverride ?? Vector2.One;
            Vector2 origin = originOverride ?? tex.Size() * 0.5f;
            sb.Draw(tex, node.Pos - ViewOff, null, color,
                node.Dir + MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
            //蜕壳裸晶：半透晶蓝水洗提亮体色
            if (moltWash > 0.05f) {
                Color wash = new Color(120, 175, 255, 70) * (moltWash * 0.32f * alpha * gloomMul);
                sb.Draw(tex, node.Pos - ViewOff, null, wash,
                    node.Dir + MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
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
                sb.Draw(tex, hip - ViewOff, null, color, rotation,
                    hipPx, stretch, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 单侧螯臂：臂节1（肩→肘）→ 臂节2（肘→腕）→ 螯体（腕锚承窝）。
        /// 单贴图有手性：远侧臂（armIndex 1）整条水平镜像，双钳才会对称地咬向前方中线
        /// （离线装配 A/B 方案对比实测）。臂节锚点横向居中且轴向 +Y，镜像下只翻位图；
        /// 螯需要镜像锚点（w-x）、镜像轴角（π-axis）与开合反号
        /// </summary>
        private static void DrawArm(SpriteBatch sb, ShrimpSkeleton sk, int armIndex, float alpha, float dark) {
            TwoBoneSolve solve = sk.ArmSolves[armIndex];
            Texture2D arm1 = Arm1Tex?.Value;
            Texture2D arm2 = Arm2Tex?.Value;
            Texture2D claw = ClawTex?.Value;
            if (arm1 == null || arm2 == null || claw == null) {
                return;
            }
            Color darkMul = new(dark, dark, dark);
            //镜像给近侧臂（实机裁决 2026-08-28：轴角 -0.95 下左右手性与旧对比结论对调）
            bool mirror = armIndex == 0;
            SpriteEffects fx = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //臂节1：贴图轴 +Y，肩锚在顶部
            Color c1 = LightAt(solve.Shoulder, alpha).MultiplyRGB(darkMul);
            sb.Draw(arm1, solve.Shoulder - ViewOff, null, c1,
                solve.UpperDir.ToRotation() - MathHelper.PiOver2, Arm1Anchor,
                new Vector2(1f, SeaShrimpDirector.ArmBone1 / Arm1AxisLen), fx, 0f);

            //臂节2
            Color c2 = LightAt(solve.Elbow, alpha).MultiplyRGB(darkMul);
            sb.Draw(arm2, solve.Elbow - ViewOff, null, c2,
                solve.ForeDir.ToRotation() - MathHelper.PiOver2, Arm2Anchor,
                new Vector2(1f, SeaShrimpDirector.ArmBone2 / Arm2AxisLen), fx, 0f);

            //螯体：承窝挂腕，贴图内在轴角对齐世界指向（ClawRot=尖端朝向），钳开合绕锚微旋
            float texAxis = mirror ? MathHelper.Pi - ClawTexAxis : ClawTexAxis;
            Vector2 anchor = mirror ? new Vector2(claw.Width - ClawAnchor.X, ClawAnchor.Y) : ClawAnchor;
            float open = sk.ClawOpen[armIndex] * 0.34f * (mirror ? -1f : 1f);
            Color c3 = LightAt(solve.Wrist, alpha).MultiplyRGB(darkMul);
            sb.Draw(claw, solve.Wrist - ViewOff, null, c3,
                sk.ClawRot[armIndex] - texAxis + open, anchor,
                1f, fx, 0f);
        }

        /// <summary>晶簇加色层：眼、须冠主晶、尾扇晶的常燃脉冲 + 蓄力增益</summary>
        private static void DrawCrystalGlow(SpriteBatch sb, ShrimpSkeleton sk, SeaShrimpStateContext ctx) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || ctx.BodyAlpha < 0.05f) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, BatchRasterizer, null, BatchMatrix);

            Vector2 gOrigin = glow.Size() * 0.5f;
            float seedPulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + ctx.Npc.whoAmI);
            float baseGain = (0.3f + 0.7f * ctx.CrystalGlow) * ctx.BodyAlpha * seedPulse
                * (1f + ctx.Molted01 * 0.45f) * (1f - MathHelper.Clamp(ctx.DeathGloom, 0f, 1f));

            void Spot(Vector2 pos, float radius, float strength) {
                sb.Draw(glow, pos - ViewOff, null, CrystalBlue * (strength * baseGain), 0f,
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
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, RestoreSampler,
                DepthStencilState.None, BatchRasterizer, null, BatchMatrix);
        }
    }
}
