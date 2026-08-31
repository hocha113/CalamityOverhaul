using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>
    /// 变异四节步态系统（纯表现层）。各端从已同步的体节位置本地重建，
    /// 不入网络包，无 gameplay 碰撞。
    ///
    /// 编制：5 髋站 x 两侧翻缘 = 10 条四节长肢（基节/腿节/胫节/跗节，触及 ~168px）。
    /// 第五站挂在蜕变新生段（链序 29，仅 P2 蜕变长节后存在）——转阶段当场
    /// 多长出一对腿，变异生长的实体化；站不存在时天然不可见。
    ///
    /// 运动模型移植荒花沙蟒验证过的"世界落足步行"：足端钉在世界固定点，身体驶过；
    /// 髋足漂移超步幅、节律波轮到本腿时换步（预备下压 → 抛物摆越 → 落地踏拍），
    /// 高速滑刹犁沙，髋没入地下自动收拢，够不着地腾空卷曲。
    /// 四节差异：胫节末端多一段贴地铺放的跗节（脚掌）——落步是"啪嗒"的湿重读数。
    ///
    /// 变异性格层：足端痉挛微颤（高频小抖与步行大周期分层）、关节灵液滴漏、
    /// 踉跄（确定性哈希驱动的低概率落短补步——病态的不齐整）。
    /// 贴图借 BSS 腿骨素材，统一乘 SkinMul 压向坏死紫。
    /// </summary>
    internal class FssLegRig
    {
        #region 编制与解剖
        /// <summary>髋站数（第五站 = 蜕变新生段；死亡演出逐站瘫软按此计数）</summary>
        public const int LegCount = 5;
        /// <summary>腿总数（li = 站号*2 + 侧位，偶数 = +法线侧，奇数 = −法线侧）</summary>
        private const int TotalLegs = LegCount * 2;

        /// <summary>髋锚体节链序（前四站铺 26 节长躯；第五站在蜕变新生段，P2 前不存在）</summary>
        internal static readonly int[] StationOrdinals = { 1, 5, 9, 13, 29 };
        /// <summary>各站步幅性格差（变异体的不齐整比 BSS 更大）</summary>
        private static readonly float[] StrideAccent = { 1.08f, 0.9f, 1.04f, 0.86f, 1.12f };

        /// <summary>基节长（髋部摆节）</summary>
        internal const float CoxaLen = 30f;
        /// <summary>腿节长</summary>
        internal const float FemurLen = 60f;
        /// <summary>胫节长</summary>
        internal const float TibiaLen = 62f;
        /// <summary>跗节长（贴地脚掌）</summary>
        internal const float TarsusLen = 24f;
        /// <summary>全肢触及</summary>
        internal const float MaxReach = CoxaLen + FemurLen + TibiaLen + TarsusLen - 8f;

        /// <summary>站间相位差：换步许可窗前→后传的蜈蚣节律</summary>
        internal const float StationLag = MathHelper.TwoPi * 0.22f;
        /// <summary>落步下沉的绘制像素基数（体节/头/髋压缩共用）</summary>
        internal const float StationDipPx = 7f;
        #endregion

        #region 步行调参
        /// <summary>足端休息半径（占全肢触及比例）</summary>
        private const float RestReach = 0.56f;
        /// <summary>髋足漂移触发换步的距离（体格更大步幅更长）</summary>
        private const float StrideTrigger = 50f;
        /// <summary>落点沿速度前瞻帧数</summary>
        private const float StepLead = 10f;
        /// <summary>摆越离地余隙</summary>
        private const float StepClearance = 32f;
        /// <summary>强制换步的伸展比（相对 MaxReach）</summary>
        private const float EmergencyStretch = 0.93f;
        /// <summary>基节相对法线的摆动限幅（弧度）</summary>
        private const float CoxaSwingMax = 1.15f;
        /// <summary>滑刹渐入/全开速度（px/f；变异体更重更容易打滑）</summary>
        private const float SkateStart = 12f;
        private const float SkateFull = 21f;
        /// <summary>痉挛微颤振幅（像素；变异常开底噪）</summary>
        private const float TwitchAmp = 2.2f;
        /// <summary>踉跄概率（1/N 步；确定性哈希驱动）</summary>
        private const int StumbleOneIn = 6;
        /// <summary>踉跄落短距离</summary>
        private const float StumbleShortPx = 26f;
        #endregion

        private struct Leg
        {
            public Vector2 Foot;
            public Vector2 PlantPos;
            public Vector2 SwingFrom;
            public Vector2 SwingTo;
            public float SwingT;
            public float SwingDur;
            public float SwingClearance;
            public bool Planted;
            public bool Swinging;
            public bool Inited;
            public float Groundness;
            public float Limp;
            /// <summary>IK 膝弯分支迟滞（±1，0 = 未定）</summary>
            public int KneeSign;
            /// <summary>已迈步计数（踉跄哈希源）</summary>
            public int StepCount;
            /// <summary>本次摆越是踉跄补步（落短后的快速跟步不再二次踉跄）</summary>
            public bool CatchUpStep;
            public bool Visible;
            public Vector2 Back;
            //绘制缓存（Update 解算、Draw 消费）
            public Vector2 Hip;
            public Vector2 CoxaTip;
            public Vector2 Knee;
            public Vector2 Ankle;
            public Vector2 DrawFoot;
        }

        private readonly Leg[] legs = new Leg[TotalLegs];
        /// <summary>平滑行进方向（步幅前瞻与跗节铺向依据）</summary>
        private float travelDir = 1f;

        #region 驱动
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
                ref Leg leg = ref legs[li];
                if (seg == null || !seg.active) {
                    leg.Visible = false;
                    continue;
                }
                leg.Visible = true;

                float chainDir = seg.rotation + MathHelper.PiOver2;
                Vector2 chainVec = chainDir.ToRotationVector2();
                float flankSign = (li & 1) == 0 ? 1f : -1f;
                Vector2 normal = (chainDir + MathHelper.PiOver2).ToRotationVector2() * flankSign;
                Vector2 hip = seg.Center + normal * 11f;

                leg.Hip = hip;
                leg.Back = -chainVec;
                leg.Groundness = MathHelper.Clamp((normal.Y + 0.6f) / 1.2f, 0f, 1f);

                if (!leg.Inited) {
                    Vector2 f0 = hip + normal * (MaxReach * RestReach);
                    f0.Y = Math.Min(f0.Y, GroundAt(f0.X, hip.Y));
                    leg.Foot = f0;
                    leg.PlantPos = f0;
                    leg.Planted = true;
                    leg.Swinging = false;
                    leg.Limp = 0f;
                    leg.Inited = true;
                }

                bool limpDecay = true;
                if (ctx.LegCommand == FssLegCommand.Collapse && StationCollapsed(li, ctx)) {
                    UpdateCollapse(ref leg, li, hip);
                    limpDecay = false;
                }
                else if (ctx.LegCommand == FssLegCommand.Tuck) {
                    UpdateTuck(ref leg, li, hip, chainVec, normal);
                }
                else if (ctx.LegCommand == FssLegCommand.Raise && station < 2) {
                    UpdateRaise(ref leg, li, hip, normal, ctx);
                }
                else {
                    UpdateWalk(ref leg, li, hip, normal, chainVec, ctx);
                }
                if (limpDecay) {
                    leg.Limp = MathHelper.Clamp(leg.Limp - 0.05f, 0f, 1f);
                }

                SolveLeg(ref leg, hip, normal);

                //变异层：关节灵液滴漏（走地腿低频）
                if (!Main.dedServ && leg.Groundness > 0.5f && Main.rand.NextBool(110)) {
                    Dust drip = Dust.NewDustPerfect(Main.rand.NextBool() ? leg.Knee : leg.Ankle,
                        DustID.Ichor,
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.5f, 1.4f)),
                        40, default, Main.rand.NextFloat(0.7f, 1f));
                    drip.noGravity = false;
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
        #endregion

        #region 姿态模组
        /// <summary>失力：四节长肢垂软瘫散（重力向），轻微摇晃</summary>
        private void UpdateCollapse(ref Leg leg, int li, Vector2 hip) {
            leg.Limp = MathHelper.Clamp(leg.Limp + ((li & 1) == 1 ? 0.05f : 0.06f), 0f, 1f);
            leg.Planted = false;
            leg.Swinging = false;
            Vector2 dangle = hip + new Vector2(
                travelDir * ((li & 1) == 1 ? 20f : 30f) + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + li * 1.7f) * 7f,
                MaxReach * 0.9f);
            dangle.Y = Math.Min(dangle.Y, GroundAt(dangle.X, hip.Y));
            leg.Foot = Vector2.Lerp(leg.Foot, dangle, 0.16f);
        }

        /// <summary>收拢贴体（钻沙/掠冲）：四关节沿体轴向后掠平</summary>
        private void UpdateTuck(ref Leg leg, int li, Vector2 hip, Vector2 chainVec, Vector2 normal) {
            leg.Planted = false;
            leg.Swinging = false;
            Vector2 fold = hip - chainVec * (32f + li / 2 * 7f + (li & 1) * 9f) + normal * 8f;
            leg.Foot = Vector2.Lerp(leg.Foot, fold, 0.28f);
        }

        /// <summary>立起姿态（前二站）：螳螂式收折，慢波轻摆</summary>
        private void UpdateRaise(ref Leg leg, int li, Vector2 hip, Vector2 normal, FssStateContext ctx) {
            leg.Planted = false;
            leg.Swinging = false;
            int station = li / 2;
            float lift = MathHelper.Clamp(ctx.FrontRaise, 0f, 1f);
            Vector2 pose = hip
                + new Vector2(travelDir * (42f + station * 18f - (li & 1) * 10f), -24f - 46f * lift)
                + normal * 11f
                + new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + li * 2.3f) * 6f, 0f);
            leg.Foot = Vector2.Lerp(leg.Foot, pose, 0.16f);
        }

        /// <summary>
        /// 世界落足步行：足端钉世界落点，漂移/伸展超限且节律窗轮到本腿时换步；
        /// 高速滑刹（锚点随体滑移 + 犁沙）；髋没入地下自动收拢；够不着地腾空卷曲。
        /// </summary>
        private void UpdateWalk(ref Leg leg, int li, Vector2 hip, Vector2 normal, Vector2 chainVec, FssStateContext ctx) {
            bool forceAir = ctx.LegCommand == FssLegCommand.Flail;
            int station = li / 2;

            Vector2 restProbe = hip + normal * (MaxReach * RestReach * StrideAccent[station]);
            float groundY = GroundAt(restProbe.X, hip.Y);

            //髋没入地下：自动收拢（钻沙途中残留步行指令的兜底）
            if (groundY < hip.Y - 10f) {
                UpdateTuck(ref leg, li, hip, chainVec, normal);
                return;
            }

            float groundDist = groundY - hip.Y;
            bool plantable = !forceAir && leg.Groundness > 0.3f
                && groundDist < MaxReach * 0.95f && groundDist >= -10f;
            if (!plantable) {
                AirCurl(ref leg, li, hip, normal, ctx);
                return;
            }

            Vector2 rest = new(restProbe.X, groundY);
            float speed = ctx.Npc.velocity.Length();
            float skate = MathHelper.Clamp((speed - SkateStart) / (SkateFull - SkateStart), 0f, 1f);

            if (leg.Swinging) {
                AdvanceSwing(ref leg, li, ctx);
                return;
            }

            if (!leg.Planted) {
                //从空中/其他姿态回到步行：远则快摆落位，近则就地落桩
                if (Vector2.Distance(leg.Foot, rest) > 14f) {
                    BeginSwing(ref leg, rest, 8f, 14f);
                    return;
                }
                leg.PlantPos = rest;
                leg.Planted = true;
            }

            //滑刹：锚点随体滑移（部分抓地），滑差犁出腐沙痕
            if (skate > 0.01f) {
                leg.PlantPos.X += ctx.Npc.velocity.X * skate * 0.8f;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(leg.PlantPos + new Vector2(Main.rand.NextFloat(-4f, 4f), -2f),
                        DustID.Sand,
                        new Vector2(-travelDir * Main.rand.NextFloat(1.4f, 2.8f) * skate, -Main.rand.NextFloat(0.6f, 1.8f)),
                        120, FssVfx.TaintedSand, Main.rand.NextFloat(0.7f, 1.1f) * (0.7f + 0.5f * skate));
                    d.noGravity = false;
                }
            }

            //地形跟随：小落差贴、大落差紧急换步
            float plantGroundY = GroundAt(leg.PlantPos.X, hip.Y);
            float groundGap = Math.Abs(plantGroundY - leg.PlantPos.Y);
            if (groundGap < 18f) {
                leg.PlantPos.Y = plantGroundY;
                groundGap = 0f;
            }

            float drift = Vector2.Distance(leg.PlantPos, rest);
            float stretch = Vector2.Distance(hip, leg.PlantPos) / MaxReach;
            float trigger = StrideTrigger * (1f + skate * 1.2f);
            bool emergency = stretch > EmergencyStretch || groundGap >= 18f;
            bool wantStep = drift > trigger || emergency;
            float t01 = SlotPhase01(li, ctx);
            bool waveOpen = t01 < 0.5f;
            bool partnerSwinging = legs[li ^ 1].Swinging;

            if (wantStep && (emergency || (waveOpen && !partnerSwinging))) {
                Vector2 target = rest + ctx.Npc.velocity * StepLead;
                Vector2 hipFuture = hip + ctx.Npc.velocity * (StepLead * 0.5f);
                Vector2 fromHip = target - hipFuture;
                float lim = MaxReach * 0.82f;
                if (fromHip.Length() > lim) {
                    target = hipFuture + fromHip.SafeNormalize(Vector2.UnitY) * lim;
                }
                target.Y = GroundAt(target.X, hip.Y);

                //踉跄：确定性哈希低概率落短（补步在落地时接续）
                leg.CatchUpStep = false;
                if (StumbleHash(li, leg.StepCount) % StumbleOneIn == 0) {
                    target.X -= travelDir * StumbleShortPx;
                    target.Y = GroundAt(target.X, hip.Y);
                    leg.CatchUpStep = true;
                }

                float distStep = Vector2.Distance(leg.Foot, target);
                float dur = MathHelper.Clamp(9f + distStep / 18f, 8f, 19f) * (1f - skate * 0.3f);
                float clearance = StepClearance * (0.75f + 0.3f * StrideAccent[station])
                    * (1f + skate * 0.5f);
                BeginSwing(ref leg, target, dur, clearance);
                return;
            }

            leg.Foot = leg.PlantPos;
        }

        /// <summary>起一步摆越（常规换步/回步行落位/踉跄补步共用）</summary>
        private static void BeginSwing(ref Leg leg, Vector2 target, float dur, float clearance) {
            leg.SwingFrom = leg.Foot;
            leg.SwingTo = target;
            leg.SwingT = 0f;
            leg.SwingDur = dur;
            leg.SwingClearance = clearance;
            leg.Swinging = true;
            leg.Planted = false;
        }

        /// <summary>摆越推进：预备下压 → 抛物摆越 → 落地踏拍（踉跄步落地立刻接快速补步）</summary>
        private void AdvanceSwing(ref Leg leg, int li, FssStateContext ctx) {
            leg.SwingT += 1f / Math.Max(leg.SwingDur, 4f);
            float t = Math.Min(leg.SwingT, 1f);

            const float PressEnd = 0.18f;
            Vector2 pos;
            if (t < PressEnd) {
                float press = MathF.Sin(t / PressEnd * MathHelper.Pi) * 3f;
                pos = leg.SwingFrom + new Vector2(0f, press);
            }
            else {
                float m = (t - PressEnd) / (1f - PressEnd);
                float horiz = m * m * (3f - 2f * m);
                pos = Vector2.Lerp(leg.SwingFrom, leg.SwingTo, horiz);
                pos.Y -= MathF.Sin(m * MathHelper.Pi) * leg.SwingClearance;
            }

            float gy = GroundAt(pos.X, pos.Y - 60f);
            pos.Y = Math.Min(pos.Y, gy);
            leg.Foot = pos;

            if (leg.SwingT < 1f) {
                return;
            }

            leg.Swinging = false;
            leg.Planted = true;
            leg.PlantPos = leg.SwingTo;
            leg.Foot = leg.SwingTo;
            leg.StepCount++;

            int station = li / 2;
            float weight = (li & 1) == 0 ? 1f : 0.7f;
            ctx.StationBob[station] = Math.Max(ctx.StationBob[station], weight);
            EmitPlant(ref leg, ctx);

            //踉跄补步：落短的脚立刻快速跟半步（病态的不齐整读数）
            if (leg.CatchUpStep) {
                leg.CatchUpStep = false;
                Vector2 catchUp = leg.PlantPos + new Vector2(travelDir * StumbleShortPx, 0f);
                catchUp.Y = GroundAt(catchUp.X, leg.Hip.Y);
                BeginSwing(ref leg, catchUp, 6f, 9f);
            }
        }

        /// <summary>腾空卷曲：四关节相位错拍的抓挠，倾角/半径双频异速（非放射划桨）</summary>
        private void AirCurl(ref Leg leg, int li, Vector2 hip, Vector2 normal, FssStateContext ctx) {
            leg.Planted = false;
            leg.Swinging = false;
            int station = li / 2;
            float ph = SlotPhase01(li, ctx) * MathHelper.TwoPi;
            float rotSign = (li & 1) == 0 ? -1f : 1f;
            float tilt = (MathF.Sin(ph) * 0.6f + travelDir * 0.12f) * rotSign;
            float radius = MaxReach * (0.42f + 0.11f * MathF.Sin(ph * 2f + station * 1.3f));
            Vector2 target = hip + normal.RotatedBy(tilt) * radius;
            leg.Foot = Vector2.Lerp(leg.Foot, target, 0.18f);
        }

        /// <summary>该腿的节律槽相位 0..1：站序波（前→后）+ 同站两侧反相</summary>
        private static float SlotPhase01(int li, FssStateContext ctx) {
            float phase = ctx.GaitPhase - li / 2 * StationLag + ((li & 1) == 1 ? MathHelper.Pi : 0f);
            phase %= MathHelper.TwoPi;
            if (phase < 0f) {
                phase += MathHelper.TwoPi;
            }
            return phase / MathHelper.TwoPi;
        }

        /// <summary>确定性踉跄哈希（各端同算：站位 + 步数）</summary>
        private static int StumbleHash(int li, int stepCount) {
            unchecked {
                int h = li * 374761393 + stepCount * 668265263;
                h ^= h >> 13;
                return h & int.MaxValue;
            }
        }

        /// <summary>落地踏拍：湿重的腐沙 + 偶发灵液溅（跗节拍地的"啪嗒"）</summary>
        private static void EmitPlant(ref Leg leg, FssStateContext ctx) {
            if (Main.dedServ || ctx.Npc.velocity.Length() < 2f) {
                return;
            }
            float power = MathHelper.Clamp(ctx.Npc.velocity.Length() / 14f, 0.4f, 1.3f);
            for (int k = 0; k < 5; k++) {
                Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f),
                    DustID.Sand,
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), -Main.rand.NextFloat(0.8f, 2.4f) * power),
                    110, FssVfx.TaintedSand, Main.rand.NextFloat(0.8f, 1.25f));
                d.noGravity = false;
            }
            if (Main.rand.NextBool(3)) {
                Dust gold = Dust.NewDustPerfect(leg.Foot, DustID.Ichor,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 2.2f)),
                    30, default, Main.rand.NextFloat(0.7f, 1f));
                gold.noGravity = false;
            }
        }

        /// <summary>足下探地：从参考高度向下扫第一格实心面</summary>
        private static float GroundAt(float x, float refY) {
            return FssVfx.FindGroundY(new Vector2(x, refY - 50f), 500f);
        }
        #endregion

        #region IK 解算
        /// <summary>
        /// 四节解析 IK：先按姿态铺放跗节反解踝点（贴地 = 脚掌沿地面向体后平铺，
        /// 悬空 = 顺势收拢），基节朝踝点限幅摆动（全伸放开直指），腿节+胫节双骨余弦；
        /// 膝弯偏好随走地权重从体后向天顶连续过渡（高膝拱），分支迟滞防抖。
        /// 痉挛微颤叠在解算后的关节上（高频小抖与步行大周期分层）。
        /// </summary>
        private void SolveLeg(ref Leg leg, Vector2 hip, Vector2 normal) {
            Vector2 foot = leg.Foot;
            Vector2 d = foot - hip;
            float dist = d.Length();
            if (dist < 1f) {
                d = normal;
                dist = 1f;
            }
            float maxD = MaxReach * 0.995f;
            if (dist > maxD) {
                foot = hip + d * (maxD / dist);
                leg.Foot = foot;
            }

            //跗节铺放：贴地脚掌趾端在前、踝在后上（湿重的平footed 读数）；
            //悬空/摆越沿髋足向收拢
            Vector2 tarsusDir;
            if (leg.Planted && leg.Groundness > 0.4f) {
                tarsusDir = new Vector2(travelDir, 0.34f * (1f - leg.Limp)).SafeNormalize(Vector2.UnitX);
            }
            else {
                tarsusDir = (foot - hip).SafeNormalize(Vector2.UnitY);
            }
            Vector2 ankle = foot - tarsusDir * TarsusLen;

            //踝点钳进基+腿+胫可及圈
            Vector2 toAnkle = ankle - hip;
            float ankleDist = toAnkle.Length();
            float ankleMax = (CoxaLen + FemurLen + TibiaLen) * 0.995f;
            if (ankleDist > ankleMax) {
                ankle = hip + toAnkle * (ankleMax / ankleDist);
                toAnkle = ankle - hip;
                ankleDist = ankleMax;
            }
            if (ankleDist < 1f) {
                toAnkle = normal;
                ankleDist = 1f;
            }
            Vector2 ankleDir = toAnkle / ankleDist;

            //基节：休息向 = 法线，朝踝点限幅摆动；伸展吃紧时放开直指
            float baseAng = normal.ToRotation();
            float wantAng = ankleDir.ToRotation();
            float delta = MathHelper.WrapAngle(wantAng - baseAng);
            float stretch01 = MathHelper.Clamp(
                (ankleDist - (FemurLen + TibiaLen - 12f)) / (CoxaLen + 12f), 0f, 1f);
            float swingMax = MathHelper.Lerp(CoxaSwingMax, MathHelper.Pi, stretch01);
            float coxaAng = baseAng + MathHelper.Clamp(delta, -swingMax, swingMax);
            if (leg.Limp > 0.05f) {
                coxaAng = coxaAng.AngleLerp(MathHelper.PiOver2, leg.Limp * 0.6f);
            }
            Vector2 coxaTip = hip + coxaAng.ToRotationVector2() * CoxaLen;

            //腿节 + 胫节双骨
            Vector2 e = ankle - coxaTip;
            float eLen = MathHelper.Clamp(e.Length(), 8f, FemurLen + TibiaLen - 2f);
            float eAng = e.ToRotation();
            float cosA = MathHelper.Clamp(
                (FemurLen * FemurLen + eLen * eLen - TibiaLen * TibiaLen) / (2f * FemurLen * eLen), -1f, 1f);
            float phi = MathF.Acos(cosA);

            Vector2 pref = leg.Back * (1f - leg.Groundness * 0.75f)
                + new Vector2(0f, -1f) * (0.25f + leg.Groundness * 1.1f);
            pref = pref.SafeNormalize(-Vector2.UnitY);
            float dotP = Vector2.Dot((eAng + phi).ToRotationVector2(), pref);
            float dotM = Vector2.Dot((eAng - phi).ToRotationVector2(), pref);
            int want = dotP >= dotM ? 1 : -1;
            if (leg.KneeSign == 0 || want != leg.KneeSign && Math.Abs(dotP - dotM) > 0.12f) {
                leg.KneeSign = want;
            }
            float kneeAng = eAng + leg.KneeSign * phi;
            Vector2 knee = coxaTip + kneeAng.ToRotationVector2() * FemurLen;
            Vector2 solvedAnkle = coxaTip + eAng.ToRotationVector2() * eLen;
            Vector2 solvedFoot = solvedAnkle + tarsusDir * TarsusLen;

            //痉挛微颤：高频小幅逐关节错相（病态底噪；足端踩定时趾端不抖，抖在膝踝）
            float tw = Main.GlobalTimeWrappedHourly;
            int li = leg.StepCount; //仅作相位盐，无逻辑意义
            Vector2 twitch = new(MathF.Sin(tw * 9.3f + li * 2.9f), MathF.Sin(tw * 11.7f + li * 1.3f));
            knee += twitch * TwitchAmp;
            solvedAnkle += twitch.RotatedBy(1.3f) * (TwitchAmp * 0.8f);

            leg.CoxaTip = coxaTip;
            leg.Knee = knee;
            leg.Ankle = solvedAnkle;
            leg.DrawFoot = leg.Planted ? foot : solvedFoot;
        }
        #endregion

        #region 绘制
        /// <summary>
        /// 画十腿：按走地权重升序绘制——背侧/悬空排先画且压暗略细，走地排后画且全亮。
        /// 绘制髋叠加该站落步下沉量而足端不动 → 抓地瞬间支撑腿被压短。
        /// 统一乘 SkinMul 手染坏死紫（与整链手染回退同源）。
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
                ref Leg leg = ref legs[li];
                if (!leg.Visible || !leg.Inited) {
                    continue;
                }
                float groundness = leg.Groundness;
                Color light = Lighting.GetColor((int)(leg.Hip.X / 16f), (int)(leg.Hip.Y / 16f));
                float dim = MathHelper.Lerp(0.62f, 1f, groundness) * (1f - leg.Limp * 0.35f);
                Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255)
                    .MultiplyRGB(FssVfx.SkinMul) * fade;

                Vector2 hip = leg.Hip + new Vector2(0f, ctx.StationBob[li / 2] * StationDipPx);
                float thick = MathHelper.Lerp(0.95f, 1.05f, groundness);

                DrawBone(sb, upperTex, hip, leg.CoxaTip, 1.4f * thick, tint, screenPos);
                DrawBone(sb, upperTex, leg.CoxaTip, leg.Knee, 1.1f * thick, tint, screenPos);
                DrawBone(sb, lowerTex, leg.Knee, leg.Ankle, 0.95f * thick, tint, screenPos);
                DrawBone(sb, lowerTex, leg.Ankle, leg.DrawFoot, 0.72f * thick, tint, screenPos);
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
        #endregion
    }
}
