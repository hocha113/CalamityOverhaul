using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>
    /// ActiveSkill 单例；冷却/蓄力态放 ModPlayer
    /// </summary>
    internal abstract class CyberwareSkillBase
    {
        /// <summary>存档 id，默认类名；重命名类请覆写固定串</summary>
        public virtual string Identifier => GetType().Name;

        public abstract string DisplayName { get; }

        public abstract string Description { get; }

        /// <summary>扇区右上角状态字，可空</summary>
        public virtual string StatusText => string.Empty;

        /// <summary>扇区图标 Item 类型，-1 不绘</summary>
        public virtual int IconItemType => -1;

        /// <summary>扇区填充 0~1</summary>
        public virtual float StatusFillRatio => 1f;

        /// <summary>false 扇区灰显</summary>
        public virtual bool IsReady => true;

        public virtual CyberwareSkillKind Kind => CyberwareSkillKind.Instant;

        /// <summary>Toggle 开着时雷达绘开标记</summary>
        public virtual bool IsActivated => false;

        /// <summary>Charge 蓄力 0~1</summary>
        public float RadialChargeRatio { get; internal set; }

        /// <summary>蓄满帧数，默认 60，不受时缓</summary>
        public virtual int FullChargeTicks => 60;

        /// <summary>Instant，瞄点 Main.MouseWorld</summary>
        public virtual void OnInstantTrigger(Player player) { }

        public virtual void OnToggleTrigger(Player player) { }

        public virtual void OnChargeTick(Player player, float ratio) { }

        public virtual void OnChargeRelease(Player player, float ratio) { }

        /// <summary>Charge 打断，应无副作用</summary>
        public virtual void OnChargeCancel(Player player) { }
    }
}
