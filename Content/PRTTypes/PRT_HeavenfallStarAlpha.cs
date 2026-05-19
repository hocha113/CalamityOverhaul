
namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_HeavenfallStarAlpha : PRT_HeavenfallStar
    {
        public override bool CanPool => true;
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
    }
}
