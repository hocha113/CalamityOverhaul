namespace CalamityOverhaul.Content.UIs.Core
{
    /// <summary>
    /// 继承这个类以修改游戏主页内容，实例为唯一的，在<see cref="UIs.ILMainMenuModification"/>中统一管理
    /// </summary>
    internal class BaseMainMenuOverUI : CWRUIPanel
    {
        public virtual bool Active => true;
        public virtual bool CanLoad() {
            return true;
        }
    }
}
