using CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith;
using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    //队列条目状态
    internal enum HackQueueState
    {
        //等待上传
        Waiting,
        //正在上传
        Uploading,
        //上传完成，短暂闪烁
        Completed,
    }

    //用于在右侧面板查询某个 hack 在队列中的状态
    internal enum QueueSlotState
    {
        None,
        Queued,
        Uploading,
        Completed,
    }

    /// <summary>队列单条记录，统一 IHackTarget</summary>
    internal class HackQueueEntry
    {
        //对应的协议定义
        public QuickHackDef Hack;
        //在 QuickHackDef.Instances 中的索引
        public int SlotIndex;
        //目标引用
        public IHackTarget Target;
        //当前队列状态
        public HackQueueState State;
        //上传进度 0~1
        public float UploadProgress;
        //飞入动画进度 0~1
        public float FlyIn;
        //完成后闪烁计时
        public float CompletedTimer;
        //故障种子
        public float GlitchSeed;
        //RAM 成本，入队时由 HackCostEvaluator 固定
        public int ComputedRamCost;

        public HackQueueEntry(QuickHackDef hack, int slotIndex, IHackTarget target, int computedRamCost) {
            Hack = hack;
            SlotIndex = slotIndex;
            Target = target;
            ComputedRamCost = computedRamCost;
            State = HackQueueState.Waiting;
            UploadProgress = 0f;
            FlyIn = 0f;
            CompletedTimer = 0f;
            GlitchSeed = Main.rand?.Next(10000) / 100f ?? 0f;
        }

        //目标是否仍然有效
        public bool IsTargetValid => Target != null && Target.IsValid;

        //目标种类（旁路便捷查询）
        public HackTargetKind TargetKind => Target?.TargetType?.Kind ?? HackTargetKind.None;

        //兼容旧 API

        /// <summary>当 Target 为 NpcScannable 时返回 NPC 索引，否则返回 -1</summary>
        public int TargetIndex => Target is NpcScannable n ? n.NpcIndex : -1;
        /// <summary>当 Target 为 TileScannable 时返回物块 X，否则返回 -1</summary>
        public int TileX => Target is TileScannable t ? t.TileCoordX : -1;
        /// <summary>当 Target 为 TileScannable 时返回物块 Y，否则返回 -1</summary>
        public int TileY => Target is TileScannable t ? t.TileCoordY : -1;
        /// <summary>当 Target 为灵异 Actor 时返回引用</summary>
        public GlitchWraithActor WraithTarget => Target as GlitchWraithActor;
        /// <summary>当 Target 为炮台时返回引用</summary>
        public IHackableTurret TurretTarget => Target as IHackableTurret;
        /// <summary>当 Target 为信号塔时返回引用</summary>
        public IHackableSignalTower SignalTowerTarget => Target as IHackableSignalTower;
    }
}
