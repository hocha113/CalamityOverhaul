namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    //仅仅作为一个传递用的父类
    public abstract class BaseUEPipelineTP : MachineTP
    {
        public virtual Color BaseColor => Color.White;
        public override float MaxUEValue => 20;
    }
}
