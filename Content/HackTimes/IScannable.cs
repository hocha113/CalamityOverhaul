namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>可扫描目标抽象，扫描数据与具体类型解耦</summary>
    internal interface IScannable
    {
        /// <summary>世界中心坐标</summary>
        Vector2 WorldCenter { get; }

        /// <summary>目标是否仍有效</summary>
        bool IsValid { get; }

        /// <summary>是否可被骇入</summary>
        bool IsHackable { get; }

        /// <summary>扫描数据行数</summary>
        int ScanRowCount { get; }

        /// <summary>构建扫描面板数据行</summary>
        void BuildScanData(string[] labels, string[] values, Color[] colors);
    }
}
