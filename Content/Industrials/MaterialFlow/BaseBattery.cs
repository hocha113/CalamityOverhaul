using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow
{
    internal class BaseBattery : TileProcessor
    {
        public MachineData MachineData;
        public virtual float MaxUEValue => 6000f;
    }
}
