using CalamityOverhaul.Content.Wraiths.Runtime;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 厉鬼行为积木。实例随 Actor 生成逐个新建（可持有内部状态），
    /// 仅在权威端（服务器/单人）被逐帧驱动，按列表顺序叠加对速度/状态的贡献
    /// </summary>
    public interface IWraithBehavior
    {
        /// <summary>权威端每帧调用，直接读写 <paramref name="wraith"/> 的运动字段</summary>
        void Update(WraithActor wraith);
    }
}
