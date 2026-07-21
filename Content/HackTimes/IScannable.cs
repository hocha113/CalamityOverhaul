namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>可扫描目标，扫描数据与具体类型解耦</summary>
    internal interface IScannable
    {
        Vector2 WorldCenter { get; }

        bool IsValid { get; }

        bool IsHackable { get; }

        /// <summary>扫描数据行数</summary>
        int ScanRowCount { get; }

        /// <summary>填充扫描面板行</summary>
        void BuildScanData(string[] labels, string[] values, Color[] colors);
    }
}
