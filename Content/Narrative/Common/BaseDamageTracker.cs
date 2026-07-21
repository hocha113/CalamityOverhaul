using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.Narrative.Common.BaseDamageTracker;
using static CalamityOverhaul.Content.Narrative.Common.DamageTrackerSystem;

namespace CalamityOverhaul.Content.Narrative.Common
{
    internal class DamageTrackerSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI.QuestTracker";

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
            TargetWeaponDamageDealt = 0f;
            TotalBossDamage = 0f;
            IsBossFightActive = false;
            CurrentDamageTrackerInstance = null;
        }

        public override void PostUpdateNPCs() {
            if (!IsBossFightActive) {
                DealtReset();
                return;
            }

            if (CurrentDamageTrackerInstance != null
                && CurrentDamageTrackerInstance.NPC.Alives()
                && NPC.AnyNPCs(CurrentDamageTrackerInstance.NPC.type)) {
                return;
            }

            DealtReset();
        }
    }

    internal abstract class BaseDamageTracker : DeathTrackingNPC, IWorldInfo
    {
        internal static float TargetWeaponDamageDealt = 0f;
        internal static float TotalBossDamage = 0f;
        internal static bool IsBossFightActive = false;
        /// <summary>当前追踪实例</summary>
        internal static BaseDamageTracker CurrentDamageTrackerInstance { get; set; }

        internal abstract int TargetNPCType { get; }
        internal virtual HashSet<int> OtherNPCType => [];

        internal abstract int[] TargetWeaponTypes { get; }
        internal abstract int[] TargetProjectileTypes { get; }
        internal abstract float RequiredContribution { get; }

        /// <summary>被追踪 NPC，周期内可 null</summary>
        internal NPC NPC { get; private set; }

        public override bool InstancePerEntity => true;

        internal bool IsTargetByID(NPC npc) => npc.type == TargetNPCType || OtherNPCType.Contains(npc.type);
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => IsTargetByID(entity);

        void IWorldInfo.OnWorldLoad() {
            ResetDamageTracking();
        }

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

            IsBossFightActive = npc.active;
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

            if (!IsQuestActive(player)) {
                return;
            }

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

            if (!IsQuestActive(player)) {
                return;
            }

            if (IsTargetProjectile(projectile)) {
                TargetWeaponDamageDealt += hit.Damage;
                return;
            }

            if (projectile.Alives() && projectile.CWR().Source is EntitySource_ItemUse itemSource && IsTargetWeapon(itemSource.Item.type)) {
                TargetWeaponDamageDealt += hit.Damage;
            }
        }

        public sealed override void OnNPCDeath(NPC npc) {
            if (IsTargetByID(npc)) {
                //OnKill仅客户端
                Check(npc);
            }
        }

        internal void Check(NPC npc) {
            if (npc.type != TargetNPCType) {
                return;
            }

            if (!IsQuestActive(Main.LocalPlayer)) {
                return;
            }

            CheckQuestCompletion();

            ResetDamageTracking();
        }

        protected virtual void CheckQuestCompletion() {
            Player player = Main.LocalPlayer;

            float contribution = TotalBossDamage > 0 ? TargetWeaponDamageDealt / TotalBossDamage : 0f;
            int contributionPct = (int)Math.Round(contribution * 100);
            int requiredPct = (int)Math.Round(RequiredContribution * 100);
            if (contributionPct < requiredPct) {
                ShowFailureMessage(player, $"{FailureReasonInsufficientDamage.Value} ({contribution:P0}/{RequiredContribution:P0})");
                return;
            }

            OnQuestCompleted(player, contribution);
            ShowSuccessMessage(player, contribution);
        }

        public abstract void OnQuestCompleted(Player player, float contribution);

        public virtual void ShowFailureMessage(Player player, string reason) {
            int combat = CombatText.NewText(player.Hitbox, Color.Red, $"{QuestFailedPrefix.Value}: {reason}", true);
            Main.combatText[combat].lifeTime = 300;  //延寿300
            VaultUtils.Text($"{QuestFailedPrefix.Value}: {reason}", Color.Red);
        }

        public virtual void ShowSuccessMessage(Player player, float contribution) {
            int combat = CombatText.NewText(player.Hitbox, Color.Gold, $"{QuestCompletedPrefix.Value} {SuccessDamageContribution.Value}: {contribution:P0}", true);
            Main.combatText[combat].lifeTime = 300;  //延寿300
            VaultUtils.Text($"{QuestCompletedPrefix.Value} {SuccessDamageContribution.Value}: {contribution:P0}", Color.Gold);
        }

        protected virtual bool IsTargetWeapon(int itemType) {
            foreach (int weaponType in TargetWeaponTypes) {
                if (itemType == weaponType) {
                    return true;
                }
            }
            return false;
        }

        protected virtual bool IsTargetProjectile(Projectile projectile) {
            foreach (int projType in TargetProjectileTypes) {
                if (projectile.type == projType) {
                    return true;
                }
            }
            return false;
        }

        public static (float targetWeaponDamage, float totalDamage, bool isActive) GetDamageTrackingData() {
            return (TargetWeaponDamageDealt, TotalBossDamage, IsBossFightActive);
        }
    }
}
