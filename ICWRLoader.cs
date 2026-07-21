namespace CalamityOverhaul
{
    /// <summary>分阶段 Load/Setup/Unload 钩子</summary>
    internal interface ICWRLoader
    {
        /// <summary>客户端资源，PostSetup 末、非服务器</summary>
        public void LoadAsset() { }
        /// <summary>PostSetup，改已注册内容</summary>
        public void SetupData() { }
        /// <summary>Load 前期数据</summary>
        public void LoadData() { }
        /// <summary>Unload</summary>
        public void UnLoadData() { }
    }
}
