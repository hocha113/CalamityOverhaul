using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.ADV.Scenarios.Helen;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Common
{
    internal class GiftScenarioNPC : DeathTrackingNPC, IWorldInfo
    {
        void IWorldInfo.OnWorldLoad() {
            foreach (var scenarios in GiftScenarioBase.BossIDToInds.Values) {
                foreach (var scenario in scenarios) {
                    GiftScenarioBase.SpawnedDic[scenario] = false;
                }
            }
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => GiftScenarioBase.SpawnedDic.Keys.Any(g => g.TargetBossID == entity.type);

        public override void OnNPCDeath(NPC npc) {
            if (!CWRRef.GetBossRushActive() // Boss Rush 不触发礼物场景
                && GiftScenarioBase.BossIDToInds.TryGetValue(npc.type, out var scenarios)) {
                foreach (var scenario in scenarios) {
                    if (scenario.CanSpawned()) {
                        GiftScenarioBase.SpawnedDic[scenario] = true;
                    }
                }
            }
        }
    }

    internal abstract class GiftScenarioBase : ADVScenarioBase, ILocalizedModType
    {
        /// <summary>
        /// 礼物场景实例的生成状态字典
        /// </summary>
        public readonly static Dictionary<GiftScenarioBase, bool> SpawnedDic = [];
        /// <summary>
        /// BossID 到礼物场景列表，多场景可共用一个 Boss
        /// </summary>
        public readonly static Dictionary<int, List<GiftScenarioBase>> BossIDToInds = [];
        /// <summary>
        /// 随机延迟计时器(单位:tick)
        /// </summary>
        private readonly static Dictionary<string, int> pendingTimers = [];
        /// <summary>
        /// 目标Boss的NPC ID
        /// </summary>
        public abstract int TargetBossID { get; }
        protected virtual bool BossDowned() => true;
        protected abstract bool IsGiftCompleted(ADVSave save);
        protected abstract void MarkGiftCompleted(ADVSave save);
        protected abstract bool StartScenarioInternal();

        /// <summary>
        /// 子类可重写以适配不同角色体系
        /// </summary>
        protected virtual bool CheckHolderCondition(ADVSave save, Player player) {
            var halibutPlayer = player.GetOverride<HalibutPlayer>();
            return halibutPlayer.HeldHalibut && save.Get<HalibutADVData>().FirstMet;
        }
        public override void VaultSetup() {
            LoadThis();
            base.VaultSetup();
        }
        public void LoadThis() {
            SpawnedDic[this] = false;
            if (TargetBossID > NPCID.None) {
                if (!BossIDToInds.TryGetValue(TargetBossID, out var list)) {
                    list = [];
                    BossIDToInds[TargetBossID] = list;
                }
                list.Add(this);
            }
        }
        public static void Clear() {
            SpawnedDic.Clear();
            BossIDToInds.Clear();
            pendingTimers.Clear();
        }

        /// <summary>
        /// 附加生成条件
        /// </summary>
        /// <param name="save"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        protected virtual bool AdditionalConditions(ADVSave save, Player player) {
            return true;
        }

        /// <summary>
        /// 是否允许生成该礼物场景
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSpawned() {
            return true;
        }

        public override void Update(ADVSave save, Player player) {
            if (!CheckHolderCondition(save, player)) {
                return;
            }
            if (IsGiftCompleted(save)) {
                return;
            }
            if (!BossDowned()) {
                return;
            }
            if (!AdditionalConditions(save, player)) {
                return;
            }
            if (!SpawnedDic[this]) {
                return; // Boss 未击败或场景未生成
            }

            // 避开 Boss 战与 Boss Rush
            if (CWRWorld.HasBoss || CWRWorld.BossRush) {
                return;
            }

            if (!pendingTimers.TryGetValue(Key, out int timer)) {
                timer = 60 * Main.rand.Next(2, 4);
                pendingTimers[Key] = timer;// 与 SupCalMoonLordReward 同策略，2~4 秒随机缓冲
                return; // 首次满足条件，进入延迟
            }

            if (timer > 0) {
                pendingTimers[Key] = timer - 1; // 延迟倒计时
                return;
            }

            if (StartScenarioInternal()) {
                MarkGiftCompleted(save);
                pendingTimers.Remove(Key); // 完成后移除
                SpawnedDic[this] = false; // 重置生成状态
            }
            else {
                pendingTimers[Key] = 30; // 场景占用未启动，30 帧后重试
            }
        }
    }
}
