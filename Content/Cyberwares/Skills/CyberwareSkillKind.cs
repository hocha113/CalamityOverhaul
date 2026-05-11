namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>
    /// 义体技能的输入语义类型，决定雷达内的交互方式
    /// </summary>
    internal enum CyberwareSkillKind
    {
        /// <summary>
        /// 瞬时类技能：在雷达中选中并释放鼠标方向键即立刻触发一次
        /// </summary>
        Instant,
        /// <summary>
        /// 开关类技能：每次"释放选中"切换开/关状态
        /// </summary>
        Toggle,
        /// <summary>
        /// 蓄力类技能：在雷达中悬停期间持续蓄力，松开按键时按蓄力比例释放
        /// <br/>悬停时间即蓄力时间，移开扇区会清零该技能的蓄力
        /// </summary>
        Charge,
    }
}
