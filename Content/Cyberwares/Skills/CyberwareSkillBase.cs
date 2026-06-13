using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>
    /// 义体技能描述符与触发入口
    /// <br/>BaseCyberware.ActiveSkill 返回单例；雷达遍历装备构建扇区
    /// <br/>冷却/蓄力/激活态留在 ModPlayer/ModSystem
    /// </summary>
    internal abstract class CyberwareSkillBase
    {
        /// <summary>
        /// 稳定标识，供 CurrentSkillId 存档
        /// <br/>默认类名；重命名类时可覆写固定字符串保兼容
        /// </summary>
        public virtual string Identifier => GetType().Name;

        /// <summary>扇区主标题，应本地化</summary>
        public abstract string DisplayName { get; }

        /// <summary>悬停二级描述，应本地化</summary>
        public abstract string Description { get; }

        /// <summary>扇区右上角状态字（RAM/冷却/ON 等），可空</summary>
        public virtual string StatusText => string.Empty;

        /// <summary>扇区图标 Item 类型，-1 不绘</summary>
        public virtual int IconItemType => -1;

        /// <summary>扇区填充 0~1（冷却/能量/蓄力等）</summary>
        public virtual float StatusFillRatio => 1f;

        /// <summary>可释放，false 时扇区灰显</summary>
        public virtual bool IsReady => true;

        /// <summary>输入类型</summary>
        public virtual CyberwareSkillKind Kind => CyberwareSkillKind.Instant;

        /// <summary>Toggle 已开启，雷达绘开标记</summary>
        public virtual bool IsActivated => false;

        /// <summary>雷达悬停蓄力 0~1，Charge 专用</summary>
        public float RadialChargeRatio { get; internal set; }

        /// <summary>蓄满帧数，默认 60，不受时缓</summary>
        public virtual int FullChargeTicks => 60;

        /// <summary>Instant 触发，瞄点读 Main.MouseWorld</summary>
        public virtual void OnInstantTrigger(Player player) { }

        /// <summary>Toggle 切换</summary>
        public virtual void OnToggleTrigger(Player player) { }

        /// <summary>Charge 蓄力每帧反馈</summary>
        public virtual void OnChargeTick(Player player, float ratio) { }

        /// <summary>Charge 松开释放入口</summary>
        public virtual void OnChargeRelease(Player player, float ratio) { }

        /// <summary>Charge 打断清理，无副作用</summary>
        public virtual void OnChargeCancel(Player player) { }
    }
}
