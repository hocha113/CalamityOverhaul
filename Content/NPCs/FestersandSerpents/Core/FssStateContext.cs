using InnoVault.StateMachines;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Core
{
    /// <summary>头部运动声明模式</summary>
    internal enum FssMoveMode
    {
        /// <summary>不声明：速度自然衰减刹停（防状态收尾残留速度）</summary>
        Hold,
        /// <summary>贴地爬行：沿地形等高线推进</summary>
        Crawl,
        /// <summary>蠕虫寻的转向（钻沙/腾空段）</summary>
        Steer,
        /// <summary>状态自管速度</summary>
        Direct,
    }

    /// <summary>四足步态指令（纯表现，各端本地模拟）</summary>
    internal enum FssLegCommand
    {
        /// <summary>常态步行</summary>
        March,
        /// <summary>钻沙收拢贴体</summary>
        Tuck,
        /// <summary>立起姿态：前腿举离地面</summary>
        Raise,
        /// <summary>腾空划游</summary>
        Flail,
        /// <summary>死亡逐腿失力</summary>
        Collapse,
    }

    /// <summary>脓蕾沙蟒状态上下文</summary>
    internal class FssStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public FssHead Owner { get; set; }
        public bool MasterMode { get; set; }
        #endregion

        #region 出招编排
        /// <summary>轮换出招序号</summary>
        public int AttackIndex { get; set; }
        /// <summary>出招冷却（裁决只看权威端）</summary>
        public int AttackCooldown { get; set; }
        /// <summary>阶段：1=P1 灼金猎手 2=P2 变异蔓延 3=P3 满溢怒放；写 ai[2] 同步</summary>
        public int Phase { get => (int)Npc.ai[2]; set => Npc.ai[2] = value; }
        /// <summary>连击队列：收招后直接接的状态号（-1 无）</summary>
        public int QueuedChainState { get; set; } = -1;
        /// <summary>
        /// 骚扰滴射时钟（跨 hub 进出持久累积）。hub 每次重建 Timer 归零而 hub
        /// 存活常仅几帧，按状态内 Timer 取模的骚扰永远凑不满周期——时钟必须挂在
        /// 上下文上跨状态攒。各端同帧推进，弹幕仍只在权威端。
        /// </summary>
        public int HarassClock { get; set; }
        /// <summary>死亡演出已完，CheckDead 据此放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>转阶段后的弹速爬坡剩余帧（公平阀，各攻击态读取折算弹速）</summary>
        public int PostTransitionRamp { get; set; }

        /// <summary>弹速公平折算：转阶段后首招八成速起步，爬坡回满</summary>
        public float RampSpeedScale => PostTransitionRamp <= 0 ? 1f
            : MathHelper.Lerp(1f, 0.8f, PostTransitionRamp / (float)FssDirector.PostTransitionRampFrames);
        #endregion

        #region 运动声明（每帧重声明，未声明回落 Hold 刹停）
        public FssMoveMode Mode { get; set; }
        /// <summary>Steer 模式目标点</summary>
        public Vector2 MoveTarget { get; set; }
        public float MoveSpeed { get; set; }
        public float TurnSpeed { get; set; } = 1.6f;
        public float AccelRate { get; set; } = 0.08f;
        /// <summary>蛇形扰动强度</summary>
        public float Slither { get; set; }
        /// <summary>爬行朝向（±1）</summary>
        public float CrawlDirX { get; set; } = 1f;
        /// <summary>爬行速度</summary>
        public float CrawlSpeed { get; set; }
        /// <summary>蛇形相位（持久量）</summary>
        public float SlitherPhase { get; set; }
        /// <summary>头部瞄准覆盖（弧度；NaN=跟速度走）。行进间攻击用：身体在爬，头看目标</summary>
        public float AimAngle { get; set; } = float.NaN;
        #endregion

        #region 四足指令（每帧重声明）
        public FssLegCommand LegCommand { get; set; }
        /// <summary>立起高度 0..1</summary>
        public float FrontRaise { get; set; }
        /// <summary>已失力腿数（死亡演出推进）</summary>
        public int CollapsedLegs { get; set; }
        /// <summary>腿整体可见度（钻沙期渐隐）</summary>
        public float LegAlpha { get; set; } = 1f;
        #endregion

        #region 表现通道
        /// <summary>鞭链冲量：一记事件沿体节向尾传播的行波（各端本地演出）</summary>
        public float WhipAge { get; set; } = 999f;
        /// <summary>鞭波强度（像素振幅基数）</summary>
        public float WhipStrength { get; set; }

        /// <summary>触发一记鞭链行波</summary>
        public void PulseWhip(float strength) {
            WhipAge = 0f;
            WhipStrength = strength;
        }

        /// <summary>步态时钟（弧度，持久量）：腿的换步排程与爬行涌动共读此拍</summary>
        public float GaitPhase { get; set; }

        /// <summary>步态时钟推进速率（弧度/帧）：随体速加快</summary>
        public static float GaitIncrement(float speedX) => 0.045f + speedX * 0.009f;

        /// <summary>各髋站落步下沉 0..1（步态系统落足帧置位，客户端表现量）</summary>
        public float[] StationBob { get; } = new float[FssLegRig.LegCount];

        /// <summary>按链序采样落步下沉（距髋站 ±3 节内线性衰减）</summary>
        public float SampleStationBob(float ordinal) {
            float sum = 0f;
            for (int k = 0; k < StationBob.Length; k++) {
                if (StationBob[k] <= 0.02f) {
                    continue;
                }
                float weight = MathHelper.Clamp(1f - Math.Abs(ordinal - FssLegRig.StationOrdinals[k]) / 3f, 0f, 1f);
                sum += StationBob[k] * weight;
            }
            return Math.Min(sum, 1.25f);
        }

        /// <summary>囊肿节辉光 0..1（预告/怒放，金色）</summary>
        public float CystGlow { get; set; }
        /// <summary>全身抖动强度 0..1（绘制层读取，位置不动）</summary>
        public float ShakeStrength { get; set; }
        /// <summary>腐沙暴强度 0..1（滤镜+风沙共用）</summary>
        public float StormLevel { get; set; }
        /// <summary>链距压缩系数（盘拢/蓄势呼吸）</summary>
        public float Compression { get; set; } = 1f;
        /// <summary>脉冲通道：0无 1预告波(头→尾) 2蜕皮波 3死亡溃爆波(尾→头) 4满溢全闪</summary>
        public int PulseKind { get; set; }
        /// <summary>波前相位 0..1</summary>
        public float PulsePhase { get; set; }
        /// <summary>腐沙暴风向（确定性：头 whoAmI 奇偶）</summary>
        public int WindSign => Npc != null && Npc.whoAmI % 2 == 0 ? 1 : -1;

        /// <summary>
        /// 吞沙鼓包位置（链序空间；吞沙炮的活体预告：从尾侧向 0 递减，&lt;0 无鼓包）。
        /// 绘制层按 |ordinal-BulgeOrdinal| 衰减放大体节 = 鼓包沿身蠕动。
        /// </summary>
        public float BulgeOrdinal { get; set; } = -1f;
        /// <summary>鼓包强度 0..1（状态每帧重声明）</summary>
        public float BulgeStrength { get; set; }

        /// <summary>吞吸强度 0..1（吞沙段尘埃向口收束的表现量）</summary>
        public float SwallowSuction { get; set; }

        /// <summary>侵蚀度 0..1（死亡/蜕皮的体表溃烂推进，着色器 uErode）</summary>
        public float ErodeLevel { get; set; }

        /// <summary>
        /// 囊肿已爆度（按链序，1=刚爆完 0=充能满）。疮爆掠航置 1，
        /// 随时间线性充能回 0；绘制层按此瘪缩囊肿节并熄灭辉光（可读资源）。
        /// 各端从状态时序同算，不入网络包。
        /// </summary>
        public float[] CystSpent { get; } = new float[FssDirector.MaxOrdinals];

        /// <summary>门冲进行中（每帧重声明）：体节 alpha 走"前邻涟漪"渐显（同入场）</summary>
        public bool PortalPhase { get; set; }
        /// <summary>门冲吞入段（每帧重声明）：全链快速渐隐（门口吞没的读数）</summary>
        public bool PortalHiding { get; set; }

        /// <summary>
        /// 裂躯领节链序（-1 = 未分裂）。被标记的体节跳过跟链、由头部状态直驱其
        /// 转向物理，成为后半身的临时首领；断口两端体表挂满值裂隙渗光。
        /// 持久量（不逐帧衰减），裂躯态 OnExit 无条件清除 = 任何中断路径都必然重连链。
        /// </summary>
        public int SplitLeaderOrdinal { get; set; } = -1;
        /// <summary>裂躯领节旋转覆盖（弧度；NaN=跟速度走；每帧重声明）。蓄力后撤时领节仍面向冲线</summary>
        public float SplitLeaderAim { get; set; } = float.NaN;
        #endregion

        #region 体节缓存
        /// <summary>体节列表（含尾，按链序），头部周期刷新</summary>
        public List<NPC> Segments { get; } = new(FssDirector.BodyCount + FssDirector.GrowthSegments + 2);
        /// <summary>体节总数（体+尾，读 ai[1] 同步槽）</summary>
        public int TotalSegments { get; set; }

        /// <summary>囊肿节判定（款式2 帧，灵液发射器）</summary>
        public static bool IsCystOrdinal(int ordinal)
            => ordinal % FssDirector.CystStep == FssDirector.CystStep - 1;

        /// <summary>重扫体节链（按 ai[0] 链序排序）</summary>
        public void RefreshSegments() {
            Segments.Clear();
            if (Npc == null) {
                return;
            }
            int bodyType = ModContent.NPCType<FssBody>();
            int tailType = ModContent.NPCType<FssTail>();
            foreach (var n in Main.ActiveNPCs) {
                if ((n.type == bodyType || n.type == tailType) && (int)n.ai[3] == Npc.whoAmI) {
                    Segments.Add(n);
                }
            }
            Segments.Sort((a, b) => ((int)a.ai[0]).CompareTo((int)b.ai[0]));
        }
        #endregion

        /// <summary>每帧默认值：运动回 Hold、腿回步行、脉冲清零、表现量自然衰减</summary>
        public void BeginFrameDefaults() {
            Mode = FssMoveMode.Hold;
            MoveSpeed = 0f;
            Slither = 0f;
            AimAngle = float.NaN;
            LegCommand = FssLegCommand.March;
            PulseKind = 0;
            PulsePhase = 0f;
            PortalPhase = false;
            PortalHiding = false;
            SplitLeaderAim = float.NaN;
            BulgeStrength *= 0.85f;
            if (BulgeStrength < 0.02f) {
                BulgeStrength = 0f;
                BulgeOrdinal = -1f;
            }
            SwallowSuction *= 0.88f;
            if (SwallowSuction < 0.02f) {
                SwallowSuction = 0f;
            }

            WhipAge = Math.Min(WhipAge + 1f, 999f);
            for (int k = 0; k < StationBob.Length; k++) {
                StationBob[k] *= 0.85f;
                if (StationBob[k] < 0.02f) {
                    StationBob[k] = 0f;
                }
            }
            //囊肿匀速充能（各端同算的确定性衰减）
            float regen = 1f / FssDirector.CystRechargeFrames;
            for (int k = 0; k < CystSpent.Length; k++) {
                if (CystSpent[k] > 0f) {
                    CystSpent[k] = Math.Max(0f, CystSpent[k] - regen);
                }
            }

            if (PostTransitionRamp > 0) {
                PostTransitionRamp--;
            }

            FrontRaise *= 0.9f;
            if (FrontRaise < 0.02f) {
                FrontRaise = 0f;
            }
            ShakeStrength *= 0.82f;
            if (ShakeStrength < 0.02f) {
                ShakeStrength = 0f;
            }
            CystGlow *= 0.9f;
            if (CystGlow < 0.02f) {
                CystGlow = 0f;
            }
            ErodeLevel *= 0.985f;
            if (ErodeLevel < 0.02f) {
                ErodeLevel = 0f;
            }
            Compression = MathHelper.Lerp(Compression, 1f, 0.08f);
            LegAlpha = MathHelper.Clamp(LegAlpha + 0.04f, 0f, 1f);
        }
    }
}
