namespace CalamityOverhaul
{
    /// <summary>
    /// 提供一个通用的资源加载、卸载途径
    /// </summary>
    internal interface ILoader
    {
        /// <summary>
        /// 该方法在CWRLoad中的最后调用，并且不会在服务器上调用，一般用于加载Asset客户端资源
        /// </summary>
        public void LoadAsset() { }
        /// <summary>
        /// 该方法在CWRLoad中的最后调用
        /// </summary>
        public void SetupData() { }
        /// <summary>
        /// 该方法在CWRLoad前行调用
        /// </summary>
        public void LoadData() { }
        /// <summary>
        /// 该方法在CWRUnLoad最后调用
        /// </summary>
        public void UnLoadData() { }

        internal void DompLoadText() => CWRMod.Instance.Logger.Info($"{GetType().Name}已经完成加载操作");
        internal void DompUnLoadText() => CWRMod.Instance.Logger.Info($"{GetType().Name}已经完成卸载操作");
    }
}
