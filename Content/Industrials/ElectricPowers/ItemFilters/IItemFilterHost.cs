namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>
    /// 过滤名单宿主：任何持有<see cref="ItemFilterSet"/>并允许玩家编辑的对象(手持卡、收集器、物流管道等)<br/>
    /// <see cref="ItemFilterEditorUI"/>只面向该接口工作，一个编辑器服务所有宿主；
    /// 宿主自己负责数据变化后的网络同步(<see cref="OnFilterChanged"/>)
    /// </summary>
    internal interface IItemFilterHost
    {
        /// <summary>宿主持有的过滤名单</summary>
        ItemFilterSet Filter { get; }

        /// <summary>编辑器标题栏显示的宿主名称</summary>
        string FilterHostName { get; }

        /// <summary>
        /// 宿主是否仍然有效(物品仍在本地玩家背包、TP实体仍存活等)，
        /// 编辑器每帧检查，失效时自动关闭
        /// </summary>
        bool FilterHostAlive { get; }

        /// <summary>
        /// 宿主的世界坐标(TP实体等)，编辑器用于距离过远时自动关闭；
        /// 手持物品等无固定位置的宿主返回<see langword="null"/>
        /// </summary>
        Vector2? FilterHostWorldCenter => null;

        /// <summary>
        /// 名单内容在本地被修改后调用，宿主在此完成自己的网络同步
        /// (TP实体走<c>SendData()</c>，手持物品走<c>SyncEquipment</c>)
        /// </summary>
        void OnFilterChanged();

        /// <summary>宿主是否支持"卸载过滤器"操作(如收集器解除过滤模式)</summary>
        bool CanUninstallFilter => false;

        /// <summary>执行卸载，仅在<see cref="CanUninstallFilter"/>为<see langword="true"/>时由编辑器调用</summary>
        void UninstallFilter() { }
    }
}
