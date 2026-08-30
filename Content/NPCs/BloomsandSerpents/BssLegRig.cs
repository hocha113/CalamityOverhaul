using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 三节步足系统（纯表现层）。联机契约沿用旧版：各端从已同步的体节位置本地重建，
    /// 不入网络包，无 gameplay 碰撞。
    ///
    /// 编制：4 髋站 x 两侧翻缘 = 8 条三节长肢（基节/腿节/胫爪 + 爪尖微节，触及 ~130px）。
    /// 每条腿固定锚在体节体轴的一侧法线上（手性随体轴连续），身体水平时一排在地侧
    /// 一排在背侧，竖直时两排向左右张开。
    ///
    /// 运动模型是"世界落足步行"（与坟灾虫臂的体坐标划桨相反的构造）：足端钉在世界
    /// 固定点，身体从上面驶过；髋足漂移超过步幅、且蜈蚣节律波（站序滞后 + 同站反相）
    /// 轮到本腿时抬腿换步——预备下压半拍 → 抛物摆越（落点沿速度前瞻 + 探地）→
    /// 落地爪咬（回填 <see cref="BssStateContext.StationBob"/> 下沉 + 咬沙尘）。
    /// 高速滑刹：体速超过步频承受上限时足端拖滑犁沙（爪尖犁出连续沙痕），
    /// 读作"快到来不及迈步"。
    ///
    /// IK：基节朝足端限幅摆动（全伸时放开直指），腿节+胫爪双骨余弦解析；
    /// 膝弯偏好随走地权重从体后向天顶连续过渡（节肢动物高膝拱），带迟滞防抖。
    /// 爪尖第四微节：落地咬地、摆越后拖、腾空内卷。
    ///
    /// 图鉴沙盒共用本类：SetStation + Advance 由 <see cref="OtherMods.BossChecklist.SerpentPortraitRig"/>
    /// 驱动，探地换虚拟沙线。贴图占位 = 尾节素材拉伸，用户贴图到位后覆盖
    /// LegUpper/LegLower/LegClaw 三张 png，本文件不动。
    /// </summary>
    internal class BssLegRig
    {
        #region 编制与解剖
        /// <summary>髋站数（每站两侧各一条腿；死亡演出逐站瘫软按此计数）</summary>
        public const int LegCount = 4;
        /// <summary>腿总数（li = 站号*2 + 侧位，偶数 = +法线侧，奇数 = −法线侧）</summary>
        private const int TotalLegs = LegCount * 2;

        /// <summary>髋锚体节链序（长腿需要更大的间距铺开；落步下沉采样共用）</summary>
        internal static readonly int[] StationOrdinals = { 1, 4, 7, 10 };
        /// <summary>各站步幅性格差（去机械感：每站步子大小略有不同）</summary>
        private static readonly float[] StrideAccent = { 1.06f, 0.93f, 1.02f, 0.9f };

        /// <summary>基节长（髋部摆节，短而粗）</summary>
        internal const float CoxaLen = 26f;
        /// <summary>腿节长</summary>
        internal const float FemurLen = 54f;
        /// <summary>胫爪长</summary>
        internal const float TibiaLen = 56f;
        /// <summary>爪尖微节长（纯绘制第四节）</summary>
        internal const float ClawLen = 13f;
        /// <summary>全肢触及</summary>
        internal const float MaxReach = CoxaLen + FemurLen + TibiaLen - 6f;

        /// <summary>站间相位差：换步许可窗前→后传的蜈蚣节律</summary>
        internal const float StationLag = MathHelper.TwoPi * 0.22f;
        /// <summary>落步下沉的绘制像素基数（体节/头/髋压缩共用）</summary>
        internal const float StationDipPx = 6f;
        #endregion

        #region 步行调参
        /// <summary>足端休息半径（占全肢触及比例）</summary>
        private const float RestReach = 0.56f;
        /// <summary>髋足漂移触发换步的距离</summary>
        private const float StrideTrigger = 44f;
        /// <summary>落点沿速度前瞻帧数（步子迈向将到之处）</summary>
        private const float StepLead = 9f;
        /// <summary>摆越离地余隙</summary>
        private const float StepClearance = 30f;
        /// <summary>强制换步的伸展比（相对 MaxReach；不等节律窗）</summary>
        private const float EmergencyStretch = 0.93f;
        /// <summary>基节相对法线的摆动限幅（弧度；关节感的来源）</summary>
        private const float CoxaSwingMax = 1.15f;
        /// <summary>滑刹渐入速度（px/f）</summary>
        private const float SkateStart = 11f;
        /// <summary>滑刹全开速度</summary>
        private const float SkateFull = 20f;
        #endregion

        /// <summary>驱动一帧腿部模拟的环境包（战斗端从 ctx 建，图鉴端自建）</summary>
        internal struct LegEnv
        {
            public BssLegCommand Command;
            public float FrontRaise;
            public int CollapsedLegs;
            public float GaitPhase;
            public Vector2 HostVelocity;
            /// <summary>探地：(x 世界坐标, 起扫参考 Y) → 地面 Y</summary>
            public Func<float, float, float> GroundAt;
            /// <summary>落步回填（station, weight），可空</summary>
            public Action<int, float> OnPlant;
            /// <summary>沙效出口（pos, vel, power）；图鉴喂 motes 用，战斗置空走 Dust</summary>
            public Action<Vector2, Vector2, float> SandFx;
            /// <summary>是否允许直接出 Dust（战斗客户端）</summary>
            public bool AllowDust;
            /// <summary>柱面抓握几何（Grip 指令时有效）</summary>
            public bool GripActive;
            public float GripCenterX;
            public float GripHalfWidth;
            public float GripTopY;
            public float GripBottomY;
        }

        private struct Leg
        {
            /// <summary>当前足端（世界坐标）</summary>
            public Vector2 Foot;
            /// <summary>落点锚（Planted 时足端钉在这里）</summary>
            public Vector2 PlantPos;
            public Vector2 SwingFrom;
            public Vector2 SwingTo;
            public float SwingT;
            public float SwingDur;
            public float SwingClearance;
            /// <summary>摆越弧的抬升方向（地面 = 上，柱面 = 离壁）</summary>
            public Vector2 SwingUp;
            public bool Planted;
            public bool Swinging;
            public bool Inited;
            /// <summary>走地权重 0..1（法线朝下程度；亮度/绘制序过渡依据）</summary>
            public float Groundness;
            /// <summary>失力度 0..1（死亡演出）</summary>
            public float Limp;
            /// <summary>IK 膝弯分支迟滞（±1，0 = 未定）</summary>
            public int KneeSign;
            /// <summary>犁沙热度 0..1（滑刹表现渐进渐出）</summary>
            public float DragHeat;
            /// <summary>爪尖角（平滑量）</summary>
            public float ClawAng;
            public bool Visible;
            /// <summary>体后方向（−链向，随体节逐帧刷新）</summary>
            public Vector2 Back;
            //绘制缓存（Advance 解算、Draw 消费）
            public Vector2 Hip;
            public Vector2 CoxaTip;
            public Vector2 Knee;
            public Vector2 DrawFoot;
        }

        private readonly Leg[] legs = new Leg[TotalLegs];
        /// <summary>平滑行进方向（步幅前瞻与姿态依据，避免转身瞬间腿抽搐）</summary>
        private float travelDir = 1f;

        //站宿主位姿（Advance 前由驱动方预填）
        private readonly Vector2[] stationPos = new Vector2[LegCount];
        private readonly float[] stationRot = new float[LegCount];
        private readonly bool[] stationOk = new bool[LegCount];

        //战斗端缓存委托（避免逐帧分配）
        private BssStateContext boundCtx;
        private Func<float, float, float> battleGroundAt;
        private Action<int, float> battlePlant;

        #region 驱动入口
        /// <summary>预填一站宿主位姿（rotation 用体节绘制旋转约定）</summary>
        public void SetStation(int station, Vector2 center, float rotation, bool ok) {
            stationPos[station] = center;
            stationRot[station] = rotation;
            stationOk[station] = ok;
        }

        /// <summary>全腿复位（图鉴沙盒循环重启用：足端待首帧重新落位，防旧位置拉丝）</summary>
        public void ResetLegs() {
            for (int li = 0; li < TotalLegs; li++) {
                legs[li].Inited = false;
                legs[li].Visible = false;
            }
        }

        /// <summary>战斗端本帧腿部模拟（客户端与单人；服务端由调用方拦掉）</summary>
        public void Update(BssStateContext ctx) {
            boundCtx = ctx;
            battleGroundAt ??= (x, refY) => BssVfx.FindGroundY(new Vector2(x, refY), 460f);
            battlePlant ??= (station, weight) => {
                if (boundCtx != null) {
                    boundCtx.StationBob[station] = Math.Max(boundCtx.StationBob[station], weight);
                }
            };

            for (int st = 0; st < LegCount; st++) {
                int ordinal = StationOrdinals[st];
                NPC seg = ordinal < ctx.Segments.Count ? ctx.Segments[ordinal] : null;
                bool ok = seg != null && seg.active;
                SetStation(st, ok ? seg.Center : Vector2.Zero, ok ? seg.rotation : 0f, ok);
            }

            LegEnv env = new() {
                Command = ctx.LegCommand,
                FrontRaise = ctx.FrontRaise,
                CollapsedLegs = ctx.CollapsedLegs,
                GaitPhase = ctx.GaitPhase,
                HostVelocity = ctx.Npc.velocity,
                GroundAt = battleGroundAt,
                OnPlant = battlePlant,
                SandFx = null,
                AllowDust = !Main.dedServ,
                GripActive = ctx.LegGripActive,
                GripCenterX = ctx.LegGripCenterX,
                GripHalfWidth = ctx.LegGripHalfWidth,
                GripTopY = ctx.LegGripTopY,
                GripBottomY = ctx.LegGripBottomY,
            };
            Advance(in env);
        }

        /// <summary>共用模拟核心：按预填站位推进全部腿（战斗与图鉴同一套）</summary>
        public void Advance(in LegEnv env) {
            if (Math.Abs(env.HostVelocity.X) > 1.2f) {
                travelDir = MathHelper.Lerp(travelDir, Math.Sign(env.HostVelocity.X), 0.08f);
            }

            for (int li = 0; li < TotalLegs; li++) {
                int station = li / 2;
                ref Leg leg = ref legs[li];
                if (!stationOk[station]) {
                    leg.Visible = false;
                    continue;
                }
                leg.Visible = true;

                //体轴与翻缘：链向角 = rotation + PiOver2；法线取固定手性的垂线 x 侧位符号
                float chainDir = stationRot[station] + MathHelper.PiOver2;
                Vector2 chainVec = chainDir.ToRotationVector2();
                float flankSign = (li & 1) == 0 ? 1f : -1f;
                Vector2 normal = (chainDir + MathHelper.PiOver2).ToRotationVector2() * flankSign;
                Vector2 hip = stationPos[station] + normal * 10f;

                leg.Hip = hip;
                leg.Back = -chainVec;
                //走地/呈现权重：法线朝下程度（±0.6 对称带）
                leg.Groundness = MathHelper.Clamp((normal.Y + 0.6f) / 1.2f, 0f, 1f);

                //足端初始化逐腿做在"首次见到宿主体节"时（防默认 (0,0) 拉丝）
                if (!leg.Inited) {
                    Vector2 f0 = hip + normal * (MaxReach * RestReach);
                    f0.Y = Math.Min(f0.Y, env.GroundAt(f0.X, hip.Y - 46f));
                    leg.Foot = f0;
                    leg.PlantPos = f0;
                    leg.Planted = true;
                    leg.Swinging = false;
                    leg.Limp = 0f;
                    leg.ClawAng = MathHelper.PiOver2;
                    leg.Inited = true;
                }

                bool limpDecay = true;
                if (env.Command == BssLegCommand.Collapse && StationCollapsed(li, env)) {
                    UpdateCollapse(ref leg, li, hip, in env);
                    limpDecay = false;
                }
                else if (env.Command == BssLegCommand.Tuck) {
                    UpdateTuck(ref leg, li, hip, chainVec, normal);
                }
                else if (env.Command == BssLegCommand.Raise && station < 2) {
                    UpdateRaise(ref leg, li, hip, normal, in env);
                }
                else if (env.Command == BssLegCommand.Grip && env.GripActive) {
                    UpdateGrip(ref leg, li, hip, chainVec, normal, in env);
                }
                else {
                    //March 步行 / Brace 蹲伏 / Flail 强制腾空 / Raise 后二站 / Collapse 未失力站
                    UpdateWalk(ref leg, li, hip, normal, chainVec, in env);
                }
                if (limpDecay) {
                    leg.Limp = MathHelper.Clamp(leg.Limp - 0.05f, 0f, 1f);
                }

                SolveLeg(ref leg, hip, normal, in env);
            }
        }
        #endregion

        #region 姿态模组
        /// <summary>本腿所在站是否已失力（偶侧先瘫，奇侧等对腿软下去再跟）</summary>
        private bool StationCollapsed(int li, in LegEnv env) {
            if (li / 2 >= env.CollapsedLegs) {
                return false;
            }
            return (li & 1) == 0 || legs[li - 1].Limp > 0.35f;
        }

        /// <summary>失力：三节长肢垂软瘫散（重力向，背侧腿翻搭过身体），轻微摇晃</summary>
        private void UpdateCollapse(ref Leg leg, int li, Vector2 hip, in LegEnv env) {
            leg.Limp = MathHelper.Clamp(leg.Limp + ((li & 1) == 1 ? 0.05f : 0.06f), 0f, 1f);
            leg.Planted = false;
            leg.Swinging = false;
            Vector2 dangle = hip + new Vector2(
                travelDir * ((li & 1) == 1 ? 18f : 26f) + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + li * 1.7f) * 6f,
                MaxReach * 0.9f);
            dangle.Y = Math.Min(dangle.Y, env.GroundAt(dangle.X, hip.Y - 46f));
            leg.Foot = Vector2.Lerp(leg.Foot, dangle, 0.16f);
        }

        /// <summary>收拢贴体（钻沙/掠冲）：三关节沿体轴向后掠平，读出流线</summary>
        private void UpdateTuck(ref Leg leg, int li, Vector2 hip, Vector2 chainVec, Vector2 normal) {
            leg.Planted = false;
            leg.Swinging = false;
            Vector2 fold = hip - chainVec * (28f + li / 2 * 6f + (li & 1) * 8f) + normal * 7f;
            leg.Foot = Vector2.Lerp(leg.Foot, fold, 0.28f);
        }

        /// <summary>
        /// 立起姿态（前二站）：螳螂式收折——足端收到髋前上方近体处，膝弯偏好把
        /// 腿节顶成高拱、胫爪垂悬，比直线举升凶相得多。慢波轻摆。
        /// </summary>
        private void UpdateRaise(ref Leg leg, int li, Vector2 hip, Vector2 normal, in LegEnv env) {
            leg.Planted = false;
            leg.Swinging = false;
            int station = li / 2;
            float lift = MathHelper.Clamp(env.FrontRaise, 0f, 1f);
            Vector2 pose = hip
                + new Vector2(travelDir * (36f + station * 16f - (li & 1) * 9f), -20f - 40f * lift)
                + normal * 10f
                + new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + li * 2.3f) * 5f, 0f);
            leg.Foot = Vector2.Lerp(leg.Foot, pose, 0.16f);
        }

        /// <summary>
        /// 柱面抓握（盘柱攀爬）：足端锚到沙柱近壁面，用与步行同一套换步机（快步、
        /// 小步幅、摆越弧朝离壁向），身体螺旋上升时腿一路重新抓握。够不着壁面的腿收拢。
        /// </summary>
        private void UpdateGrip(ref Leg leg, int li, Vector2 hip, Vector2 chainVec, Vector2 normal, in LegEnv env) {
            float side = Math.Sign(hip.X - env.GripCenterX);
            if (side == 0f) {
                side = (li & 1) == 0 ? 1f : -1f;
            }
            Vector2 wallPoint = new(
                env.GripCenterX + side * env.GripHalfWidth,
                MathHelper.Clamp(hip.Y + 8f, env.GripTopY, env.GripBottomY));

            if (Vector2.Distance(hip, wallPoint) > MaxReach * 0.97f) {
                UpdateTuck(ref leg, li, hip, chainVec, normal);
                return;
            }

            if (leg.Swinging) {
                AdvanceSwing(ref leg, li, in env);
                return;
            }

            if (!leg.Planted) {
                leg.PlantPos = leg.Foot;
                leg.Planted = true;
            }

            float drift = Vector2.Distance(leg.PlantPos, wallPoint);
            float stretch = Vector2.Distance(hip, leg.PlantPos) / MaxReach;
            if (drift > 30f || stretch > EmergencyStretch) {
                BeginSwing(ref leg, wallPoint, 8f, 12f, new Vector2(-side, 0f));
                return;
            }
            leg.Foot = leg.PlantPos;
        }

        /// <summary>
        /// 世界落足步行（March/Brace；Flail 或够不着地时腾空卷曲）：
        /// 足端钉在世界落点上，身体驶过；漂移/伸展超限且节律窗轮到本腿时换步。
        /// 高速滑刹：落点锚随体滑移，滑差犁沙。
        /// </summary>
        private void UpdateWalk(ref Leg leg, int li, Vector2 hip, Vector2 normal, Vector2 chainVec, in LegEnv env) {
            bool brace = env.Command == BssLegCommand.Brace;
            bool forceAir = env.Command == BssLegCommand.Flail;
            int station = li / 2;

            float restReach = brace ? 0.64f : RestReach;
            Vector2 restProbe = hip + normal * (MaxReach * restReach * StrideAccent[station]);
            if (brace) {
                //蹲伏站距外扩：前后站沿行进向撑开，读出"绷住要跳"
                restProbe.X += travelDir * (station - 1.5f) * 14f;
            }
            float groundY = env.GroundAt(restProbe.X, hip.Y - 46f);

            //髋没入地下：自动收拢（钻沙途中残留步行指令的兜底）
            if (groundY < hip.Y - 10f) {
                UpdateTuck(ref leg, li, hip, chainVec, normal);
                return;
            }

            float groundDist = groundY - hip.Y;
            bool plantable = !forceAir && leg.Groundness > 0.3f
                && groundDist < MaxReach * 0.95f && groundDist >= -10f;
            if (!plantable) {
                AirCurl(ref leg, li, hip, normal, in env);
                return;
            }

            Vector2 rest = new(restProbe.X, groundY);
            float speed = env.HostVelocity.Length();
            float skate = brace ? 0f
                : MathHelper.Clamp((speed - SkateStart) / (SkateFull - SkateStart), 0f, 1f);

            if (leg.Swinging) {
                AdvanceSwing(ref leg, li, in env);
                return;
            }

            if (!leg.Planted) {
                //从空中/其他姿态回到步行：远则快摆落位（防瞬移贴地），近则就地落桩
                if (Vector2.Distance(leg.Foot, rest) > 14f) {
                    BeginSwing(ref leg, rest, 8f, 14f, -Vector2.UnitY);
                    return;
                }
                leg.PlantPos = rest;
                leg.Planted = true;
            }

            //滑刹：锚点随体滑移（部分抓地），滑差犁出连续沙痕
            if (skate > 0.01f) {
                leg.PlantPos.X += env.HostVelocity.X * skate * 0.8f;
                leg.DragHeat = MathHelper.Clamp(leg.DragHeat + 0.12f, 0f, 1f);
                EmitDrag(ref leg, in env, skate);
            }
            else {
                leg.DragHeat = MathHelper.Clamp(leg.DragHeat - 0.08f, 0f, 1f);
            }

            //地形跟随：小落差贴、大落差触发紧急换步
            float plantGroundY = env.GroundAt(leg.PlantPos.X, hip.Y - 46f);
            float groundGap = Math.Abs(plantGroundY - leg.PlantPos.Y);
            if (groundGap < 18f) {
                leg.PlantPos.Y = plantGroundY;
                groundGap = 0f;
            }

            //换步判定：漂移超步幅（节律窗内）或伸展/落差超限（紧急，不等窗）
            float drift = Vector2.Distance(leg.PlantPos, rest);
            float stretch = Vector2.Distance(hip, leg.PlantPos) / MaxReach;
            float trigger = (brace ? 26f : StrideTrigger) * (1f + skate * 1.2f);
            bool emergency = stretch > EmergencyStretch || groundGap >= 18f;
            bool wantStep = drift > trigger || emergency;
            float t01 = SlotPhase01(li, env.GaitPhase);
            bool waveOpen = t01 < 0.5f;
            bool partnerSwinging = legs[li ^ 1].Swinging;

            if (wantStep && (emergency || (waveOpen && !partnerSwinging))) {
                Vector2 target = rest + env.HostVelocity * StepLead;
                //落点钳在预测髋的可及圈内（不许迈出解剖极限）
                Vector2 hipFuture = hip + env.HostVelocity * (StepLead * 0.5f);
                Vector2 fromHip = target - hipFuture;
                float lim = MaxReach * 0.82f;
                if (fromHip.Length() > lim) {
                    target = hipFuture + fromHip.SafeNormalize(Vector2.UnitY) * lim;
                }
                target.Y = env.GroundAt(target.X, hip.Y - 46f);

                float distStep = Vector2.Distance(leg.Foot, target);
                float dur = MathHelper.Clamp(8f + distStep / 18f, brace ? 6f : 8f, 18f)
                    * (1f - skate * 0.3f);
                float clearance = StepClearance * (0.75f + 0.3f * StrideAccent[station])
                    * (1f + skate * 0.5f);
                BeginSwing(ref leg, target, dur, clearance, -Vector2.UnitY);
                return;
            }

            leg.Foot = leg.PlantPos;
        }

        /// <summary>起一步摆越（统一入口：常规换步/回步行落位/柱面重抓握共用）</summary>
        private static void BeginSwing(ref Leg leg, Vector2 target, float dur, float clearance, Vector2 up) {
            leg.SwingFrom = leg.Foot;
            leg.SwingTo = target;
            leg.SwingT = 0f;
            leg.SwingDur = dur;
            leg.SwingClearance = clearance;
            leg.SwingUp = up;
            leg.Swinging = true;
            leg.Planted = false;
        }

        /// <summary>摆越推进：预备下压 18% → 主摆（水平缓动 + 抛物离地）→ 落地爪咬</summary>
        private void AdvanceSwing(ref Leg leg, int li, in LegEnv env) {
            leg.SwingT += 1f / Math.Max(leg.SwingDur, 4f);
            float t = Math.Min(leg.SwingT, 1f);

            const float PressEnd = 0.18f;
            Vector2 pos;
            if (t < PressEnd) {
                //预备：足端原地向支撑向压一记（蓄力半拍，力量在离地前）
                float press = MathF.Sin(t / PressEnd * MathHelper.Pi) * 3f;
                pos = leg.SwingFrom - leg.SwingUp * press;
            }
            else {
                float m = (t - PressEnd) / (1f - PressEnd);
                float horiz = m * m * (3f - 2f * m);
                pos = Vector2.Lerp(leg.SwingFrom, leg.SwingTo, horiz);
                float arc = MathF.Sin(m * MathHelper.Pi);
                pos += leg.SwingUp * arc * leg.SwingClearance;
            }

            //摆越途中不许穿地（仅地面步态需要；柱面 SwingUp 为横向，探地钳制无意义）
            if (leg.SwingUp.Y < -0.5f) {
                float gy = env.GroundAt(pos.X, pos.Y - 60f);
                pos.Y = Math.Min(pos.Y, gy);
            }
            leg.Foot = pos;

            if (leg.SwingT >= 1f) {
                leg.Swinging = false;
                leg.Planted = true;
                leg.PlantPos = leg.SwingTo;
                leg.Foot = leg.SwingTo;

                int station = li / 2;
                float weight = (li & 1) == 0 ? 1f : 0.7f;
                env.OnPlant?.Invoke(station, weight);
                EmitPlant(ref leg, in env);
            }
        }

        /// <summary>
        /// 腾空卷曲（Flail/够不着地）：三关节相位错拍的"抓挠空气"——倾角与半径
        /// 双频异速调制画出折叠的抓握小环，与放射状划桨划清界限。
        /// </summary>
        private void AirCurl(ref Leg leg, int li, Vector2 hip, Vector2 normal, in LegEnv env) {
            leg.Planted = false;
            leg.Swinging = false;
            leg.DragHeat = MathHelper.Clamp(leg.DragHeat - 0.08f, 0f, 1f);
            int station = li / 2;
            float ph = SlotPhase01(li, env.GaitPhase) * MathHelper.TwoPi;
            float rotSign = (li & 1) == 0 ? -1f : 1f;
            float tilt = (MathF.Sin(ph) * 0.62f + travelDir * 0.12f) * rotSign;
            float radius = MaxReach * (0.42f + 0.11f * MathF.Sin(ph * 2f + station * 1.3f));
            Vector2 target = hip + normal.RotatedBy(tilt) * radius;
            leg.Foot = Vector2.Lerp(leg.Foot, target, 0.18f);
        }

        /// <summary>该腿的节律槽相位 0..1：站序波（前→后）+ 同站两侧反相</summary>
        private static float SlotPhase01(int li, float gaitPhase) {
            float phase = gaitPhase - li / 2 * StationLag + ((li & 1) == 1 ? MathHelper.Pi : 0f);
            phase %= MathHelper.TwoPi;
            if (phase < 0f) {
                phase += MathHelper.TwoPi;
            }
            return phase / MathHelper.TwoPi;
        }
        #endregion

        #region 沙效
        /// <summary>落地爪咬沙尘</summary>
        private static void EmitPlant(ref Leg leg, in LegEnv env) {
            float power = MathHelper.Clamp(env.HostVelocity.Length() / 14f, 0.4f, 1.3f);
            if (env.SandFx != null) {
                env.SandFx(leg.Foot, new Vector2(0f, -1.6f) * power, power);
                return;
            }
            if (!env.AllowDust || env.HostVelocity.Length() < 2f) {
                return;
            }
            for (int k = 0; k < 4; k++) {
                Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.8f, 2.2f) * power),
                    110, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }

        /// <summary>滑刹犁沙：沿滑差方向连续掀沙痕（打滑的可见证据）</summary>
        private void EmitDrag(ref Leg leg, in LegEnv env, float skate) {
            if (env.SandFx != null) {
                if (Main.rand.NextBool(3)) {
                    env.SandFx(leg.PlantPos, new Vector2(-travelDir * 1.6f, -1f) * skate, skate);
                }
                return;
            }
            if (!env.AllowDust || !Main.rand.NextBool(2)) {
                return;
            }
            Dust d = Dust.NewDustPerfect(leg.PlantPos + new Vector2(Main.rand.NextFloat(-4f, 4f), -2f),
                DustID.Sand,
                new Vector2(-travelDir * Main.rand.NextFloat(1.4f, 2.8f) * skate, -Main.rand.NextFloat(0.6f, 1.8f)),
                120, default, Main.rand.NextFloat(0.7f, 1.1f) * (0.7f + 0.5f * skate));
            d.noGravity = false;
        }
        #endregion

        #region IK 解算
        /// <summary>
        /// 三节解析 IK：基节朝足端限幅摆动（近距关节感、全伸放开直指），
        /// 腿节+胫爪双骨余弦；膝弯偏好 = 体后 → 天顶随走地权重连续过渡（高膝拱），
        /// 分支迟滞防抖。爪尖角按姿态平滑（落地咬地/摆越后拖/腾空内卷）。
        /// </summary>
        private void SolveLeg(ref Leg leg, Vector2 hip, Vector2 normal, in LegEnv env) {
            Vector2 d = leg.Foot - hip;
            float dist = d.Length();
            if (dist < 1f) {
                d = normal;
                dist = 1f;
            }
            float maxD = MaxReach * 0.995f;
            if (dist > maxD) {
                leg.Foot = hip + d * (maxD / dist);
                d = leg.Foot - hip;
                dist = maxD;
            }
            Vector2 dir = d / dist;

            //基节：休息向 = 法线，朝目标限幅摆动；伸展吃紧时放开限幅直指目标
            float baseAng = normal.ToRotation();
            float wantAng = dir.ToRotation();
            float delta = MathHelper.WrapAngle(wantAng - baseAng);
            float stretch01 = MathHelper.Clamp(
                (dist - (FemurLen + TibiaLen - 12f)) / (CoxaLen + 12f), 0f, 1f);
            float swingMax = MathHelper.Lerp(CoxaSwingMax, MathHelper.Pi, stretch01);
            //失力腿基节松脱：向重力向垂
            float coxaAng = baseAng + MathHelper.Clamp(delta, -swingMax, swingMax);
            if (leg.Limp > 0.05f) {
                coxaAng = coxaAng.AngleLerp(MathHelper.PiOver2, leg.Limp * 0.6f);
            }
            Vector2 coxaTip = hip + coxaAng.ToRotationVector2() * CoxaLen;

            //腿节 + 胫爪双骨
            Vector2 e = leg.Foot - coxaTip;
            float eLen = MathHelper.Clamp(e.Length(), 8f, FemurLen + TibiaLen - 2f);
            float eAng = e.ToRotation();
            float cosA = MathHelper.Clamp(
                (FemurLen * FemurLen + eLen * eLen - TibiaLen * TibiaLen) / (2f * FemurLen * eLen), -1f, 1f);
            float phi = MathF.Acos(cosA);

            //膝弯偏好：走地 → 天顶拱起，腾空/背侧 → 朝体后（连续量，分支带迟滞）
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
            Vector2 foot = coxaTip + eAng.ToRotationVector2() * eLen;

            leg.CoxaTip = coxaTip;
            leg.Knee = knee;
            leg.DrawFoot = foot;

            //爪尖角：落地顺胫爪续入支撑面（地面咬沙、柱面扣壁同一条规则）、
            //摆越沿行进后拖、腾空顺胫爪向内卷
            float clawTarget;
            if (leg.Planted) {
                clawTarget = (foot - knee).ToRotation();
            }
            else if (leg.Swinging) {
                Vector2 swingDir = leg.SwingTo - leg.SwingFrom;
                clawTarget = swingDir.LengthSquared() > 1f
                    ? (-swingDir).ToRotation()
                    : (foot - knee).ToRotation();
            }
            else {
                Vector2 tibiaDir = foot - knee;
                clawTarget = tibiaDir.ToRotation() + leg.KneeSign * 0.55f;
            }
            leg.ClawAng = leg.ClawAng.AngleLerp(clawTarget, 0.22f);
        }
        #endregion

        #region 绘制
        /// <summary>
        /// 战斗端画八腿：按走地权重升序绘制——背侧/悬空排先画且压暗略细，走地排后画
        /// 且全亮，全部压在头 PreDraw 层。绘制髋叠加该站落步下沉量而足端不动 →
        /// 抓地瞬间支撑腿被压短（重量读数）。
        /// </summary>
        public void Draw(SpriteBatch sb, Vector2 screenPos, BssStateContext ctx) {
            if (ctx.LegAlpha <= 0.03f) {
                return;
            }
            float fade = ctx.LegAlpha * (1f - ctx.Npc.alpha / 255f);
            if (fade <= 0.03f) {
                return;
            }

            Span<int> order = stackalloc int[TotalLegs];
            BuildDrawOrder(order);

            foreach (int li in order) {
                ref Leg leg = ref legs[li];
                if (!leg.Visible || !leg.Inited) {
                    continue;
                }
                Color light = Lighting.GetColor((int)(leg.Hip.X / 16f), (int)(leg.Hip.Y / 16f));
                float dim = MathHelper.Lerp(0.62f, 1f, leg.Groundness) * (1f - leg.Limp * 0.35f);
                Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255) * fade;
                float bob = ctx.StationBob[li / 2] * StationDipPx;
                DrawLeg(sb, in leg, screenPos, tint, bob);
            }
        }

        /// <summary>图鉴端画八腿（场景坐标，环境色由调用方给）</summary>
        public void DrawStandalone(SpriteBatch sb, Func<int, float, Color> tintFor) {
            Span<int> order = stackalloc int[TotalLegs];
            BuildDrawOrder(order);
            foreach (int li in order) {
                ref Leg leg = ref legs[li];
                if (!leg.Visible || !leg.Inited) {
                    continue;
                }
                DrawLeg(sb, in leg, Vector2.Zero, tintFor(li, leg.Groundness), 0f);
            }
        }

        /// <summary>按走地权重升序：暗排在底、亮排在面（角色过渡时序随之连续换层）</summary>
        private void BuildDrawOrder(Span<int> order) {
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
        }

        /// <summary>画一条腿的四段骨节（基节/腿节/胫爪/爪尖；髋端叠 bob 下沉，足端踩定）</summary>
        private static void DrawLeg(SpriteBatch sb, in Leg leg, Vector2 screenPos, Color tint, float bobPx) {
            Texture2D upperTex = BssHead.LegUpperAsset?.Value;
            Texture2D lowerTex = BssHead.LegLowerAsset?.Value;
            Texture2D clawTex = BssHead.LegClawAsset?.Value;
            if (upperTex == null || lowerTex == null) {
                return;
            }
            Vector2 hip = leg.Hip + new Vector2(0f, bobPx);
            float thick = MathHelper.Lerp(0.9f, 1f, leg.Groundness);

            DrawBone(sb, upperTex, hip, leg.CoxaTip, 1.35f * thick, tint, screenPos);
            DrawBone(sb, upperTex, leg.CoxaTip, leg.Knee, 1.05f * thick, tint, screenPos);
            DrawBone(sb, lowerTex, leg.Knee, leg.DrawFoot, 0.9f * thick, tint, screenPos);
            if (clawTex != null) {
                Vector2 clawEnd = leg.DrawFoot + leg.ClawAng.ToRotationVector2() * ClawLen;
                DrawBone(sb, clawTex, leg.DrawFoot, clawEnd, 0.62f * thick, tint, screenPos);
            }
        }

        /// <summary>骨节拉伸绘制：贴图约定尖端朝上（占位 = 尾节素材），底端锚在关节起点</summary>
        internal static void DrawBone(SpriteBatch sb, Texture2D tex, Vector2 from, Vector2 to,
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
