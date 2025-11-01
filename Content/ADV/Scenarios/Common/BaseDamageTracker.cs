using Terraria;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Scenarios.Common.BaseDamageTracker;

namespace CalamityOverhaul.Content.ADV.Scenarios.Common
{
    internal class DamageTrackerSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (!IsBossFightActive) {
                return;//û�м����Bossս����ֱ�ӷ���
            }
            if (NPC.AnyNPCs(HuntingNPCID)) {
                return;//Ŀ��Boss��Ȼ���ڣ�����ս��
            }
            //Boss�ѱ����ܣ�����׷������
            TargetWeaponDamageDealt = 0f;
            TotalBossDamage = 0f;
            IsBossFightActive = false;
        }
    }

    /// <summary>
    /// ͨ�õ��˺�׷��ϵͳ���࣬����׷����Ҷ��ض�NPCʹ���ض�������ɵ��˺�
    /// </summary>
    internal abstract class BaseDamageTracker : GlobalNPC, IWorldInfo
    {
        //�˺�׷������
        internal static float TargetWeaponDamageDealt = 0f;
        internal static float TotalBossDamage = 0f;
        internal static bool IsBossFightActive = false;
        internal static int HuntingNPCID;

        //��Ҫ����ʵ�ֵ�����
        internal abstract int TargetNPCType { get; }
        internal abstract int[] TargetWeaponTypes { get; }
        internal abstract int[] TargetProjectileTypes { get; }
        internal abstract float RequiredContribution { get; }

        public override bool InstancePerEntity => true;//��ӦNPCʵ������һ��ʵ��

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == TargetNPCType;

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

            //Boss����ʱ���ս������
            IsBossFightActive = npc.active;
            //��¼Boss������ֵ
            TotalBossDamage = npc.lifeMax;

            HuntingNPCID = TargetNPCType;
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (npc.type != TargetNPCType) {
                return;
            }

            //ʹ�ûص���׷��ʵ����ɵ��˺�
            modifiers.ModifyHitInfo += (ref NPC.HitInfo info) => {
                if (IsTargetWeapon(item.type)) {
                    TargetWeaponDamageDealt += info.Damage;
                }
            };
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (npc.type != TargetNPCType) {
                return;
            }

            modifiers.ModifyHitInfo += (ref NPC.HitInfo info) => {
                //��ⵯĻ�Ƿ�����Ŀ������
                if (IsTargetProjectile(projectile)) {
                    TargetWeaponDamageDealt += info.Damage;
                }
            };
        }

        public sealed override void OnKill(NPC npc) {
            //���ã���Ϊ��ADVHook�е���Check����
        }

        internal void Check(NPC npc) {
            if (npc.type != TargetNPCType) {
                return;
            }

            CheckQuestCompletion();

            //����׷������
            ResetDamageTracking();
        }

        /// <summary>
        /// ��������Ƿ����
        /// </summary>
        protected virtual void CheckQuestCompletion() {
            Player player = Main.LocalPlayer;

            //�������һ��ʹ��Ŀ������
            Item heldItem = player.GetItem();
            if (!IsTargetWeapon(heldItem.type)) {
                ShowFailureMessage(player, "δʹ��ָ������������һ��");
                return;
            }

            //��������㹻���˺�����
            float contribution = TotalBossDamage > 0 ? TargetWeaponDamageDealt / TotalBossDamage : 0f;
            if (contribution < RequiredContribution) {
                ShowFailureMessage(player, $"�����˺�ռ�Ȳ��� ({contribution:P0}/{RequiredContribution:P0})");
                return;
            }

            //�������
            OnQuestCompleted(player, contribution);
            ShowSuccessMessage(player, contribution);
        }

        /// <summary>
        /// ���������ʱ���ã��������д��ʵ���Զ����߼�
        /// </summary>
        public abstract void OnQuestCompleted(Player player, float contribution);

        /// <summary>
        /// ��ʾ����ʧ����Ϣ
        /// </summary>
        public virtual void ShowFailureMessage(Player player, string reason) {
            CombatText.NewText(player.Hitbox, Color.Red, $"����ʧ��: {reason}");
        }

        /// <summary>
        /// ��ʾ����ɹ���Ϣ
        /// </summary>
        public virtual void ShowSuccessMessage(Player player, float contribution) {
            CombatText.NewText(player.Hitbox, Color.Gold, $"�������! �˺�ռ��: {contribution:P0}");
        }

        /// <summary>
        /// �����Ʒ�Ƿ���Ŀ������
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
        /// ��鵯Ļ�Ƿ�����Ŀ������
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
        /// ��ȡ��ǰ�˺�׷�����ݹ�UIʹ��
        /// </summary>
        public static (float targetWeaponDamage, float totalDamage, bool isActive) GetDamageTrackingData() {
            return (TargetWeaponDamageDealt, TotalBossDamage, IsBossFightActive);
        }
    }
}
