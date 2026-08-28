using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 步态系统（纯表现层）。联机契约沿用废钢统帅四臂先例：
    /// 各端从已同步的体节位置本地重建，不入网络包，无 gameplay 碰撞。
    ///
    /// 编制：4 髋站 x 两侧翻缘 = 8 条两节长肢（触及 ~124px）。
    /// 每条腿固定锚在体节体轴的一侧法线上（手性随体轴连续，不看世界朝向），
    /// 身体水平时一排在地侧、一排在背侧，竖直时两排向左右张开——躯干两边始终有腿。
    ///
    /// 运动模型镜像至尊灾厄坟灾虫臂（SepulcherArm）：肢体端点不钉在世界上，
    /// 而是锚在体节坐标系里做不对称的划桨往复——快速功率段向后耙、从容恢复段
    /// 折叠前探；贴地侧由地面线钳制成"爪耙沙面"，恢复段按离地余隙抛物线抬过。
    /// 相位全部绑 <see cref="BssStateContext.GaitPhase"/> 步态时钟：站序滞后成
    /// 蜈蚣节律波（前→后），同站两侧反相；头部爬行的推进涌动与贴地呼吸读同一
    /// 时钟，腿和身体咬合在同一节拍上。抓地拍回填 <see cref="BssStateContext.StationBob"/>：
    /// 该站体节局部下沉、支撑腿绘制髋随之压低（腿被身体压弯的重量读数）。
    /// 双骨余弦定理解析 IK；贴图占位用尾节素材拉伸，用户腿贴图到位后
    /// 直接覆盖 LegUpper/LegLower 两张 png，本文件不动。
    /// </summary>
    internal class BssLegRig
    {
        /// <summary>髋站数（每站两侧各一条腿；死亡演出逐站瘫软按此计数）</summary>
        public const int LegCount = 4;
        /// <summary>腿总数（li = 站号*2 + 侧位，偶数 = +法线侧，奇数 = −法线侧）</summary>
        private const int TotalLegs = LegCount * 2;

        /// <summary>髋锚体节链序（长腿需要更大的间距铺开；落步下沉采样共用）</summary>
        internal static readonly int[] StationOrdinals = { 1, 4, 7, 10 };
        /// <summary>各站划水倾角修饰（去机械感的性格差）</summary>
        private static readonly float[] StrokeAccent = { 0.08f, -0.06f, 0.03f, -0.09f };

        public const float UpperLen = 58f;
        public const float LowerLen = 66f;
        private const float MaxReach = UpperLen + LowerLen - 2f;

        /// <summary>功率段（快耙）占周期比例；其余为从容恢复段（镜像坟灾虫快收慢展的不对称）</summary>
        internal const float PowerFraction = 0.32f;
        /// <summary>划水前探倾角（自法线向体前，弧度）</summary>
        private const float TiltForward = 0.9f;
        /// <summary>划水后耙倾角（自法线向体后，弧度）</summary>
        private const float TiltBack = 0.85f;
        /// <summary>恢复段最大离地余隙（抬腿过障的高度）</summary>
        private const float RecoveryClearance = 26f;
        /// <summary>站间相位差：划水波前→后传的蜈蚣节律</summary>
        private const float StationLag = MathHelper.TwoPi * 0.22f;
        /// <summary>落步下沉的绘制像素基数（体节/头/髋压缩共用）</summary>
        internal const float StationDipPx = 6f;

        private struct Leg
        {
            public Vector2 Hip;
            public Vector2 Foot;
            /// <summary>体后方向（−链向，随体节逐帧刷新；膝弯统一朝体后 = 两侧镜像对称）</summary>
            public Vector2 Back;
            /// <summary>足端已初始化（首次见到宿主体节时落位；防默认 (0,0) = 世界原点拉丝）</summary>
            public bool Inited;
            /// <summary>走地权重 0..1（法线朝下程度；亮度/绘制序过渡依据）</summary>
            public float Groundness;
            /// <summary>上帧时钟槽相位 0..1（相位回卷 = 新划水循环 = 抓地拍）</summary>
            public float PrevPhase01;
            public bool Visible;
            /// <summary>失力度 0..1（死亡演出）</summary>
            public float Limp;
        }

        private readonly Leg[] legs = new Leg[TotalLegs];
        /// <summary>平滑行进方向（膝弯与非步行姿态依据，避免转身瞬间腿抽搐）</summary>
        private float travelDir = 1f;

        /// <summary>本帧腿部模拟（客户端与单人；服务端由调用方拦掉）</summary>
        public void Update(BssStateContext ctx) {
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

                //体轴与翻缘：链向角 = rotation + PiOver2；法线取固定手性的垂线 x 侧位符号，
                //不看世界朝向 → 水平时一排朝地一排朝天，竖直时两排朝左右，转向连续不跳侧
                float chainDir = seg.rotation + MathHelper.PiOver2;
                Vector2 chainVec = chainDir.ToRotationVector2();
                float flankSign = (li & 1) == 0 ? 1f : -1f;
                Vector2 normal = (chainDir + MathHelper.PiOver2).ToRotationVector2() * flankSign;
                legs[li].Back = -chainVec;
                //走地/呈现权重：法线朝下程度（±0.6 对称带）。水平身体地侧 1、背侧 0，
                //竖直身体两侧各 0.5——亮度居中不整体发暗，抓地判定（>0.5）也自然关闭
                legs[li].Groundness = MathHelper.Clamp((normal.Y + 0.6f) / 1.2f, 0f, 1f);

                Vector2 hip = seg.Center + normal * 10f;
                legs[li].Hip = hip;

                //足端初始化必须逐腿做在"首次见到宿主体节"时：入场首帧体节尚未生成，
                //全局标记会把足端留在默认 (0,0)（世界原点），出场即拉丝
                if (!legs[li].Inited) {
                    Vector2 f0 = hip + normal * (MaxReach * 0.7f);
                    f0.Y = Math.Min(f0.Y, GroundAt(f0.X, hip));
                    legs[li].Foot = f0;
                    legs[li].Limp = 0f;
                    legs[li].Inited = true;
                }

                if (ctx.LegCommand == BssLegCommand.Collapse && StationCollapsed(li, ctx)) {
                    //失力：长肢垂软瘫散（重力向，背侧腿翻搭过身体），轻微摇晃；同站近拍先瘫
                    legs[li].Limp = MathHelper.Clamp(legs[li].Limp + ((li & 1) == 1 ? 0.05f : 0.06f), 0f, 1f);
                    Vector2 dangle = hip + new Vector2(
                        travelDir * ((li & 1) == 1 ? 18f : 26f) + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + li * 1.7f) * 6f,
                        MaxReach * 0.9f);
                    dangle.Y = Math.Min(dangle.Y, GroundAt(dangle.X, hip));
                    legs[li].Foot = Vector2.Lerp(legs[li].Foot, dangle, 0.16f);
                }
                else if (ctx.LegCommand == BssLegCommand.Tuck) {
                    //收拢贴体（钻沙/掠冲）：沿体轴向后掠平 + 贴向自家翻缘，读出流线
                    Vector2 fold = hip - chainVec * (30f + station * 6f + (li & 1) * 8f) + normal * 8f;
                    legs[li].Foot = Vector2.Lerp(legs[li].Foot, fold, 0.28f);
                }
                else if (ctx.LegCommand == BssLegCommand.Raise && station < 2) {
                    //前二站举离地面：长肢前探高举 + 两侧张开，月总式的威仪
                    float lift = MathHelper.Clamp(ctx.FrontRaise, 0f, 1f);
                    Vector2 pose = hip + new Vector2(travelDir * (44f + station * 18f - (li & 1) * 10f), -26f - 34f * lift)
                        + normal * 12f
                        + new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + li * 2.3f) * 6f, 0f);
                    legs[li].Foot = Vector2.Lerp(legs[li].Foot, pose, 0.16f);
                }
                else {
                    //March 步行 / Flail 腾空（同一划桨，空中照样爬——镜像坟灾虫爬空气）/
                    //Raise 后二站 / Collapse 未失力站
                    legs[li].Limp = MathHelper.Clamp(legs[li].Limp - 0.05f, 0f, 1f);
                    UpdateStroke(ref legs[li], li, hip, normal, chainVec, head, ctx);
                }
            }
        }

        /// <summary>本腿所在站是否已失力（偶侧先瘫，奇侧等对腿软下去再跟）</summary>
        private bool StationCollapsed(int li, BssStateContext ctx) {
            if (li / 2 >= ctx.CollapsedLegs) {
                return false;
            }
            return (li & 1) == 0 || legs[li - 1].Limp > 0.35f;
        }

        /// <summary>
        /// 划桨步态（镜像坟灾虫臂的往复构造，叠加沙地交互）：
        /// 端点 = 髋 + 法线绕体轴倾角旋转 x 半径，倾角在 [+前探, −后耙] 间随时钟往复——
        /// 功率段（PowerFraction）快速后耙、半径全伸，贴地时是"爪耙沙面"的推进读数；
        /// 恢复段从容折叠前探，按离地余隙抛物线抬过地面。相位回卷 = 新循环的抓地拍：
        /// 贴地则写入该站下沉 + 咬沙尘。端点始终体节相对，身体任何姿态都跟手不拉丝。
        /// </summary>
        private void UpdateStroke(ref Leg leg, int li, Vector2 hip, Vector2 normal, Vector2 chainVec, NPC head, BssStateContext ctx) {
            float t01 = SlotPhase01(li, ctx);
            bool cycleWrapped = t01 < leg.PrevPhase01;
            leg.PrevPhase01 = t01;

            float tilt, radius, clearance;
            if (t01 < PowerFraction) {
                //功率段：全伸快耙（近似线性 = 坟灾虫式快收）
                float p = t01 / PowerFraction;
                tilt = MathHelper.Lerp(TiltForward, -TiltBack, p);
                radius = 0.88f;
                clearance = 0f;
            }
            else {
                //恢复段：折叠 → 前探，抛物线抬腿
                float r = (t01 - PowerFraction) / (1f - PowerFraction);
                float arc = MathF.Sin(r * MathHelper.Pi);
                float eased = r * r * (3f - 2f * r);
                tilt = MathHelper.Lerp(-TiltBack, TiltForward, eased);
                radius = MathHelper.Lerp(0.88f, 0.52f, arc);
                clearance = RecoveryClearance * arc;
            }
            tilt += StrokeAccent[li / 2];

            //倾角正向 = 向体前（rotSign = −侧位符号，两侧对称展开）
            float rotSign = (li & 1) == 0 ? -1f : 1f;
            Vector2 target = hip + normal.RotatedBy(tilt * rotSign) * (MaxReach * radius);

            //沙地交互：地面线钳制（贴地排耙沙；背侧/悬空排 min() 自然无效）
            float groundY = GroundAt(target.X, hip);
            target.Y = Math.Min(target.Y, groundY - clearance);
            leg.Foot = Vector2.Lerp(leg.Foot, target, 0.4f);

            bool grounded = leg.Groundness > 0.5f && Math.Abs(leg.Foot.Y - groundY) < 10f;
            float speedNow = head.velocity.Length();

            //抓地拍：新循环起点即爪尖咬进沙面——该站下沉 + 咬沙尘
            if (cycleWrapped && grounded) {
                int station = li / 2;
                float weight = (li & 1) == 0 ? 1f : 0.7f;
                ctx.StationBob[station] = Math.Max(ctx.StationBob[station], weight);
                if (!Main.dedServ && speedNow > 2f) {
                    for (int k = 0; k < 4; k++) {
                        Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                            DustID.Sand, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.8f, 2.2f)),
                            110, default, Main.rand.NextFloat(0.8f, 1.2f));
                        d.noGravity = false;
                    }
                }
            }
            //功率段耙沙：沿耙向连续掀起沙痕（发力的可见证据）
            else if (t01 < PowerFraction && grounded && speedNow > 2f
                && !Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-4f, 4f), -2f),
                    DustID.Sand, new Vector2(-travelDir * Main.rand.NextFloat(1.2f, 2.4f), -Main.rand.NextFloat(0.5f, 1.4f)),
                    120, default, Main.rand.NextFloat(0.7f, 1.05f));
                d.noGravity = false;
            }
        }

        /// <summary>该腿的时钟槽相位 0..1：站序节律波（前→后）+ 同站两侧反相</summary>
        private static float SlotPhase01(int li, BssStateContext ctx) {
            float phase = ctx.GaitPhase - li / 2 * StationLag + ((li & 1) == 1 ? MathHelper.Pi : 0f);
            phase %= MathHelper.TwoPi;
            if (phase < 0f) {
                phase += MathHelper.TwoPi;
            }
            return phase / MathHelper.TwoPi;
        }

        /// <summary>足下探地：从髋上方向下扫第一格实心面（长腿探得更深）</summary>
        private static float GroundAt(float x, Vector2 hip) {
            return BssVfx.FindGroundY(new Vector2(x, hip.Y - 46f), 460f);
        }

        /// <summary>
        /// 画八腿：按走地权重升序绘制——背侧/悬空排先画且压暗略细，走地排后画且全亮，
        /// 全部压在头 PreDraw 层（体节 whoAmI 更高、绘制在后，天然盖住腿根 = 腿读作贴体，
        /// 不遮红花发射节）。绘制髋叠加该站落步下沉量而足端不动 → 抓地瞬间支撑腿被压短。
        /// </summary>
        public void Draw(SpriteBatch sb, Vector2 screenPos, BssStateContext ctx) {
            if (ctx.LegAlpha <= 0.03f) {
                return;
            }
            Texture2D upperTex = BssHead.LegUpperAsset?.Value;
            Texture2D lowerTex = BssHead.LegLowerAsset?.Value;
            if (upperTex == null || lowerTex == null) {
                return;
            }
            float fade = ctx.LegAlpha * (1f - ctx.Npc.alpha / 255f);
            if (fade <= 0.03f) {
                return;
            }

            //按走地权重升序：暗排在底、亮排在面（角色过渡时序随之连续换层）
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
                Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255) * fade;

                //支撑腿压缩：髋随该站落步下沉，足端踩定不动
                Vector2 hip = legs[li].Hip + new Vector2(0f, ctx.StationBob[li / 2] * StationDipPx);
                Vector2 foot = legs[li].Foot;

                //解剖学硬钳：画出来的足端绝不许超过腿骨总长（任何来源的野值都止步于此）
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
                //膝弯统一朝体后（比较两解与体后向的点积）：两侧在体坐标里镜像对称，
                //任何身体朝向都不会出现一侧膝朝头一侧膝朝尾的错拍
                float phi = MathF.Acos(cosA);
                Vector2 back = legs[li].Back;
                float kneeAng = Vector2.Dot((baseAng + phi).ToRotationVector2(), back)
                    >= Vector2.Dot((baseAng - phi).ToRotationVector2(), back)
                    ? baseAng + phi : baseAng - phi;
                Vector2 knee = hip + kneeAng.ToRotationVector2() * UpperLen;

                float thick = MathHelper.Lerp(0.9f, 1f, groundness);
                DrawBone(sb, upperTex, hip, knee, 1.2f * thick, tint, screenPos);
                DrawBone(sb, lowerTex, knee, foot, 0.95f * thick, tint, screenPos);
            }
        }

        /// <summary>骨节拉伸绘制：贴图约定尖端朝上（占位 = 尾节素材），底端锚在关节起点</summary>
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
