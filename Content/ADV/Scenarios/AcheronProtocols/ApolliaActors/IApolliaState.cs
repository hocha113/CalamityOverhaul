namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>阿波利娅行为状态接口：每个具体行为</summary>
    internal interface IApolliaState
    {
        /// <summary>进入该状态时调用一次</summary>
        void Enter(ApolliaActor actor);

        /// <summary>每帧更新，返回非null时表示要切换到新状态</summary>
        IApolliaState Update(ApolliaActor actor);

        /// <summary>离开该状态时调用一次</summary>
        void Exit(ApolliaActor actor);
    }
}
