using CalamityOverhaul.Content.HackTimes.Scannables;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    //队列条目状态
    internal enum HackQueueState
    {
        Waiting,//等上传
        Uploading,//上传中
        Completed,//完成闪一下
    }

    //右侧面板 slot 查询
    internal enum QueueSlotState
    {
        None,
        Queued,
        Uploading,
        Completed,
    }

    /// <summary>队列单条，统一 IHackTarget</summary>
    internal class HackQueueEntry
    {
        public QuickHackDef Hack;
        //QuickHackDef.Instances 索引
        public int SlotIndex;
        public IHackTarget Target;
        public HackQueueState State;
        public float UploadProgress;//0~1
        public float FlyIn;//0~1 飞入
        public float CompletedTimer;//完成闪烁
        public float GlitchSeed;
        //入队时锁定的 RAM 成本
        public int ComputedRamCost;
        //请求幂等键
        public uint SessionId;
        public uint RequestId;
        //服务端激活号
        public long ActivationId;
        public bool AuthorityAccepted;

        public HackQueueEntry(QuickHackDef hack, int slotIndex, IHackTarget target, int computedRamCost) {
            Initialize(hack, slotIndex, target, computedRamCost, 0, 0);
        }

        public HackQueueEntry(QuickHackDef hack, int slotIndex, IHackTarget target,
            int computedRamCost, uint sessionId, uint requestId) {
            Initialize(hack, slotIndex, target, computedRamCost, sessionId, requestId);
        }

        private void Initialize(QuickHackDef hack, int slotIndex, IHackTarget target,
            int computedRamCost, uint sessionId, uint requestId) {
            Hack = hack;
            SlotIndex = slotIndex;
            Target = target;
            ComputedRamCost = Math.Clamp(computedRamCost, 0, 1024);
            SessionId = sessionId;
            RequestId = requestId;
            ActivationId = 0;
            AuthorityAccepted = sessionId == 0 || requestId == 0;
            State = HackQueueState.Waiting;
            UploadProgress = 0f;
            FlyIn = 0f;
            CompletedTimer = 0f;
            GlitchSeed = Main.rand?.Next(10000) / 100f ?? 0f;
        }

        public bool IsTargetValid => Target != null && Target.IsValid;

        public HackTargetKind TargetKind => Target?.TargetType?.Kind ?? HackTargetKind.None;

        //兼容旧 API

        /// <summary>NpcScannable 时 NPC 索引，否则 -1</summary>
        public int TargetIndex => Target is NpcScannable n ? n.NpcIndex : -1;
        /// <summary>TileScannable 时物块 X，否则 -1</summary>
        public int TileX => Target is TileScannable t ? t.TileCoordX : -1;
        /// <summary>TileScannable 时物块 Y，否则 -1</summary>
        public int TileY => Target is TileScannable t ? t.TileCoordY : -1;
        /// <summary>炮台引用，否则 null</summary>
        public IHackableTurret TurretTarget => Target as IHackableTurret;
        /// <summary>信号塔引用，否则 null</summary>
        public IHackableSignalTower SignalTowerTarget => Target as IHackableSignalTower;
    }
}
