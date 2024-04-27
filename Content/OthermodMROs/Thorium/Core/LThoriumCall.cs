using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.Core
{
    internal interface LThoriumCall
    {
        /// <summary>
        /// 在模组加载周期的最开始阶段调用
        /// 用于加载关于瑟银模组的数据，比如反射某些字段的内容并储存到外部
        /// 注意，这个方法的调用是单实例的，不能为当前实例进行编程，如果你想用这个方法向外部储存数据，
        /// 你需要保证它们是唯一的或者是静态的
        /// </summary>
        /// <param name="thoriumMod"></param>
        public void LoadThoDate(Mod thoriumMod);
        /// <summary>
        /// 在模组加载周期的最末期阶段调用
        /// 用于加载关于瑟银模组的数据，比如反射某些字段的内容并储存到外部
        /// 注意，这个方法的调用是单实例的，不能为当前实例进行编程，如果你想用这个方法向外部储存数据，
        /// 你需要保证它们是唯一的或者是静态的
        /// </summary>
        /// <param name="thoriumMod"></param>
        public void PostLoadThoDate(Mod thoriumMod);
    }
}
