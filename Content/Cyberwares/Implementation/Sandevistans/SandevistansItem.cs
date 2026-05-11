using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦义体物品，也是所有斯安威斯坦型号的基类
    /// 不同型号继承此类并覆写冷却参数即可自动接入冷却系统和HUD
    /// </summary>
    internal class SandevistansItem : BaseCyberware
    {
        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.NervousSystem;

        public override int CapacityCost => 3;

        public override CyberwareSkillBase ActiveSkill => SandevistanSkill.Instance;

        /// <summary>
        /// 最大冷却容量（帧数），决定可持续激活的总时长
        /// </summary>
        public virtual float MaxCooldownTime => 480f;

        /// <summary>
        /// 激活状态下每帧消耗的冷却值，值越大持续时间越短
        /// </summary>
        public virtual float ConsumptionPerFrame => 1.5f;

        /// <summary>
        /// 未激活时每帧恢复的冷却值，值越大回复越快
        /// </summary>
        public virtual float RecoveryPerFrame => 0.8f;

        public override void OnEquip(Player player) {
            Sandevistan.CurrentCooldown = MaxCooldownTime;
        }

        public override void OnUnequip(Player player) {
            Sandevistan.ForceDeactivate();
            Sandevistan.CurrentCooldown = 0;
        }
    }
}
