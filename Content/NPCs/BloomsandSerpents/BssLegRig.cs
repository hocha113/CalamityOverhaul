using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 三节步足系统（纯表现层）。联机契约：各端从已同步的体节位置本地重建，
    /// 不入网络包，无 gameplay 碰撞。
    ///
    /// 编制：4 髋站 x 两侧翻缘 = 8 条三节长肢（基节/腿节/胫爪 + 爪尖微节，贴图尺度触及 ~130px，
    /// 骨长/步幅/骨宽随 <see cref="Scale"/> 同比，战斗端取宿主 NPC.scale）。
    /// 每条腿固定锚在体节体轴的一侧法线上（手性随体轴连续），身体水平时一排在地侧
    /// 一排在背侧，竖直时两排向左右张开。
    ///
    /// 运动模型是节肢动物的"世界落足步行"：足端钉在世界固定点，身体从上面驶过；
    /// 足落在休息位前方半个步幅，随体位移漂到休息位后方半个步幅时抬腿换步——
    /// 一个步幅 = 一个步态时钟周期（<see cref="BssStateContext.GaitIncrement"/> 按此定义），
    /// 换步许可窗沿髋站前→后传播、同站两侧反相，蜈蚣的节律波由此闭合，
    /// 而不是靠伸展超限的应急换步凑出来。预备下压半拍 → 抛物摆越 → 落地爪咬。
    ///
    /// 肢体范围约束（沿用残酷月球领主手臂链的思路）：每个落足目标先钳进髋的可达包络
    /// （半径窗 + 相对法线的摆角窗），腿不许甩到前后极限；基节摆动限幅、膝弯有效跨距
    /// 有上下限（永不锁直、永不折死）、爪尖目标越界只钳制不拉伸。
    /// 高速滑刹：体速超过步频承受上限时足端拖滑犁沙，读作"快到来不及迈步"。
    ///
    /// 图鉴沙盒共用本类：SetStation + Advance 由 <see cref="OtherMods.BossChecklist.SerpentPortraitRig"/>
    /// 驱动，探地换虚拟沙线。贴图为步足正式稿（爪尖微节共用胫爪稿，槽位在 BssHead）。
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

        /// <summary>
        /// 骨长与步幅的整体倍率（战斗端每帧取宿主 NPC.scale，图鉴端保持 1）。
        /// 下面所有 *Base 常量都是贴图尺度的像素量，实际使用一律经属性乘过倍率
        /// </summary>
        public float Scale { get; set; } = 1f;

        /// <summary>基节长（髋部摆节，短而粗）</summary>
        private const float CoxaLenBase = 26f;
        /// <summary>腿节长</summary>
        private const float FemurLenBase = 54f;
        /// <summary>胫爪长</summary>
        private const float TibiaLenBase = 56f;
        /// <summary>爪尖微节长（纯绘制第四节）</summary>
        private const float ClawLenBase = 13f;
        /// <summary>全肢触及</summary>
        private const float MaxReachBase = CoxaLenBase + FemurLenBase + TibiaLenBase - 6f;
        /// <summary>髋锚离体轴的法向偏移</summary>
        private const float HipSideBase = 10f;

        private float CoxaLen => CoxaLenBase * Scale;
        private float FemurLen => FemurLenBase * Scale;
        private float TibiaLen => TibiaLenBase * Scale;
        private float MaxReach => MaxReachBase * Scale;

        /// <summary>站间相位差：换步许可窗前→后传的蜈蚣节律</summary>
        internal const float StationLag = MathHelper.TwoPi * 0.22f;
        /// <summary>落步下沉的绘制像素基数（体节/头/髋压缩共用）</summary>
        internal const float StationDipPx = 6f;
        #endregion

        #region 步行调参
        /// <summary>
        /// 步幅（贴图尺度）：足端从前落点漂到后抬点经历的体位移，约 0.74 倍全肢触及。
        /// 步态时钟一个周期 = 身体前进一个步幅
        /// </summary>
        internal const float StrideBase = 96f;
        /// <summary>世界步幅（战斗端：贴图步幅 × 整体放大），<see cref="BssStateContext.GaitIncrement"/> 读它</summary>
        internal static float StrideWorld => StrideBase * BssDirector.BodyScale;
        /// <summary>换步许可窗占时钟周期的比例（窗内且同站对腿落地才许抬）</summary>
        private const float StepWindow = 0.5f;
        /// <summary>足端休息半径（占全肢触及比例）</summary>
        private const float RestReach = 0.56f;
        /// <summary>落足目标可达包络：半径窗（占全肢触及；下限须容得下髋离地约 0.34 倍触及的贴地姿）</summary>
        private const float EnvelopeMin = 0.25f;
        private const float EnvelopeMax = 0.84f;
        /// <summary>落足目标可达包络：相对法线的摆角窗（弧度，约 ±57°）</summary>
        private const float EnvelopeSwing = 1.0f;
        /// <summary>摆越离地余隙（贴图尺度）</summary>
        private const float StepClearanceBase = 26f;
        /// <summary>探地起扫高度：从髋上方这么远向下扫地（贴图尺度）</summary>
        private const float GroundProbeLiftBase = 46f;

        private float StepClearance => StepClearanceBase * Scale;
        private float GroundProbeLift => GroundProbeLiftBase * Scale;
        /// <summary>强制换步的伸展比（相对 MaxReach；不等节律窗）</summary>
        private const float EmergencyStretch = 0.9f;
        /// <summary>基节相对法线的摆动限幅（弧度；关节感的来源）</summary>
        private const float CoxaSwingMax = 0.8f;
        /// <summary>
        /// 膝弯有效跨距窗（腿节+胫爪的合成距离占两骨之和）：上限杜绝锁直；
        /// 下限只防退化（贴地爬行时髋离地仅约 60px 而全肢 175px，长腿本就该高拱深折，
        /// 下限抬高会把足端画进地里）
        /// </summary>
        private const float KneeSpanMin = 0.12f;
        private const float KneeSpanMax = 0.94f;
        /// <summary>滑刹渐入速度（px/f；中速步行档 12 以内全程真迈步）</summary>
        private const float SkateStart = 15f;
        /// <summary>滑刹全开速度</summary>
        private const float SkateFull = 26f;
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
        /// <summary>平滑行进方向（单位向量；步幅前后判定与落点前瞻依据，避免转身瞬间腿抽搐）</summary>
        private Vector2 travel = Vector2.UnitX;
        /// <summary>平滑水平朝向（±1；立起/瘫软等姿态用）</summary>
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
            Scale = ctx.Npc.scale;
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
            if (env.HostVelocity.LengthSquared() > 1.2f * 1.2f) {
                travel = Vector2.Lerp(travel, env.HostVelocity.SafeNormalize(travel), 0.1f).SafeNormalize(Vector2.UnitX);
                if (Math.Abs(env.HostVelocity.X) > 1.2f) {
                    travelDir = MathHelper.Lerp(travelDir, Math.Sign(env.HostVelocity.X), 0.08f);
                }
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
                Vector2 hip = stationPos[station] + normal * (HipSideBase * Scale);

                leg.Hip = hip;
                leg.Back = -chainVec;
                //走地/呈现权重：法线朝下程度（±0.6 对称带）
                leg.Groundness = MathHelper.Clamp((normal.Y + 0.6f) / 1.2f, 0f, 1f);

                //足端初始化逐腿做在"首次见到宿主体节"时（防默认 (0,0) 拉丝）
                if (!leg.Inited) {
                    Vector2 f0 = hip + normal * (MaxReach * RestReach);
                    f0.Y = Math.Min(f0.Y, env.GroundAt(f0.X, hip.Y - GroundProbeLift));
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
                (travelDir * ((li & 1) == 1 ? 18f : 26f) + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + li * 1.7f) * 6f) * Scale,
                MaxReach * 0.9f);
            dangle.Y = Math.Min(dangle.Y, env.GroundAt(dangle.X, hip.Y - GroundProbeLift));
            leg.Foot = Vector2.Lerp(leg.Foot, dangle, 0.16f);
        }

        /// <summary>收拢贴体（钻沙/掠冲）：三关节沿体轴向后掠平，读出流线</summary>
        private void UpdateTuck(ref Leg leg, int li, Vector2 hip, Vector2 chainVec, Vector2 normal) {
            leg.Planted = false;
            leg.Swinging = false;
            Vector2 fold = hip - chainVec * ((28f + li / 2 * 6f + (li & 1) * 8f) * Scale) + normal * (7f * Scale);
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
                + new Vector2(travelDir * (36f + station * 16f - (li & 1) * 9f), -20f - 40f * lift) * Scale
                + normal * (10f * Scale)
                + new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + li * 2.3f) * 5f * Scale, 0f);
            leg.Foot = Vector2.Lerp(leg.Foot, pose, 0.16f);
        }

        /// <summary>
        /// 柱面抓握（盘柱攀爬）：足端锚到沙柱近壁面，用与步行同一套换步机（快步、小步幅），
        /// 身体沿柱面上升时腿一路重新抓握。够不着壁面的腿收拢贴体。
        /// 壁面点是柱心 ± 柱半宽、Y 钳在柱高区间：柱是 Actor 不是物块，摆越途中的探地钳制
        /// 打到的是远处地面，不干扰。
        /// </summary>
        private void UpdateGrip(ref Leg leg, int li, Vector2 hip, Vector2 chainVec, Vector2 normal, in LegEnv env) {
            float side = Math.Sign(hip.X - env.GripCenterX);
            if (side == 0f) {
                side = (li & 1) == 0 ? 1f : -1f;
            }
            Vector2 wallPoint = new(
                env.GripCenterX + side * env.GripHalfWidth,
                MathHelper.Clamp(hip.Y + 8f * Scale, env.GripTopY, env.GripBottomY));

            if (Vector2.Distance(hip, wallPoint) > MaxReach * 0.97f) {
                UpdateTuck(ref leg, li, hip, chainVec, normal);
                return;
            }
            leg.DragHeat = MathHelper.Clamp(leg.DragHeat - 0.08f, 0f, 1f);

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
            if (drift > 30f * Scale || stretch > EmergencyStretch) {
                BeginSwing(ref leg, wallPoint, 8f, 12f * Scale);
                return;
            }
            leg.Foot = leg.PlantPos;
        }

        /// <summary>
        /// 世界落足步行（March/Brace；Flail 或够不着地时腾空卷曲）：
        /// 足落在休息位前方半步幅，随身体驶过漂到后方半步幅时、且节律窗轮到本腿、
        /// 同站对腿落地，才抬腿换步；伸展/落差超限走应急换步（不等窗）。
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
                restProbe += travel * ((station - 1.5f) * 14f * Scale);
            }
            float groundY = env.GroundAt(restProbe.X, hip.Y - GroundProbeLift);

            //髋没入地下：自动收拢（钻沙途中残留步行指令的兜底）
            if (groundY < hip.Y - 10f * Scale) {
                UpdateTuck(ref leg, li, hip, chainVec, normal);
                return;
            }

            float groundDist = groundY - hip.Y;
            bool plantable = !forceAir && leg.Groundness > 0.3f
                && groundDist < MaxReach * 0.9f && groundDist >= -10f * Scale;
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
                if (Vector2.Distance(leg.Foot, rest) > 14f * Scale) {
                    Vector2 landing = ClampToEnvelope(hip, normal, rest);
                    landing.Y = env.GroundAt(landing.X, hip.Y - GroundProbeLift);
                    BeginSwing(ref leg, landing, 7f, 14f * Scale);
                    return;
                }
                leg.PlantPos = rest;
                leg.Planted = true;
            }

            //滑刹：锚点随体滑移（部分抓地），滑差犁出连续沙痕
            if (skate > 0.01f) {
                leg.PlantPos += env.HostVelocity * (skate * 0.8f);
                leg.DragHeat = MathHelper.Clamp(leg.DragHeat + 0.12f, 0f, 1f);
                EmitDrag(ref leg, in env, skate);
            }
            else {
                leg.DragHeat = MathHelper.Clamp(leg.DragHeat - 0.08f, 0f, 1f);
            }

            //地形跟随：小落差贴、大落差触发应急换步
            float plantGroundY = env.GroundAt(leg.PlantPos.X, hip.Y - GroundProbeLift);
            float groundGap = Math.Abs(plantGroundY - leg.PlantPos.Y);
            float stepDown = 18f * Scale;
            if (groundGap < stepDown) {
                leg.PlantPos.Y = plantGroundY;
                groundGap = 0f;
            }

            //步幅周期：足端沿行进向落到休息位后方半步幅即该抬；滑刹期允许拖得更远
            float stride = StrideBase * Scale * StrideAccent[station] * (brace ? 0.5f : 1f);
            float along = Vector2.Dot(leg.PlantPos - rest, travel);
            float stretch = Vector2.Distance(hip, leg.PlantPos) / MaxReach;
            bool emergency = stretch > EmergencyStretch || groundGap >= stepDown;
            bool behind = along < -0.5f * stride * (1f + skate * 0.8f);
            bool wantStep = behind || emergency;
            float t01 = SlotPhase01(li, env.GaitPhase);
            bool windowOpen = t01 < StepWindow;
            bool partnerSwinging = legs[li ^ 1].Swinging;

            if (wantStep && (emergency || (windowOpen && !partnerSwinging))) {
                //落点：休息位前方半步幅 + 少量速度前瞻，钳进预测髋的可达包络（不许迈出解剖极限）
                Vector2 target = rest + travel * (0.5f * stride) + env.HostVelocity * 2f;
                Vector2 hipFuture = hip + env.HostVelocity * 3f;
                target = ClampToEnvelope(hipFuture, normal, target);
                target.Y = env.GroundAt(target.X, hip.Y - GroundProbeLift);

                //摆越时长 ≈ 周期的四成（速度越快步子越快），蹲伏碎步更短
                float cycle = stride / Math.Max(speed, 3f);
                float dur = MathHelper.Clamp(cycle * 0.42f, brace ? 4f : 5f, 12f);
                float clearance = StepClearance * (0.8f + 0.25f * StrideAccent[station]) * (1f + skate * 0.4f);
                BeginSwing(ref leg, target, dur, clearance);
                return;
            }

            leg.Foot = leg.PlantPos;
        }

        /// <summary>
        /// 肢体范围包络：目标点钳进髋的可达圈（半径窗）与相对法线的摆角窗。
        /// 这是"腿不许乱甩"的几何声明：越界目标被拉回边界，而不是让骨链去够。
        /// </summary>
        private Vector2 ClampToEnvelope(Vector2 hip, Vector2 normal, Vector2 target) {
            Vector2 d = target - hip;
            float len = d.Length();
            float normalAng = normal.ToRotation();
            if (len < 1f) {
                return hip + normal * (MaxReach * EnvelopeMin);
            }
            float ang = MathHelper.Clamp(MathHelper.WrapAngle(d.ToRotation() - normalAng), -EnvelopeSwing, EnvelopeSwing);
            len = MathHelper.Clamp(len, MaxReach * EnvelopeMin, MaxReach * EnvelopeMax);
            return hip + (normalAng + ang).ToRotationVector2() * len;
        }

        /// <summary>起一步摆越（统一入口：常规换步/回步行落位共用；抬升方向 = 世界上）</summary>
        private static void BeginSwing(ref Leg leg, Vector2 target, float dur, float clearance) {
            leg.SwingFrom = leg.Foot;
            leg.SwingTo = target;
            leg.SwingT = 0f;
            leg.SwingDur = dur;
            leg.SwingClearance = clearance;
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
                float press = MathF.Sin(t / PressEnd * MathHelper.Pi) * 3f * Scale;
                pos = leg.SwingFrom + new Vector2(0f, press);
            }
            else {
                float m = (t - PressEnd) / (1f - PressEnd);
                float horiz = m * m * (3f - 2f * m);
                pos = Vector2.Lerp(leg.SwingFrom, leg.SwingTo, horiz);
                float arc = MathF.Sin(m * MathHelper.Pi);
                pos.Y -= arc * leg.SwingClearance;
            }

            //摆越途中不许穿地
            float gy = env.GroundAt(pos.X, pos.Y - 60f * Scale);
            pos.Y = Math.Min(pos.Y, gy);
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
        /// 双频异速调制画出折叠的抓握小环，幅度压在包络摆角窗内。
        /// </summary>
        private void AirCurl(ref Leg leg, int li, Vector2 hip, Vector2 normal, in LegEnv env) {
            leg.Planted = false;
            leg.Swinging = false;
            leg.DragHeat = MathHelper.Clamp(leg.DragHeat - 0.08f, 0f, 1f);
            int station = li / 2;
            float ph = SlotPhase01(li, env.GaitPhase) * MathHelper.TwoPi;
            float rotSign = (li & 1) == 0 ? -1f : 1f;
            float tilt = MathHelper.Clamp((MathF.Sin(ph) * 0.62f + travelDir * 0.12f) * rotSign, -EnvelopeSwing, EnvelopeSwing);
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
            float power = MathHelper.Clamp(env.HostVelocity.Length() / 10f, 0.4f, 1.3f);
            if (env.SandFx != null) {
                env.SandFx(leg.Foot, new Vector2(0f, -1.6f) * power, power);
                return;
            }
            if (!env.AllowDust || env.HostVelocity.Length() < 1.5f) {
                return;
            }
            for (int k = 0; k < 3; k++) {
                Dust d = Dust.NewDustPerfect(leg.Foot + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.8f, 2f) * power),
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
        /// 腿节+胫爪双骨余弦（有效跨距钳在 KneeSpan 窗内：永不锁直、永不折死）；
        /// 膝弯偏好 = 体后 → 天顶随走地权重连续过渡（高膝拱），分支迟滞防抖。
        /// 爪尖角按姿态平滑（落地咬地/摆越后拖/腾空内卷）。
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
            float slack = 12f * Scale;
            float stretch01 = MathHelper.Clamp(
                (dist - (FemurLen + TibiaLen - slack)) / (CoxaLen + slack), 0f, 1f);
            float swingMax = MathHelper.Lerp(CoxaSwingMax, MathHelper.Pi, stretch01);
            //失力腿基节松脱：向重力向垂
            float coxaAng = baseAng + MathHelper.Clamp(delta, -swingMax, swingMax);
            if (leg.Limp > 0.05f) {
                coxaAng = coxaAng.AngleLerp(MathHelper.PiOver2, leg.Limp * 0.6f);
            }
            Vector2 coxaTip = hip + coxaAng.ToRotationVector2() * CoxaLen;

            //腿节 + 胫爪双骨：有效跨距钳窗
            Vector2 e = leg.Foot - coxaTip;
            float span = FemurLen + TibiaLen;
            float eLen = MathHelper.Clamp(e.Length(), span * KneeSpanMin, span * KneeSpanMax);
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

            //爪尖角：落地顺胫爪续入地面咬沙、摆越沿行进后拖、腾空顺胫爪向内卷
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
                DrawLeg(sb, in leg, screenPos, tint, bob, Scale);
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
                DrawLeg(sb, in leg, Vector2.Zero, tintFor(li, leg.Groundness), 0f, Scale);
            }
        }

        /// <summary>腿骨贴图覆写（null = 荒花默认稿）：脓蕾图鉴托管本 rig 时换成自己的改色腿</summary>
        internal Asset<Texture2D> UpperTexOverride { get; set; }
        internal Asset<Texture2D> LowerTexOverride { get; set; }

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

        /// <summary>画一条腿的四段骨节（基节/腿节/胫爪/爪尖；髋端叠 bob 下沉，足端踩定；骨宽随倍率）</summary>
        private void DrawLeg(SpriteBatch sb, in Leg leg, Vector2 screenPos, Color tint, float bobPx, float scale) {
            Texture2D upperTex = (UpperTexOverride ?? BssHead.LegUpperAsset)?.Value;
            Texture2D lowerTex = (LowerTexOverride ?? BssHead.LegLowerAsset)?.Value;
            //爪尖微节共用胫爪稿（覆写时也跟着换）
            Texture2D clawTex = (LowerTexOverride ?? BssHead.LegClawAsset)?.Value;
            if (upperTex == null || lowerTex == null) {
                return;
            }
            Vector2 hip = leg.Hip + new Vector2(0f, bobPx);
            float thick = MathHelper.Lerp(0.9f, 1f, leg.Groundness) * scale;

            DrawBone(sb, upperTex, hip, leg.CoxaTip, 1.35f * thick, tint, screenPos);
            DrawBone(sb, upperTex, leg.CoxaTip, leg.Knee, 1.05f * thick, tint, screenPos);
            DrawBone(sb, lowerTex, leg.Knee, leg.DrawFoot, 0.9f * thick, tint, screenPos);
            if (clawTex != null) {
                Vector2 clawEnd = leg.DrawFoot + leg.ClawAng.ToRotationVector2() * (ClawLenBase * scale);
                DrawBone(sb, clawTex, leg.DrawFoot, clawEnd, 0.62f * thick, tint, screenPos);
            }
        }

        /// <summary>骨节拉伸绘制：贴图约定尖端朝上，底端锚在关节起点</summary>
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
