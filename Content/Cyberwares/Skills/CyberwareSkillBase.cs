using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>
    /// 单个义体技能的描述符与触发入口
    /// <br/>每个具体的 <see cref="BaseCyberware"/> 通过覆写 <see cref="BaseCyberware.ActiveSkill"/>
    /// 返回一个本类型的单例实例，<see cref="CyberwareSkillRadialUI"/> 在打开雷达时会遍历所有装备义体的
    /// <see cref="BaseCyberware.ActiveSkill"/> 并构建扇区
    /// <br/>技能的真实运行时状态（冷却 / 蓄力 / 激活状态等）仍然保留在各自的 ModPlayer / ModSystem 中，
    /// 本类型只是一个轻量描述符，避免与现有义体实现深度耦合
    /// </summary>
    internal abstract class CyberwareSkillBase
    {
        /// <summary>
        /// 雷达扇区主标题文字（应使用本地化文本）
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// 雷达 hover 时二级信息面板的描述正文（应使用本地化文本）
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// 在雷达扇区右上角显示的状态文字（例如 "RAM 3" "12s" "ON"），可返回空字符串
        /// </summary>
        public virtual string StatusText => string.Empty;

        /// <summary>
        /// 在雷达扇区中央显示图标用的 Item 类型 ID
        /// <br/>默认值 -1 表示不绘制图标，由实现类返回对应义体物品类型时复用原版物品贴图
        /// </summary>
        public virtual int IconItemType => -1;

        /// <summary>
        /// 0~1 的状态值，作为雷达扇区的填充进度
        /// <br/>语义随技能不同：可表示冷却剩余、能量储备、蓄力进度等，0 = 空，1 = 满
        /// </summary>
        public virtual float StatusFillRatio => 1f;

        /// <summary>
        /// 技能是否处于可释放状态（false 时扇区灰显且无法选中触发）
        /// </summary>
        public virtual bool IsReady => true;

        /// <summary>
        /// 该技能的输入类型，决定雷达的交互行为
        /// </summary>
        public virtual CyberwareSkillKind Kind => CyberwareSkillKind.Instant;

        /// <summary>
        /// 已激活/已开启状态（Toggle 类语义）；雷达据此为开关型技能绘制额外的"开"标记
        /// </summary>
        public virtual bool IsActivated => false;

        /// <summary>
        /// 当前在雷达中累积的蓄力比例（0~1），仅 <see cref="CyberwareSkillKind.Charge"/> 使用
        /// <br/>由 <see cref="CyberwareSkillRadialPlayer"/> 在悬停期间写入，雷达据此扩展扇区填充
        /// </summary>
        public float RadialChargeRatio { get; internal set; }

        /// <summary>
        /// 蓄满所需的实时帧数（实际时间，不受时缓影响）
        /// <br/>默认 60 帧（1 秒），<see cref="CyberwareSkillKind.Charge"/> 类应按需覆写
        /// </summary>
        public virtual int FullChargeTicks => 60;

        /// <summary>
        /// 本技能在触发时是否需要一个世界坐标系下的瞄点
        /// <list type="bullet">
        ///   <item>仅对 <see cref="CyberwareSkillKind.Instant"/> 有意义</item>
        ///   <item><see langword="true"/> 时雷达会在玩家按下技能键的同一帧快照 <c>Main.MouseWorld</c>，
        ///     松开时把快照位置作为 <paramref name="aimWorld"/> 传入 <see cref="OnInstantTrigger(Player, Vector2)"/>，
        ///     避免雷达期间鼠标被劫持去选扇区导致瞄点丢失</item>
        ///   <item>单技能直触路径（雷达不开盘）下，瞄点仍取自玩家当前鼠标，
        ///     由技能自行通过 <c>Main.MouseWorld</c> / <c>Player.tileTargetX/Y</c> 获取，
        ///     本属性此时不参与判断</item>
        /// </list>
        /// </summary>
        public virtual bool RequiresAim => false;

        /// <summary>
        /// Instant 类技能的触发入口；雷达在释放鼠标方向键且未蓄力时调用一次
        /// </summary>
        public virtual void OnInstantTrigger(Player player) { }

        /// <summary>
        /// 带瞄点的瞬时触发入口
        /// <br/>当 <see cref="RequiresAim"/> 为 <see langword="true"/> 时，雷达会调用本重载并传入按 V 那一帧的鼠标世界坐标快照
        /// <br/>默认实现转发到无参版本，便于增量迁移；方向类技能应覆写本重载使用 <paramref name="aimWorld"/>
        /// </summary>
        public virtual void OnInstantTrigger(Player player, Vector2 aimWorld) => OnInstantTrigger(player);

        /// <summary>
        /// Toggle 类技能的状态切换入口
        /// </summary>
        public virtual void OnToggleTrigger(Player player) { }

        /// <summary>
        /// Charge 类技能在雷达悬停的每一帧被调用，<paramref name="ratio"/> 为已累积的蓄力比例（0~1）
        /// <br/>可用于在玩家身上叠加蓄力期间的视觉/音效反馈
        /// </summary>
        public virtual void OnChargeTick(Player player, float ratio) { }

        /// <summary>
        /// Charge 类技能在雷达关闭瞬间（即玩家松开方向键）的释放入口
        /// </summary>
        public virtual void OnChargeRelease(Player player, float ratio) { }

        /// <summary>
        /// Charge 类技能在悬停被打断（切换扇区 / 雷达被强制取消）时调用，
        /// 用于清理临时视觉状态，不应造成游戏内影响
        /// </summary>
        public virtual void OnChargeCancel(Player player) { }
    }
}
