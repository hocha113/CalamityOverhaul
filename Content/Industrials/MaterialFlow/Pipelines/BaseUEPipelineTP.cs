namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    /// <summary>UE 管道 TP 基类</summary>
    public abstract class BaseUEPipelineTP : MachineTP
    {
        public virtual Color BaseColor => Color.White;
        public override float MaxUEValue => 100;
    }
}
