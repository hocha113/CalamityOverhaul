using InnoVault.StateMachines;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core
{
    /// <summary>头部运动声明模式</summary>
    internal enum BssMoveMode
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
    internal enum BssLegCommand
    {
        /// <summary>常态步行：足端钉世界落点、超步幅/超伸展换步（够不着地自动腾空卷曲）</summary>
        March,
        /// <summary>钻沙收拢贴体</summary>
        Tuck,
        /// <summary>立起姿态：前腿螳螂式收折举离地面</summary>
        Raise,
        /// <summary>强制腾空卷曲（抓挠空气，无地面交互）</summary>
        Flail,
        /// <summary>死亡逐腿失力（配合 CollapsedLegs 计数）</summary>
        Collapse,
        /// <summary>柱面抓握：足端锚沙柱壁面攀爬（几何由 LegGrip* 声明）</summary>
        Grip,
        /// <summary>蓄势蹲伏：站距外扩贴地咬定、快步稳桩（起跳前的压缩拍）</summary>
        Brace,
    }

    /// <summary>荒花沙蟒状态上下文</summary>
    internal class BssStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public BssHead Owner { get; set; }
        public bool MasterMode { get; set; }
        #endregion

        #region 出招编排
        /// <summary>轮换出招序号</summary>
        public int AttackIndex { get; set; }
        /// <summary>出招冷却（裁决只看权威端）</summary>
        public int AttackCooldown { get; set; }
        /// <summary>阶段：1=P1 巡曳 2=P2 沙暴 3=P3 繁花；写 ai[2] 同步</summary>
        public int Phase { get => (int)Npc.ai[2]; set => Npc.ai[2] = value; }
        /// <summary>连击队列：收招后直接接的状态号（-1 无）</summary>
        public int QueuedChainState { get; set; } = -1;
        /// <summary>追击阀已用闸（连接件不许连发；轮换出招时清零，权威端裁决量）</summary>
        public bool ChaseValveUsed { get; set; }
        /// <summary>死亡演出已完，CheckDead 据此放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 运动声明（每帧重声明，未声明回落 Hold 刹停）
        public BssMoveMode Mode { get; set; }
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
        public BssLegCommand LegCommand { get; set; }
        /// <summary>立起高度 0..1（Raise 姿态的前腿抬升与身体前倾读数）</summary>
        public float FrontRaise { get; set; }
        /// <summary>已失力腿数（死亡演出推进）</summary>
        public int CollapsedLegs { get; set; }
        /// <summary>腿整体可见度（钻沙期渐隐）</summary>
        public float LegAlpha { get; set; } = 1f;

        /// <summary>柱面抓握声明（Grip 指令的几何，每帧重声明；盘柱状态喂值）</summary>
        public bool LegGripActive { get; set; }
        public float LegGripCenterX { get; set; }
        public float LegGripHalfWidth { get; set; }
        public float LegGripTopY { get; set; }
        public float LegGripBottomY { get; set; }
        #endregion

        #region 表现通道
        /// <summary>
        /// 鞭链冲量：一记事件（冲刺/破土/急转）沿体节向尾传播的行波。
        /// 各端本地演出：状态在事件帧调 <see cref="PulseWhip"/>，体节按链序延迟读波做绘制偏移。
        /// </summary>
        public float WhipAge { get; set; } = 999f;
        /// <summary>鞭波强度（像素振幅基数）</summary>
        public float WhipStrength { get; set; }

        /// <summary>触发一记鞭链行波</summary>
        public void PulseWhip(float strength) {
            WhipAge = 0f;
            WhipStrength = strength;
        }

        /// <summary>
        /// 纵向肌肉波（真实节距，非绘制偏移）：出手帧释放波头→尾付出蓄势长度，
        /// 急刹追压波身体向头压缩。种类常量见 <see cref="NPCs.SerpentChainMath"/>。
        /// 各端本地演算 + 周期纠偏，与 Compression 同风险级，不新增网络包。
        /// </summary>
        public int GapWaveKind { get; set; }
        /// <summary>肌肉波龄（帧）</summary>
        public float GapWaveAge { get; set; } = 999f;
        /// <summary>肌肉波振幅（节距比例）</summary>
        public float GapWaveAmp { get; set; }

        /// <summary>触发一记纵向肌肉波</summary>
        public void PulseGapWave(int kind, float amp) {
            GapWaveKind = kind;
            GapWaveAge = 0f;
            GapWaveAmp = amp;
        }

        /// <summary>蓄力聚拢 0..1（每帧重声明）：颈段节距收紧、身体向头收拢上膛</summary>
        public float GatherLevel { get; set; }

        /// <summary>头部本帧速度模长（头 AI 每帧喂值，体节跟链读它算高速拉伸）</summary>
        public float HeadSpeed { get; set; }

        /// <summary>
        /// 步态时钟（弧度，持久量）：随体速推进，腿的换步排程与爬行的涌动/贴地呼吸共读此拍，
        /// 腿和身体因此咬合在同一节奏上。各端同算，联机风险面同 <see cref="SlitherPhase"/>。
        /// </summary>
        public float GaitPhase { get; set; }

        /// <summary>
        /// 步态时钟推进速率（弧度/帧）：随体速加快。划桨式步态（镜像坟灾虫臂）端点不钉世界，
        /// 周期可以放到从容档——巡曳速约 32 帧一个完整划水循环。
        /// </summary>
        public static float GaitIncrement(float speedX) => 0.045f + speedX * 0.009f;

        /// <summary>
        /// 各髋站落步下沉 0..1（步态系统落足帧置位，客户端表现量）。
        /// 体节/头部绘制按链序采样局部下沉，腿绘制层把同量叠到髋位做支撑腿压缩。
        /// </summary>
        public float[] StationBob { get; } = new float[BssLegRig.LegCount];

        /// <summary>按链序采样落步下沉（距髋站 ±3 节内线性衰减，重量波沿身传播的读数来源）</summary>
        public float SampleStationBob(float ordinal) {
            float sum = 0f;
            for (int k = 0; k < StationBob.Length; k++) {
                if (StationBob[k] <= 0.02f) {
                    continue;
                }
                float weight = MathHelper.Clamp(1f - Math.Abs(ordinal - BssLegRig.StationOrdinals[k]) / 3f, 0f, 1f);
                sum += StationBob[k] * weight;
            }
            return Math.Min(sum, 1.25f);
        }

        /// <summary>怒吼声波环龄（帧；&lt;0 = 无。爆震怒吼帧点火，头部绘制层消费）</summary>
        public float RoarRingAge { get; set; } = -1f;
        /// <summary>声波环心（点火帧锁定，不随头移动）</summary>
        public Vector2 RoarRingCenter { get; set; }

        /// <summary>点一记怒吼声波环（各端本地演出）</summary>
        public void FireRoarRing(Vector2 center) {
            RoarRingAge = 0f;
            RoarRingCenter = center;
        }

        /// <summary>红花节辉光 0..1（涟漪预告/怒放）</summary>
        public float BloomGlow { get; set; }
        /// <summary>全身抖动强度 0..1（体节绘制层读取，位置不动）</summary>
        public float ShakeStrength { get; set; }
        /// <summary>沙尘暴强度 0..1（滤镜+风沙+花瓣风偏共用）</summary>
        public float StormLevel { get; set; }
        /// <summary>链距压缩系数（盘拢/蓄势呼吸）</summary>
        public float Compression { get; set; } = 1f;
        /// <summary>脉冲通道：0无 1预告波(头→尾) 2发射波 3死亡溃爆波(尾→头) 4怒放全闪</summary>
        public int PulseKind { get; set; }
        /// <summary>波前相位 0..1</summary>
        public float PulsePhase { get; set; }
        /// <summary>沙暴风向（确定性：头 whoAmI 奇偶）</summary>
        public int WindSign => Npc != null && Npc.whoAmI % 2 == 0 ? 1 : -1;
        #endregion

        #region 体节缓存
        /// <summary>体节列表（含尾，按链序），头部周期刷新</summary>
        public List<NPC> Segments { get; } = new(BssDirector.BodyCount + 2);
        /// <summary>体节总数（体+尾，读 ai[1] 同步槽）</summary>
        public int TotalSegments { get; set; }

        /// <summary>红花节判定（款式2，钉刺/花瓣发射器）</summary>
        public static bool IsFlowerOrdinal(int ordinal)
            => ordinal % BssDirector.FlowerStep == BssDirector.FlowerStep - 1;

        /// <summary>重扫体节链（按 ai[0] 链序排序）</summary>
        public void RefreshSegments() {
            Segments.Clear();
            if (Npc == null) {
                return;
            }
            int bodyType = ModContent.NPCType<BssBody>();
            int tailType = ModContent.NPCType<BssTail>();
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
            Mode = BssMoveMode.Hold;
            MoveSpeed = 0f;
            Slither = 0f;
            AimAngle = float.NaN;
            LegCommand = BssLegCommand.March;
            LegGripActive = false;
            PulseKind = 0;
            PulsePhase = 0f;

            WhipAge = Math.Min(WhipAge + 1f, 999f);
            GapWaveAge = Math.Min(GapWaveAge + 1f, 999f);
            if (RoarRingAge >= 0f && ++RoarRingAge > 46f) {
                RoarRingAge = -1f;
            }
            GatherLevel = 0f;
            for (int k = 0; k < StationBob.Length; k++) {
                StationBob[k] *= 0.85f;
                if (StationBob[k] < 0.02f) {
                    StationBob[k] = 0f;
                }
            }

            FrontRaise *= 0.9f;
            if (FrontRaise < 0.02f) {
                FrontRaise = 0f;
            }
            ShakeStrength *= 0.82f;
            if (ShakeStrength < 0.02f) {
                ShakeStrength = 0f;
            }
            BloomGlow *= 0.9f;
            if (BloomGlow < 0.02f) {
                BloomGlow = 0f;
            }
            Compression = MathHelper.Lerp(Compression, 1f, 0.08f);
            LegAlpha = MathHelper.Clamp(LegAlpha + 0.04f, 0f, 1f);
        }
    }
}
