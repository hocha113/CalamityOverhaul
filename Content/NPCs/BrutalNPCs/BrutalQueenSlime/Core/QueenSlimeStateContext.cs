using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core
{
    /// <summary>随从角色标记，写入随从 npc.ai[0]</summary>
    internal static class QueenMinionRole
    {
        /// <summary>0=非本系统随从(回落原版AI)</summary>
        public const int None = 0;
        /// <summary>棱晶节点(蓝)，光束折射锚点，可破坏</summary>
        public const int PrismNode = 1;
        /// <summary>凝胶伴舞(粉)，绕后编队+迫击炮</summary>
        public const int GelDancer = 2;
        /// <summary>翼卫(紫)，二阶段镜像护航</summary>
        public const int WingedEscort = 3;
    }

    /// <summary>状态机共享上下文</summary>
    internal class QueenSlimeStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 战斗状态
        /// <summary>二阶段(展翅)，血量≤50%后为真</summary>
        public bool IsPhase2 { get; set; }
        /// <summary>阶段转换演出已完成</summary>
        public bool Phase2Unfolded { get; set; }
        /// <summary>低血大招已释放</summary>
        public bool UltFired { get; set; }
        public bool IsAsuraMode { get; set; }
        /// <summary>出招环索引</summary>
        public int AttackPhaseIndex { get; set; }
        /// <summary>死亡演出完，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>投技冷却(服务端计数)，二阶段展开后才开始走表；初值即首抓延迟</summary>
        public int GrabCooldown { get; set; } = 900;
        /// <summary>分裂召唤冷却(服务端计数)，防止仆从被清后立刻重召</summary>
        public int SummonCooldown { get; set; } = 240;
        #endregion

        #region 蓄力/视觉数据(每帧本地驱动)
        /// <summary>蓄力进度 0~1，驱动王冠辉光与身体虹彩</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无 1棱镜光束 2陨雨 3大招</summary>
        public int ChargeType { get; set; }

        /// <summary>形变脉冲，-1压扁~+1拉伸，主控每帧衰减</summary>
        public float SquashPulse { get; set; }
        /// <summary>翼展开度 0~1，转换演出推高，二阶段常驻1</summary>
        public float WingSpread { get; set; }
        /// <summary>翼拍速度倍率，冲刺/升空时推高</summary>
        public float WingFlapBoost { get; set; }
        /// <summary>残影强度 0~1，冲刺推高主控衰减</summary>
        public float AfterimageBoost { get; set; }
        /// <summary>身体虹彩泛光 0~1(折射蓄能视觉)</summary>
        public float PrismShimmer { get; set; }
        #endregion

        #region 动画数据(客户端本地)
        /// <summary>逻辑帧索引 0~23，语义沿用原版帧表</summary>
        public int BodyFrame { get; set; }
        public int BodyFrameCounter { get; set; }
        /// <summary>翼帧计数，0~23循环，/6取帧</summary>
        public int WingFrameCounter { get; set; }
        /// <summary>姿态指令 0自动 1强制起跳姿态 2强制下落 3强制落地蹲姿 4喷吐姿态 5飞行巡航</summary>
        public int PoseCommand { get; set; }
        #endregion

        public void ResetChargeState() {
            IsCharging = false;
            ChargeProgress = 0f;
            ChargeType = 0;
        }

        public void SetChargeState(int type, float progress) {
            IsCharging = true;
            ChargeType = type;
            ChargeProgress = progress;
        }

        /// <summary>推高形变脉冲(绝对值更大者胜)</summary>
        public void PushSquash(float value) {
            if (System.Math.Abs(value) > System.Math.Abs(SquashPulse)) {
                SquashPulse = value;
            }
        }

        #region 随从查询

        /// <summary>某 NPC 是否本皇后麾下指定角色</summary>
        public bool IsMyMinion(NPC n, int role) {
            return n.active && (int)n.ai[0] == role && (int)n.ai[2] == Npc.whoAmI
                && (n.type == NPCID.QueenSlimeMinionBlue || n.type == NPCID.QueenSlimeMinionPink || n.type == NPCID.QueenSlimeMinionPurple);
        }

        /// <summary>收集在场棱晶节点，按 ai[1] 槽位排序</summary>
        public List<NPC> CollectPrismNodes() {
            List<NPC> nodes = [];
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.QueenSlimeMinionBlue && IsMyMinion(n, QueenMinionRole.PrismNode)) {
                    nodes.Add(n);
                }
            }
            nodes.Sort((a, b) => ((int)a.ai[1]).CompareTo((int)b.ai[1]));
            return nodes;
        }

        /// <summary>统计指定角色随从数</summary>
        public int CountMinions(int role) {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (IsMyMinion(n, role)) {
                    count++;
                }
            }
            return count;
        }

        #endregion
    }
}
