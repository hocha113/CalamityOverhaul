namespace CalamityOverhaul.Content
{
    internal interface IWorldInfo
    {
        /// <summary>
        /// 最好只操作静态数据
        /// </summary>
        public void OnWorldLoad() { }
        /// <summary>
        /// 最好只操作静态数据
        /// </summary>
        public void OnWorldUnLoad() { }
    }
}
