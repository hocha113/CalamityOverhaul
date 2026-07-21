namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>
    /// 可编辑<see cref="ItemFilterSet"/>的宿主；<see cref="ItemFilterEditorUI"/>只认此接口<br/>
    /// 同步由宿主<see cref="OnFilterChanged"/>自行完成
    /// </summary>
    internal interface IItemFilterHost
    {
        ItemFilterSet Filter { get; }

        /// <summary>标题栏宿主名</summary>
        string FilterHostName { get; }

        /// <summary>失效时编辑器关闭</summary>
        bool FilterHostAlive { get; }

        /// <summary>过远关闭用；无固定位返回null</summary>
        Vector2? FilterHostWorldCenter => null;

        /// <summary>本地改名单后同步(TP.SendData / SyncEquipment)</summary>
        void OnFilterChanged();

        /// <summary>是否支持卸载过滤</summary>
        bool CanUninstallFilter => false;

        /// <summary>卸载，仅CanUninstallFilter时由编辑器调用</summary>
        void UninstallFilter() { }
    }
}
