using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Scenarios.Common.BaseDamageTracker;
using static CalamityOverhaul.Content.ADV.Scenarios.Common.DamageTrackerSystem;

namespace CalamityOverhaul.Content.ADV.Scenarios.Common
{
    internal class DamageTrackerSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI.QuestTracker";

        //本地化文本
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

        public override void PostUpdateNPCs() {
            if (!IsBossFightActive) {
                return;//没有激活的Boss战斗，直接返回
            }
            if (NPC.AnyNPCs(HuntingNPCID)) {
                return;//目标Boss仍然存在，继续战斗
            }
            //Boss已被击败，重置追踪数据
            TargetWeaponDamageDealt = 0f;
            TotalBossDamage = 0f;
            IsBossFightActive = false;
        }
    }

    /// <summary>
    /// 通用的伤害追踪系统基类，用于追踪玩家对特定NPC使用特定武器造成的伤害
    /// </summary>
    internal abstract class BaseDamageTracker : GlobalNPC, IWorldInfo
    {
        //伤害追踪数据
        internal static float TargetWeaponDamageDealt = 0f;
        internal static float TotalBossDamage = 0f;
        internal static bool IsBossFightActive = false;
        internal static int HuntingNPCID;

        //需要子类实现的配置
        internal abstract int TargetNPCType { get; }
        internal virtual HashSet<int> OtherNPCType => [];

        internal abstract int[] TargetWeaponTypes { get; }
        internal abstract int[] TargetProjectileTypes { get; }
        internal abstract float RequiredContribution { get; }

        public override bool InstancePerEntity => true;//对应NPC实例创建一个实例

        internal bool IsTargetByID(NPC npc) => npc.type == TargetNPCType || OtherNPCType.Contains(npc.type);
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => IsTargetByID(entity);

        void IWorldInfo.OnWorldLoad() {
            ResetDamageTracking();
        }

        protected virtual void ResetDamageTracking() {
            TargetWeaponDamageDealt = 0f;
            TotalBossDamage = 0f;
            IsBossFightActive = false;
        }

        public override void AI(NPC npc) {
            if (npc.type != TargetNPCType) {
                return;
            }

            //Boss存在时标记战斗激活
            IsBossFightActive = npc.active;
            //记录Boss总生命值
            TotalBossDamage = npc.lifeMax;
            //标记目标
            HuntingNPCID = TargetNPCType;
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (!IsTargetByID(npc)) {
                return;
            }

            //使用回调来追踪实际造成的伤害
            modifiers.ModifyHitInfo += (ref NPC.HitInfo info) => {
                if (IsTargetWeapon(item.type)) {
                    TargetWeaponDamageDealt += info.Damage;
                }
            };
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (!IsTargetByID(npc)) {
                return;
            }

            modifiers.ModifyHitInfo += (ref NPC.HitInfo info) => {
                //检测弹幕是否来自目标武器
                if (IsTargetProjectile(projectile)) {
                    TargetWeaponDamageDealt += info.Damage;
                }
            };
        }

        public sealed override void OnKill(NPC npc) {
            //弃用，改为在ADVHook中调用Check方法
        }

        internal void Check(NPC npc) {
            if (npc.type != TargetNPCType) {
                return;
            }

            CheckQuestCompletion();

            //重置追踪数据
            ResetDamageTracking();
        }

        /// <summary>
        /// 检查任务是否完成
        /// </summary>
        protected virtual void CheckQuestCompletion() {
            Player player = Main.LocalPlayer;

            //检测是否造成足够的伤害贡献
            float contribution = TotalBossDamage > 0 ? TargetWeaponDamageDealt / TotalBossDamage : 0f;
            if (contribution < RequiredContribution) {
                ShowFailureMessage(player, $"{FailureReasonInsufficientDamage.Value} ({contribution:P0}/{RequiredContribution:P0})");
                return;
            }

            //任务完成
            OnQuestCompleted(player, contribution);
            ShowSuccessMessage(player, contribution);
        }

        /// <summary>
        /// 当任务完成时调用，子类可重写以实现自定义逻辑
        /// </summary>
        public abstract void OnQuestCompleted(Player player, float contribution);

        /// <summary>
        /// 显示任务失败消息
        /// </summary>
        public virtual void ShowFailureMessage(Player player, string reason) {
            int combat = CombatText.NewText(player.Hitbox, Color.Red, $"{QuestFailedPrefix.Value}: {reason}", true);
            Main.combatText[combat].lifeTime = 300;//延长显示时间
        }

        /// <summary>
        /// 显示任务成功消息
        /// </summary>
        public virtual void ShowSuccessMessage(Player player, float contribution) {
            int combat = CombatText.NewText(player.Hitbox, Color.Gold, $"{QuestCompletedPrefix.Value} {SuccessDamageContribution.Value}: {contribution:P0}", true);
            Main.combatText[combat].lifeTime = 300;//延长显示时间
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
        /// 获取当前伤害追踪数据供UI使用
        /// </summary>
        public static (float targetWeaponDamage, float totalDamage, bool isActive) GetDamageTrackingData() {
            return (TargetWeaponDamageDealt, TotalBossDamage, IsBossFightActive);
        }
    }
}
