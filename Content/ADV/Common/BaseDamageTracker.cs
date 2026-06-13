using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Common.BaseDamageTracker;
using static CalamityOverhaul.Content.ADV.Common.DamageTrackerSystem;

namespace CalamityOverhaul.Content.ADV.Common
{
    internal class DamageTrackerSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI.QuestTracker";

        // 本地化文本
        public static LocalizedText QuestFailedPrefix { get; private set; }
        public static LocalizedText QuestCompletedPrefix { get; private set; }
        public static LocalizedText FailureReasonWrongWeapon { get; private set; }
        public static LocalizedText FailureReasonInsufficientDamage { get; private set; }
        public static LocalizedText SuccessDamageContribution { get; private set; }

        public override void SetStaticDefaults() {
            QuestFailedPrefix = this.GetLocalization(nameof(QuestFailedPrefix), () => "任务失败");
            QuestCompletedPrefix = this.GetLocalization(nameof(QuestCompletedPrefix), () => "任务完成!");
            FailureReasonWrongWeapon = this.GetLocalization(nameof(FailureReasonWrongWeapon), () => "未使用指定武器完成最后一击");
            FailureReasonInsufficientDamage = this.GetLocalization(nameof(FailureReasonInsufficientDamage), () => "武器伤害占比不足");
            SuccessDamageContribution = this.GetLocalization(nameof(SuccessDamageContribution), () => "伤害占比");
        }

        internal static void DealtReset() {
            // Boss 已消失，重置追踪
            TargetWeaponDamageDealt = 0f;
            TotalBossDamage = 0f;
            IsBossFightActive = false;
            CurrentDamageTrackerInstance = null;
        }

