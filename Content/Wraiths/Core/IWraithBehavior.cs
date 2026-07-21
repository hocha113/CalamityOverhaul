using CalamityOverhaul.Content.Wraiths.Runtime;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>行为积木，随 Actor 新建，仅权威端按列表顺序驱动</summary>
    public interface IWraithBehavior
    {
        /// <summary>权威端每帧，直接读写运动字段</summary>
        void Update(WraithActor wraith);
    }
}
