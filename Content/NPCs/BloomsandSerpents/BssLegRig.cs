using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 四足步态系统（纯表现层）。联机契约沿用废钢统帅四臂先例：
    /// 各端从已同步的体节位置本地重建，不入网络包，无 gameplay 碰撞。
    ///
    /// 月总式长腿：触及 ~124px 的两节长肢，远前方落足、身体从足上碾过、
    /// 拖行超限再慢摆大弧换步——腿是"撑着身体走"的主角，不是装饰。
    /// 落足回填 <see cref="BssStateContext.StepBob"/>，身体表现下沉回弹（爬行辅助的重量读数）。
    /// 双骨余弦定理解析 IK；贴图占位用尾节素材拉伸，用户腿贴图到位后
    /// 直接覆盖 LegUpper/LegLower 两张 png，本文件不动。
    /// </summary>
    internal class BssLegRig
    {
        public const int LegCount = 4;

        /// <summary>髋锚体节链序（长腿需要更大的间距铺开）</summary>
        private static readonly int[] HipOrdinals = { 1, 4, 7, 10 };
        /// <summary>足端中立位相对髋的横向错位（步位交错，避免四足同槽）</summary>
        private static readonly float[] NeutralOffsetX = { 42f, -12f, 26f, -30f };

        public const float UpperLen = 58f;
        public const float LowerLen = 66f;
        private const float MaxReach = UpperLen + LowerLen - 2f;
        /// <summary>拖行超过此值换步（长肢大跨度）</summary>
        private const float StrideLen = 92f;
        /// <summary>换步落点前引：落足远在髋前方，身体随后碾过它</summary>
        private const float StrideLead = 78f;
        /// <summary>慢摆帧数（月总式的从容）</summary>
        private const int SwingFrames = 17;
        /// <summary>摆动抬弧高度</summary>
        private const float SwingLift = 40f;

        private struct Leg
        {
            public Vector2 Hip;
            public Vector2 Foot;
            public bool Swinging;
            public float SwingT;
            public Vector2 SwingFrom;
            public Vector2 SwingTo;
            public bool Visible;
            /// <summary>失力度 0..1（死亡演出）</summary>
            public float Limp;
        }

        private readonly Leg[] legs = new Leg[LegCount];
        private bool init;
        /// <summary>平滑行进方向（膝弯与落点前引依据，避免转身瞬间腿抽搐）</summary>
        private float travelDir = 1f;

        /// <summary>本帧腿部模拟（客户端与单人；服务端由调用方拦掉）</summary>
        public void Update(BssStateContext ctx) {
            NPC head = ctx.Npc;
            if (Math.Abs(head.velocity.X) > 1.2f) {
                travelDir = MathHelper.Lerp(travelDir, Math.Sign(head.velocity.X), 0.08f);
            }

            for (int i = 0; i < LegCount; i++) {
                NPC seg = HipOrdinals[i] < ctx.Segments.Count ? ctx.Segments[HipOrdinals[i]] : null;
                if (seg == null || !seg.active) {
                    legs[i].Visible = false;
                    continue;
                }
                legs[i].Visible = true;

                //腹侧法线：体轴垂线里朝下的那条（体节 rotation = 链向角 - PiOver2）
                float axis = seg.rotation + MathHelper.PiOver2;
                Vector2 perp = (axis + MathHelper.PiOver2).ToRotationVector2();
                Vector2 ventral = perp.Y >= 0f ? perp : -perp;
                Vector2 hip = seg.Center + ventral * 10f;
                legs[i].Hip = hip;

                if (!init) {
                    legs[i].Foot = new Vector2(hip.X + NeutralOffsetX[i], GroundAt(hip.X + NeutralOffsetX[i], hip));
                    legs[i].Swinging = false;
                    legs[i].Limp = 0f;
                }

                bool marchThisLeg = ctx.LegCommand == BssLegCommand.March
                    || (ctx.LegCommand == BssLegCommand.Raise && i >= 2)
                    || (ctx.LegCommand == BssLegCommand.Collapse && i >= ctx.CollapsedLegs);

                if (ctx.LegCommand == BssLegCommand.Collapse && i < ctx.CollapsedLegs) {
                    //失力：长肢垂软瘫散，轻微摇晃
                    legs[i].Limp = MathHelper.Clamp(legs[i].Limp + 0.06f, 0f, 1f);
                    legs[i].Swinging = false;
                    Vector2 dangle = hip + new Vector2(
                        travelDir * 26f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + i * 1.7f) * 6f,
                        MaxReach * 0.9f);
                    dangle.Y = Math.Min(dangle.Y, GroundAt(dangle.X, hip));
                    legs[i].Foot = Vector2.Lerp(legs[i].Foot, dangle, 0.16f);
                }
                else if (marchThisLeg) {
                    legs[i].Limp = MathHelper.Clamp(legs[i].Limp - 0.05f, 0f, 1f);
                    UpdateMarch(ref legs[i], i, hip, head, ctx);
                }
                else if (ctx.LegCommand == BssLegCommand.Tuck) {
                    //收拢贴体（钻沙/掠冲）：长腿向后掠平，读出流线
                    legs[i].Swinging = false;
                    Vector2 fold = hip + new Vector2(-travelDir * (30f + i * 6f), 10f);
                    legs[i].Foot = Vector2.Lerp(legs[i].Foot, fold, 0.28f);
                }
                else if (ctx.LegCommand == BssLegCommand.Raise) {
                    //前腿举离地面（i<2 才会走到这里）：长肢前探高举，月总式的威仪
                    legs[i].Swinging = false;
                    float lift = MathHelper.Clamp(ctx.FrontRaise, 0f, 1f);
                    Vector2 pose = hip + new Vector2(travelDir * (44f + i * 18f), -26f - 34f * lift)
                        + new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + i * 2.3f) * 6f, 0f);
                    legs[i].Foot = Vector2.Lerp(legs[i].Foot, pose, 0.16f);
                }
                else {
                    //Flail：腾空乱蹬，长肢划大弧
                    legs[i].Swinging = false;
                    float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * 5.4f + i * 1.71f) * 0.7f;
                    Vector2 kick = hip + ventral.RotatedBy(wave) * (MaxReach * 0.66f);
                    legs[i].Foot = Vector2.Lerp(legs[i].Foot, kick, 0.28f);
                }
            }
            init = true;
        }

        /// <summary>
        /// 长肢步行：足端踩定跟地形，身体前进把髋推过足位，拖行超过 StrideLen 才起摆，
        /// 慢摆大弧落到远前方（水平走 smoothstep 缓入缓出，从容不慌）
        /// </summary>
        private void UpdateMarch(ref Leg leg, int index, Vector2 hip, NPC head, BssStateContext ctx) {
            float neutralX = hip.X + NeutralOffsetX[index];
            if (!leg.Swinging) {
                float stretch = Math.Abs(neutralX - leg.Foot.X);
                if (stretch > StrideLen && CanSwing(index)) {
                    leg.Swinging = true;
                    leg.SwingT = 0f;
                    leg.SwingFrom = leg.Foot;
                    float targetX = neutralX + travelDir * StrideLead;
                    leg.SwingTo = new Vector2(targetX, GroundAt(targetX, hip));
                }
                else {
                    //踩定：足 Y 贴住地形（沙丘起伏跟随）
                    leg.Foot.Y = GroundAt(leg.Foot.X, hip);
                    //够不着地就朝髋收（悬崖边缘垂腿）
                    if (Vector2.Distance(leg.Foot, hip) > MaxReach) {
                        leg.Foot = hip + (leg.Foot - hip).SafeNormalize(Vector2.UnitY) * MaxReach;
                    }
                }
            }

            if (leg.Swinging) {
                //摆速随体速缩放：慢爬从容大步，追击疾步倒腾（腿始终跟得上身体）
                float pace = 1f + Math.Abs(head.velocity.X) * 0.04f;
                leg.SwingT += pace / SwingFrames;
                //摆动中落点持续追髋（身体在动，长腿要落到"将来"的前方）
                float retargetX = hip.X + NeutralOffsetX[index] + travelDir * StrideLead;
                leg.SwingTo.X = MathHelper.Lerp(leg.SwingTo.X, retargetX, 0.25f);

                if (leg.SwingT >= 1f) {
                    leg.Swinging = false;
                    leg.SwingTo.Y = GroundAt(leg.SwingTo.X, hip);
                    leg.Foot = leg.SwingTo;
                    //落足：尘扑 + 身体下沉回弹（腿在撑着身体走的重量读数）
                    ctx.StepBob = Math.Max(ctx.StepBob, 1f);
                    if (!Main.dedServ && Math.Abs(head.velocity.X) > 2f) {
                        for (int k = 0; k < 5; k++) {
                            Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                                DustID.Sand, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.8f, 2.2f)),
                                110, default, Main.rand.NextFloat(0.8f, 1.2f));
                            d.noGravity = false;
                        }
                    }
                }
                else {
                    //水平缓入缓出 + 正弦抬弧：从容的大步
                    float ease = leg.SwingT * leg.SwingT * (3f - 2f * leg.SwingT);
                    Vector2 flat = Vector2.Lerp(leg.SwingFrom, leg.SwingTo, ease);
                    leg.Foot = flat - new Vector2(0f, MathF.Sin(leg.SwingT * MathHelper.Pi) * SwingLift);
                }
            }
        }

        /// <summary>换步许可：同刻至多两足摆动，相邻腿不同摆（对角步态的涌现来源）</summary>
        private bool CanSwing(int index) {
            int swinging = 0;
            for (int i = 0; i < LegCount; i++) {
                if (legs[i].Swinging) {
                    swinging++;
                    if (i == index - 1 || i == index + 1) {
                        return false;
                    }
                }
            }
            return swinging < 2;
        }

        /// <summary>足下探地：从髋上方向下扫第一格实心面（长腿探得更深）</summary>
        private static float GroundAt(float x, Vector2 hip) {
            return BssVfx.FindGroundY(new Vector2(x, hip.Y - 46f), 460f);
        }

        /// <summary>
        /// 画四腿：远侧腿（奇数位）先画且压暗，近侧腿后画，全部压在头 PreDraw 层
        /// （体节 whoAmI 更高、绘制在后，天然盖住腿根 = 腿读作腹下）
        /// </summary>
        public void Draw(SpriteBatch sb, Vector2 screenPos, BssStateContext ctx) {
            if (!init || ctx.LegAlpha <= 0.03f) {
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

            //远侧一对先画（压暗），近侧一对后画
            Span<int> order = stackalloc int[] { 1, 3, 0, 2 };
            foreach (int i in order) {
                if (!legs[i].Visible) {
                    continue;
                }
                bool farSide = i is 1 or 3;
                Color light = Lighting.GetColor((int)(legs[i].Hip.X / 16f), (int)(legs[i].Hip.Y / 16f));
                float dim = (farSide ? 0.62f : 1f) * (1f - legs[i].Limp * 0.35f);
                Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255) * fade;

                //双骨解析 IK：膝弯朝行进反方向（虫腿后弯）
                Vector2 hip = legs[i].Hip;
                Vector2 foot = legs[i].Foot;
                Vector2 d = foot - hip;
                float dist = MathHelper.Clamp(d.Length(), 14f, MaxReach);
                float baseAng = d.ToRotation();
                float cosA = MathHelper.Clamp((UpperLen * UpperLen + dist * dist - LowerLen * LowerLen)
                    / (2f * UpperLen * dist), -1f, 1f);
                float bendDir = travelDir >= 0f ? -1f : 1f;
                float kneeAng = baseAng + MathF.Acos(cosA) * bendDir;
                Vector2 knee = hip + kneeAng.ToRotationVector2() * UpperLen;

                DrawBone(sb, upperTex, hip, knee, 1.2f, tint, screenPos);
                DrawBone(sb, lowerTex, knee, foot, 0.95f, tint, screenPos);
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
