using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow
{
    internal class BaseBattery : TileProcessor
    {
        internal MachineData GeneratorData;
        internal virtual float MaxUEValue => 6000f;
    }
}
