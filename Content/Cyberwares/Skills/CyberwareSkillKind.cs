namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>义体技能输入语义，决定雷达交互</summary>
    internal enum CyberwareSkillKind
    {
        /// <summary>瞬发：选中后按触发键立刻执行</summary>
        Instant,
        /// <summary>开关：每次触发切换开/关</summary>
        Toggle,
        /// <summary>
        /// 蓄力：悬停扇区累积，松开触发键按比例释放
        /// <br/>移开扇区清零蓄力
        /// </summary>
        Charge,
    }
}
