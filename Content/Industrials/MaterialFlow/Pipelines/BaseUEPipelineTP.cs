namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    //仅仅作为一个传递用的父类
    internal abstract class BaseUEPipelineTP : MachineTP
    {
        internal int TurningID { get; set; }
        internal bool Turning { get; set; }
        internal bool Decussation { get; set; }
        internal int ThreeCrutchesID { get; set; }
        public override float MaxUEValue => 20;
    }
}
