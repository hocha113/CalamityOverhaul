namespace CalamityOverhaul.Content.ADV.Scenarios
{
    /// <summary>
    /// 对话场景接口
    /// </summary>
    public interface IADVScenario
    {
        string Key { get; }
        bool CanRepeat { get; }
        bool IsCompleted { get; }
        void Start();
        void Reset();
    }
}
