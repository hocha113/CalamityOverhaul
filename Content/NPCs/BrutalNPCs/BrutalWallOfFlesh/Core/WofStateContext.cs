using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core
{
    /// <summary>状态上下文</summary>
    internal class WofStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 战斗状态
        /// <summary>战斗阶段 1/2/3，写入 npc.ai[1] 同步(大招/转阶段的一次性门也由它承担)</summary>
        public int Phase { get; set; } = 1;
        public bool IsDeathMode { get; set; }
        public bool MasterMode { get; set; }
        /// <summary>死亡演出完成，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>目标长期脱屏激怒(反风筝阀)</summary>
        public bool FarEnraged { get; set; }
        /// <summary>脱屏累计帧(主控维护)</summary>
        public int FarTimer { get; set; }
        #endregion

        #region 运动参数（状态每帧声明，主控 UpdateAdvance 消费）
        /// <summary>推进速度系数，1=常规曲线</summary>
        public float AdvanceFactor { get; set; } = 1f;
        /// <summary>绝对速度覆盖，&lt;0 表示不启用</summary>
        public float SpeedOverride { get; set; } = -1f;
        /// <summary>本帧禁用原版Y锚定(Intro/Death 演出直控)</summary>
        public bool SuppressYAnchor { get; set; }
        #endregion

        #region 演出数据（每帧声明）
        /// <summary>嘴部指令 0自动咀嚼 1大张 2紧咬</summary>
        public int MouthCommand { get; set; }
        /// <summary>蓄力类型 0无 1突进 2漩涡 3大迁徙 4扫描 5舌鞭</summary>
        public int ChargeType { get; set; }
        /// <summary>蓄力进度 0~1</summary>
        public float ChargeProgress { get; set; }
        /// <summary>全墙潮红强度 0~1(推进死线的心跳)</summary>
        public float WallFlush { get; set; }
        /// <summary>后方血幕当前X(仅大迁徙期有效，其余为0)</summary>
        public float RearCurtainX { get; set; }
        /// <summary>血幕不透明度 0~1</summary>
        public float RearCurtainOpacity { get; set; }
        #endregion

        #region 选招（仅服务端消费）
        /// <summary>洗牌袋</summary>
        public List<WofStateIndex> AttackBag { get; } = [];
        /// <summary>上一招，防复读</summary>
        public WofStateIndex LastAttack { get; set; } = WofStateIndex.Advance;
        /// <summary>投技冷却(主控每帧递减，两条触发路径共享)</summary>
        public int GrabCooldown { get; set; }
        /// <summary>绕后惩罚预定的受害者whoAmI，-1无(仅入场帧消费)</summary>
        public int PendingGrabVictim { get; set; } = -1;
        #endregion

        public void ResetChargeState() {
            ChargeType = 0;
            ChargeProgress = 0f;
        }

        public void SetChargeState(int type, float progress) {
            ChargeType = type;
            ChargeProgress = MathHelper.Clamp(progress, 0f, 1f);
        }

        /// <summary>收集活跃饥饿者，按 whoAmI 升序(各端一致)</summary>
        public List<NPC> CollectHungries() {
            List<NPC> list = [];
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == Terraria.ID.NPCID.TheHungry) {
                    list.Add(n);
                }
            }
            list.Sort((a, b) => a.whoAmI.CompareTo(b.whoAmI));
            return list;
        }

        /// <summary>统计活跃水蛭头数</summary>
        public int CountLeeches() {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == Terraria.ID.NPCID.LeechHead) {
                    count++;
                }
            }
            return count;
        }
    }
}
