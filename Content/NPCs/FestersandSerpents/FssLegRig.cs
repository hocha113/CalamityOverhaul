using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>
    /// 变异步态系统（纯表现层，自 BssLegRig 演化）。各端从已同步的体节位置本地重建，
    /// 不入网络包，无 gameplay 碰撞。
    ///
    /// 编制：4 髋站 x 两侧翻缘 = 8 条两节长肢（体格放大，触及 ~140px），
    /// 髋站沿 26 节长躯铺开。运动模型同荒花沙蟒（划桨往复 + 地面线钳制 + 抓地拍
    /// 回填 StationBob），此外叠加变异性格：足端持续痉挛微颤（病态高频小抖，
    /// 与划桨大周期分层），关节偶发灵液滴漏。
    /// 贴图借用 BSS 腿骨素材，绘制统一过 SkinMul 手染压向坏死紫。
    /// </summary>
    internal class FssLegRig
    {
        /// <summary>髋站数（每站两侧各一条腿；死亡演出逐站瘫软按此计数）</summary>
        public const int LegCount = 4;
        /// <summary>腿总数（li = 站号*2 + 侧位，偶数 = +法线侧，奇数 = −法线侧）</summary>
        private const int TotalLegs = LegCount * 2;

        /// <summary>髋锚体节链序（26 节长躯拉开间距；落步下沉采样共用）</summary>
        internal static readonly int[] StationOrdinals = { 1, 5, 9, 13 };
        /// <summary>各站划水倾角修饰（去机械感的性格差）</summary>
        private static readonly float[] StrokeAccent = { 0.08f, -0.06f, 0.03f, -0.09f };

        public const float UpperLen = 66f;
        public const float LowerLen = 76f;
        private const float MaxReach = UpperLen + LowerLen - 2f;

        /// <summary>功率段（快耙）占周期比例</summary>
        internal const float PowerFraction = 0.32f;
        /// <summary>划水前探倾角（自法线向体前，弧度）</summary>
        private const float TiltForward = 0.9f;
        /// <summary>划水后耙倾角（自法线向体后，弧度）</summary>
        private const float TiltBack = 0.85f;
        /// <summary>恢复段最大离地余隙（抬腿过障的高度）</summary>
        private const float RecoveryClearance = 30f;
        /// <summary>站间相位差：划水波前→后传的蜈蚣节律</summary>
        private const float StationLag = MathHelper.TwoPi * 0.22f;
        /// <summary>落步下沉的绘制像素基数（体节/头/髋压缩共用）</summary>
        internal const float StationDipPx = 7f;
        /// <summary>痉挛微颤振幅（像素；变异性格的常开底噪）</summary>
        private const float TwitchAmp = 2.4f;

        private struct Leg
        {
            public Vector2 Hip;
            public Vector2 Foot;
            /// <summary>体后方向（−链向，随体节逐帧刷新；膝弯统一朝体后）</summary>
            public Vector2 Back;
            /// <summary>足端已初始化（防默认 (0,0) 世界原点拉丝）</summary>
            public bool Inited;
            /// <summary>走地权重 0..1（法线朝下程度）</summary>
            public float Groundness;
            /// <summary>上帧时钟槽相位 0..1（相位回卷 = 新划水循环 = 抓地拍）</summary>
            public float PrevPhase01;
            public bool Visible;
            /// <summary>失力度 0..1（死亡演出）</summary>
            public float Limp;
        }

        private readonly Leg[] legs = new Leg[TotalLegs];
        /// <summary>平滑行进方向（膝弯与非步行姿态依据）</summary>
        private float travelDir = 1f;

        /// <summary>本帧腿部模拟（客户端与单人；服务端由调用方拦掉）</summary>
        public void Update(FssStateContext ctx) {
            NPC head = ctx.Npc;
            if (Math.Abs(head.velocity.X) > 1.2f) {
                travelDir = MathHelper.Lerp(travelDir, Math.Sign(head.velocity.X), 0.08f);
            }

            for (int li = 0; li < TotalLegs; li++) {
                int station = li / 2;
                int ordinal = StationOrdinals[station];
                NPC seg = ordinal < ctx.Segments.Count ? ctx.Segments[ordinal] : null;
                if (seg == null || !seg.active) {
                    legs[li].Visible = false;
                    continue;
                }
                legs[li].Visible = true;

                float chainDir = seg.rotation + MathHelper.PiOver2;
                Vector2 chainVec = chainDir.ToRotationVector2();
                float flankSign = (li & 1) == 0 ? 1f : -1f;
                Vector2 normal = (chainDir + MathHelper.PiOver2).ToRotationVector2() * flankSign;
                legs[li].Back = -chainVec;
                legs[li].Groundness = MathHelper.Clamp((normal.Y + 0.6f) / 1.2f, 0f, 1f);

                Vector2 hip = seg.Center + normal * 11f;
                legs[li].Hip = hip;

                if (!legs[li].Inited) {
                    Vector2 f0 = hip + normal * (MaxReach * 0.7f);
                    f0.Y = Math.Min(f0.Y, GroundAt(f0.X, hip));
                    legs[li].Foot = f0;
                    legs[li].Limp = 0f;
                    legs[li].Inited = true;
                }

                if (ctx.LegCommand == FssLegCommand.Collapse && StationCollapsed(li, ctx)) {
                    //失力：长肢垂软瘫散，轻微摇晃；同站近拍先瘫
                    legs[li].Limp = MathHelper.Clamp(legs[li].Limp + ((li & 1) == 1 ? 0.05f : 0.06f), 0f, 1f);
                    Vector2 dangle = hip + new Vector2(
                        travelDir * ((li & 1) == 1 ? 20f : 30f) + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + li * 1.7f) * 7f,
                        MaxReach * 0.9f);
                    dangle.Y = Math.Min(dangle.Y, GroundAt(dangle.X, hip));
                    legs[li].Foot = Vector2.Lerp(legs[li].Foot, dangle, 0.16f);
                }
                else if (ctx.LegCommand == FssLegCommand.Tuck) {
                    //收拢贴体（钻沙/掠冲）
                    Vector2 fold = hip - chainVec * (34f + station * 7f + (li & 1) * 9f) + normal * 9f;
                    legs[li].Foot = Vector2.Lerp(legs[li].Foot, fold, 0.28f);
                }
                else if (ctx.LegCommand == FssLegCommand.Raise && station < 2) {
                    //前二站举离地面：长肢前探高举 + 两侧张开
                    float lift = MathHelper.Clamp(ctx.FrontRaise, 0f, 1f);
                    Vector2 pose = hip + new Vector2(travelDir * (50f + station * 20f - (li & 1) * 12f), -30f - 40f * lift)
                        + normal * 13f
                        + new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + li * 2.3f) * 7f, 0f);
                    legs[li].Foot = Vector2.Lerp(legs[li].Foot, pose, 0.16f);
                }
                else {
                    legs[li].Limp = MathHelper.Clamp(legs[li].Limp - 0.05f, 0f, 1f);
                    UpdateStroke(ref legs[li], li, hip, normal, chainVec, head, ctx);
                }
            }
        }

        /// <summary>本腿所在站是否已失力（偶侧先瘫，奇侧等对腿软下去再跟）</summary>
        private bool StationCollapsed(int li, FssStateContext ctx) {
            if (li / 2 >= ctx.CollapsedLegs) {
                return false;
            }
            return (li & 1) == 0 || legs[li - 1].Limp > 0.35f;
        }

        /// <summary>
        /// 划桨步态（同荒花沙蟒的往复构造）：功率段快速后耙、恢复段折叠前探抛物线抬过。
        /// 相位回卷 = 抓地拍：写入该站下沉 + 咬沙尘。变异层：足端目标叠常开痉挛微颤。
        /// </summary>
        private void UpdateStroke(ref Leg leg, int li, Vector2 hip, Vector2 normal, Vector2 chainVec, NPC head, FssStateContext ctx) {
            float t01 = SlotPhase01(li, ctx);
            bool cycleWrapped = t01 < leg.PrevPhase01;
            leg.PrevPhase01 = t01;

            float tilt, radius, clearance;
            if (t01 < PowerFraction) {
                float p = t01 / PowerFraction;
                tilt = MathHelper.Lerp(TiltForward, -TiltBack, p);
                radius = 0.88f;
                clearance = 0f;
            }
            else {
                float r = (t01 - PowerFraction) / (1f - PowerFraction);
                float arc = MathF.Sin(r * MathHelper.Pi);
                float eased = r * r * (3f - 2f * r);
                tilt = MathHelper.Lerp(-TiltBack, TiltForward, eased);
                radius = MathHelper.Lerp(0.88f, 0.52f, arc);
                clearance = RecoveryClearance * arc;
            }
            tilt += StrokeAccent[li / 2];

            float rotSign = (li & 1) == 0 ? -1f : 1f;
            Vector2 target = hip + normal.RotatedBy(tilt * rotSign) * (MaxReach * radius);

            //痉挛微颤：高频小幅、逐腿错相（与划桨大周期分层的病态抖）
            target += new Vector2(
                MathF.Sin(Main.GlobalTimeWrappedHourly * 9.3f + li * 2.9f),
                MathF.Sin(Main.GlobalTimeWrappedHourly * 11.7f + li * 1.3f)) * TwitchAmp;

            float groundY = GroundAt(target.X, hip);
            target.Y = Math.Min(target.Y, groundY - clearance);
            leg.Foot = Vector2.Lerp(leg.Foot, target, 0.4f);

            bool grounded = leg.Groundness > 0.5f && Math.Abs(leg.Foot.Y - groundY) < 10f;
            float speedNow = head.velocity.Length();

            //抓地拍：新循环起点即爪尖咬进沙面
            if (cycleWrapped && grounded) {
                int station = li / 2;
                float weight = (li & 1) == 0 ? 1f : 0.7f;
                ctx.StationBob[station] = Math.Max(ctx.StationBob[station], weight);
                if (!Main.dedServ && speedNow > 2f) {
                    for (int k = 0; k < 4; k++) {
                        Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                            DustID.Sand, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.8f, 2.2f)),
                            110, FssVfx.TaintedSand, Main.rand.NextFloat(0.8f, 1.2f));
                        d.noGravity = false;
                    }
                }
            }
            //功率段耙沙：沿耙向连续掀起沙痕
            else if (t01 < PowerFraction && grounded && speedNow > 2f
                && !Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-4f, 4f), -2f),
                    DustID.Sand, new Vector2(-travelDir * Main.rand.NextFloat(1.2f, 2.4f), -Main.rand.NextFloat(0.5f, 1.4f)),
                    120, FssVfx.TaintedSand, Main.rand.NextFloat(0.7f, 1.05f));
                d.noGravity = false;
            }
        }

        /// <summary>该腿的时钟槽相位 0..1：站序节律波（前→后）+ 同站两侧反相</summary>
        private static float SlotPhase01(int li, FssStateContext ctx) {
            float phase = ctx.GaitPhase - li / 2 * StationLag + ((li & 1) == 1 ? MathHelper.Pi : 0f);
            phase %= MathHelper.TwoPi;
            if (phase < 0f) {
                phase += MathHelper.TwoPi;
            }
            return phase / MathHelper.TwoPi;
        }

        /// <summary>足下探地：从髋上方向下扫第一格实心面</summary>
        private static float GroundAt(float x, Vector2 hip) {
            return FssVfx.FindGroundY(new Vector2(x, hip.Y - 50f), 500f);
        }

        /// <summary>
        /// 画八腿：按走地权重升序绘制——背侧/悬空排先画且压暗略细，走地排后画且全亮。
        /// 全部压在头 PreDraw 层（整链绘制在腿之后，天然盖住腿根）。
        /// 统一乘 SkinMul 手染坏死紫；关节偶发灵液滴漏（变异层）。
        /// </summary>
        public void Draw(SpriteBatch sb, Vector2 screenPos, FssStateContext ctx) {
            if (ctx.LegAlpha <= 0.03f) {
                return;
            }
            Texture2D upperTex = FssHead.LegUpperAsset?.Value;
            Texture2D lowerTex = FssHead.LegLowerAsset?.Value;
            if (upperTex == null || lowerTex == null) {
                return;
            }
            float fade = ctx.LegAlpha * (1f - ctx.Npc.alpha / 255f);
            if (fade <= 0.03f) {
                return;
            }

            Span<int> order = stackalloc int[TotalLegs];
            for (int i = 0; i < TotalLegs; i++) {
                order[i] = i;
            }
            for (int i = 1; i < TotalLegs; i++) {
                int cur = order[i];
                float key = legs[cur].Groundness;
                int j = i - 1;
                while (j >= 0 && legs[order[j]].Groundness > key) {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = cur;
            }

            foreach (int li in order) {
                if (!legs[li].Visible || !legs[li].Inited) {
                    continue;
                }
                float groundness = legs[li].Groundness;
                Color light = Lighting.GetColor((int)(legs[li].Hip.X / 16f), (int)(legs[li].Hip.Y / 16f));
                float dim = MathHelper.Lerp(0.62f, 1f, groundness) * (1f - legs[li].Limp * 0.35f);
                Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255)
                    .MultiplyRGB(FssVfx.SkinMul) * fade;

                Vector2 hip = legs[li].Hip + new Vector2(0f, ctx.StationBob[li / 2] * StationDipPx);
                Vector2 foot = legs[li].Foot;

                //解剖学硬钳：画出来的足端绝不许超过腿骨总长
                Vector2 d = foot - hip;
                float rawLen = d.Length();
                if (rawLen > MaxReach) {
                    foot = hip + d * (MaxReach / rawLen);
                    d = foot - hip;
                }
                float dist = MathHelper.Clamp(d.Length(), 14f, MaxReach);
                float baseAng = d.ToRotation();
                float cosA = MathHelper.Clamp((UpperLen * UpperLen + dist * dist - LowerLen * LowerLen)
                    / (2f * UpperLen * dist), -1f, 1f);
                float phi = MathF.Acos(cosA);
                Vector2 back = legs[li].Back;
                float kneeAng = Vector2.Dot((baseAng + phi).ToRotationVector2(), back)
                    >= Vector2.Dot((baseAng - phi).ToRotationVector2(), back)
                    ? baseAng + phi : baseAng - phi;
                Vector2 knee = hip + kneeAng.ToRotationVector2() * UpperLen;

                //关节灵液滴漏（低频变异底噪，走地腿才滴）
                if (!Main.dedServ && groundness > 0.5f && Main.rand.NextBool(90)) {
                    Dust drip = Dust.NewDustPerfect(knee, DustID.Ichor,
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.5f, 1.4f)),
                        40, default, Main.rand.NextFloat(0.7f, 1f));
                    drip.noGravity = false;
                }

                float thick = MathHelper.Lerp(0.95f, 1.05f, groundness);
                DrawBone(sb, upperTex, hip, knee, 1.25f * thick, tint, screenPos);
                DrawBone(sb, lowerTex, knee, foot, 1f * thick, tint, screenPos);
            }
        }

        /// <summary>骨节拉伸绘制：贴图约定尖端朝上，底端锚在关节起点</summary>
        private static void DrawBone(SpriteBatch sb, Texture2D tex, Vector2 from, Vector2 to,
            float thickness, Color tint, Vector2 screenPos) {
            Vector2 dir = to - from;
            float len = dir.Length();
            if (len < 3f) {
                return;
            }
            float rot = dir.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = new(tex.Width * 0.5f, tex.Height - 2f);
            Vector2 scale = new(thickness * 0.7f, len / (tex.Height - 4f));
            sb.Draw(tex, from - screenPos, null, tint, rot, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
