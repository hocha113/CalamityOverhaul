namespace CalamityOverhaul.Content.ADV.EntrustManager
{
    /// <summary>委托引导存档</summary>
    internal class EntrustGuideModule : ADVDataModule
    {
        /// <summary>引导已完成则不再触发</summary>
        public bool GuideSeen = false;
    }
}
