using CalamityOverhaul.Common;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items
{
    /// <summary>
    /// 这是一个属于副本物品的基类，因为大修的基本框架需要副本物品的存在，所以建立一个类用于副本物品的继承重写是必须的，<br/>
    /// 同时，副本物品也需要一个统一的父级关系来方便管理，而不是与其他的物品混杂着共用<see cref="ModItem"/>父级关系
    /// </summary>
    public abstract class EctypeItem : ModItem
    {
        protected virtual bool isload => false;
        public sealed override bool IsLoadingEnabled(Mod mod) {
            return CWRServerConfig.Instance.ForceReplaceResetContent && !isload ? false : base.IsLoadingEnabled(mod);
        }
    }
}