        public override void PostUpdateNPCs() {
            if (!IsBossFightActive) { // 无 Boss 战，重置追踪
                DealtReset();
                return;
            }

            if (CurrentDamageTrackerInstance != null
                && CurrentDamageTrackerInstance.NPC.Alives()
                && NPC.AnyNPCs(CurrentDamageTrackerInstance.NPC.type)) {
                return; // Boss 仍在场，继续追踪
            }

            DealtReset();
        }
    }

    /// <summary>
    /// 指定武器对 Boss 的伤害占比追踪基类
    /// </summary>
    internal abstract class BaseDamageTracker : DeathTrackingNPC, IWorldInfo
    {
        // 伤害追踪数据
        internal static float TargetWeaponDamageDealt = 0f;
        internal static float TotalBossDamage = 0f;
        internal static bool IsBossFightActive = false;
        /// <summary>
        /// 当前正在处理的伤害追踪实例
        /// </summary>
        internal static BaseDamageTracker CurrentDamageTrackerInstance { get; set; }

        // 子类配置
        internal abstract int TargetNPCType { get; }
        internal virtual HashSet<int> OtherNPCType => [];

        internal abstract int[] TargetWeaponTypes { get; }
        internal abstract int[] TargetProjectileTypes { get; }
        internal abstract float RequiredContribution { get; }

        /// <summary>
        /// 被追踪 NPC，更新周期内可能为 null
        /// </summary>
        internal NPC NPC { get; private set; }

        public override bool InstancePerEntity => true; // 每 NPC 实例一份

        internal bool IsTargetByID(NPC npc) => npc.type == TargetNPCType || OtherNPCType.Contains(npc.type);
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => IsTargetByID(entity);

        void IWorldInfo.OnWorldLoad() {
            ResetDamageTracking(); // 进世界重置
        }

        /// <summary>
        /// 任务是否激活
        /// </summary>
        public abstract bool IsQuestActive(Player player);

        protected virtual void ResetDamageTracking() {
            TargetWeaponDamageDealt = 0f;
            TotalBossDamage = 0f;
            IsBossFightActive = false;
            CurrentDamageTrackerInstance = null;
        }

        public override bool PreAI(NPC npc) {
            if (npc.type != TargetNPCType) {
                return true;
            }

            // Boss 在场则激活战斗追踪
            IsBossFightActive = npc.active;
            // 记录 Boss 最大生命
            TotalBossDamage = npc.lifeMax;

            if (npc.Alives()) {
                foreach (var n in npc.EntityGlobals) {
                    if (n is BaseDamageTracker tracker) {
                        CurrentDamageTrackerInstance = tracker;
                        CurrentDamageTrackerInstance.NPC = npc;
                        break;
                    }
                }
            }
            return true;
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (!IsTargetByID(npc)) {
                return;
            }

            // 任务未激活则跳过
            if (!IsQuestActive(player)) {
                return;
            }

            // 统计目标武器伤害
            if (IsTargetWeapon(item.type)) {
                TargetWeaponDamageDealt += hit.Damage;
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (!IsTargetByID(npc)) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (projectile.owner.TryGetPlayer(out Player owner)) {
                player = owner;
            }

            // 任务未激活则跳过
            if (!IsQuestActive(player)) {
                return;
            }

            // 目标武器弹幕
            if (IsTargetProjectile(projectile)) {
                TargetWeaponDamageDealt += hit.Damage;
                return;
            }

            // ItemUse 来源弹幕
            if (projectile.Alives() && projectile.CWR().Source is EntitySource_ItemUse itemSource && IsTargetWeapon(itemSource.Item.type)) {
                TargetWeaponDamageDealt += hit.Damage;
            }
        }

        public sealed override void OnNPCDeath(NPC npc) {
            if (IsTargetByID(npc)) {
                // DeathTrackingNPC.OnKill 在客户端调用，此处安全
                Check(npc);
            }
        }

        internal void Check(NPC npc) {
            if (npc.type != TargetNPCType) {
                return;
            }

            // 任务未激活则跳过
            if (!IsQuestActive(Main.LocalPlayer)) {
                return;
            }

            CheckQuestCompletion();

            // 重置追踪
            ResetDamageTracking();
        }

        /// <summary>
        /// 检查任务是否完成
        /// </summary>
        protected virtual void CheckQuestCompletion() {
            Player player = Main.LocalPlayer;

            // 伤害占比判定
            float contribution = TotalBossDamage > 0 ? TargetWeaponDamageDealt / TotalBossDamage : 0f;
            int contributionPct = (int)Math.Round(contribution * 100);
            int requiredPct = (int)Math.Round(RequiredContribution * 100);
            if (contributionPct < requiredPct) {
                ShowFailureMessage(player, $"{FailureReasonInsufficientDamage.Value} ({contribution:P0}/{RequiredContribution:P0})");
                return;
            }

            //任务完成
            OnQuestCompleted(player, contribution);
            ShowSuccessMessage(player, contribution);
        }

        /// <summary>
        /// 任务完成时回调
        /// </summary>
        public abstract void OnQuestCompleted(Player player, float contribution);

        /// <summary>
        /// 显示任务失败消息
        /// </summary>
        public virtual void ShowFailureMessage(Player player, string reason) {
            int combat = CombatText.NewText(player.Hitbox, Color.Red, $"{QuestFailedPrefix.Value}: {reason}", true);
            Main.combatText[combat].lifeTime = 300; // 延长显示
            VaultUtils.Text($"{QuestFailedPrefix.Value}: {reason}", Color.Red);
        }

        /// <summary>
        /// 显示任务成功消息
        /// </summary>
        public virtual void ShowSuccessMessage(Player player, float contribution) {
            int combat = CombatText.NewText(player.Hitbox, Color.Gold, $"{QuestCompletedPrefix.Value} {SuccessDamageContribution.Value}: {contribution:P0}", true);
            Main.combatText[combat].lifeTime = 300; // 延长显示
            VaultUtils.Text($"{QuestCompletedPrefix.Value} {SuccessDamageContribution.Value}: {contribution:P0}", Color.Gold);
        }

        /// <summary>
        /// 检查物品是否是目标武器
        /// </summary>
        protected virtual bool IsTargetWeapon(int itemType) {
            foreach (int weaponType in TargetWeaponTypes) {
                if (itemType == weaponType) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查弹幕是否来自目标武器
        /// </summary>
        protected virtual bool IsTargetProjectile(Projectile projectile) {
            foreach (int projType in TargetProjectileTypes) {
                if (projectile.type == projType) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 供 UI 读取的追踪数据
        /// </summary>
        public static (float targetWeaponDamage, float totalDamage, bool isActive) GetDamageTrackingData() {
            return (TargetWeaponDamageDealt, TotalBossDamage, IsBossFightActive);
        }
    }
}
