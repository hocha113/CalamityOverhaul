namespace CalamityOverhaul
{
    /// <summary>
    /// 提供一个通用的资源加载、卸载途径
    /// </summary>
    internal interface ICWRLoader
    {
        /// <summary>
        /// 在最后调用，并且不会在服务器上调用，一般加载资源
        /// </summary>
        public void LoadAsset() { }
        /// <summary>
        /// 在模组内容加载完成后调用，一般获取或修改模组内容
        /// </summary>
        public void SetupData() { }
        /// <summary>
        /// 在模组加载前期运行，载入数据
        /// </summary>
        public void LoadData() { }
        /// <summary>
        /// 在模组卸载时运行，卸载数据
        /// </summary>
        public void UnLoadData() { }
    }
}
