using CalamityOverhaul.Content.ADV.Scenarios;
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
            foreach (var giftScenario in GiftScenarioBase.BossIDToInds.Values) {
                GiftScenarioBase.SpawnedDic[giftScenario] = false;
            }
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => GiftScenarioBase.SpawnedDic.Keys.Any(g => g.TargetBossID == entity.type);

        public override void OnKill(NPC npc) {
            if (!CWRRef.GetBossRushActive()//Boss Rush时不触发礼物场景
                && GiftScenarioBase.BossIDToInds.TryGetValue(npc.type, out var giftScenarioBase)
                && giftScenarioBase.CanSpawned()) {
                GiftScenarioBase.SpawnedDic[giftScenarioBase] = true;
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
        /// BossID到礼物场景实例的映射
        /// </summary>
        public readonly static Dictionary<int, GiftScenarioBase> BossIDToInds = [];
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
        public override void VaultSetup() {
            LoadThis();
            base.VaultSetup();
        }
        public void LoadThis() {
            SpawnedDic[this] = false;
            if (TargetBossID > NPCID.None) {
                BossIDToInds[TargetBossID] = this;
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
        /// <param name="halibutPlayer"></param>
        /// <returns></returns>
        protected virtual bool AdditionalConditions(ADVSave save, HalibutPlayer halibutPlayer) {
            return true;
        }

        /// <summary>
        /// 是否允许生成该礼物场景
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSpawned() {
            return true;
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!halibutPlayer.HeldHalibut) {
                return;
            }
            if (!save.FirstMet) {
                return;//必须先触发过初次见面
            }
            if (IsGiftCompleted(save)) {
                return;
            }
            if (!BossDowned()) {
                return;
            }
            if (!AdditionalConditions(save, halibutPlayer)) {
                return;
            }
            if (!SpawnedDic[this]) {
                return;//Boss未被击败或礼物场景未生成
            }

            //避免在不合适的时候触发
            if (CWRWorld.HasBoss || CWRWorld.BossRush) {
                return;
            }

            if (!pendingTimers.TryGetValue(Key, out int timer)) {
                timer = 60 * Main.rand.Next(2, 4);
                pendingTimers[Key] = timer;//与SupCalMoonLordReward保持一致的随机缓冲时间
                return;//首次满足条件进入延迟阶段
            }

            if (timer > 0) {
                pendingTimers[Key] = timer - 1;//倒计时进行中
                return;
            }

            if (StartScenarioInternal()) {
                MarkGiftCompleted(save);
                pendingTimers.Remove(Key);//完成后移除
                SpawnedDic[this] = false;//重置生成状态
            }
            else {
                pendingTimers[Key] = 30;//若因其它场景占用而未成功启动则稍后重试
            }
        }
    }
}
