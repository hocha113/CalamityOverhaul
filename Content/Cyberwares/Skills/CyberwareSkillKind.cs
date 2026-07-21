namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>技能输入语义</summary>
    internal enum CyberwareSkillKind
    {
        /// <summary>瞬发，触发键立刻执行</summary>
        Instant,
        /// <summary>开关，每次触发切换</summary>
        Toggle,
        /// <summary>蓄力，松开按比例释放；移开扇区清零</summary>
        Charge,
    }
}
