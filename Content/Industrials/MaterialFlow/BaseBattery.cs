namespace CalamityOverhaul.Content.Industrials.MaterialFlow
{
    internal class BaseBattery : MachineTP
    {
        public sealed override void SetMachine() {
            SetBattery();
        }

        public virtual void SetBattery() {

        }
    }
}
