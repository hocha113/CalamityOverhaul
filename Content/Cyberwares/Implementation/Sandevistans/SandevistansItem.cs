using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦义体基类
    /// <br/>子类覆写冷却参数即接入 HUD/系统
    /// </summary>
    internal class SandevistansItem : BaseCyberware
    {
        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.NervousSystem;

        public override int CapacityCost => 3;

        public override CyberwareSkillBase ActiveSkill => SandevistanSkill.Instance;

        /// <summary>最大冷却帧，总可持续时长</summary>
        public virtual float MaxCooldownTime => 480f;

        /// <summary>激活每帧消耗，越大越短</summary>
        public virtual float ConsumptionPerFrame => 1.5f;

        /// <summary>停用每帧恢复</summary>
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
