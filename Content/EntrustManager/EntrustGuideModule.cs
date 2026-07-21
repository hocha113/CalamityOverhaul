using InnoVault.DataModules;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>委托引导存档</summary>
    internal class EntrustGuideModule : DataModule
    {
        /// <summary>已看过则不再触发</summary>
        public bool GuideSeen = false;
    }
}
