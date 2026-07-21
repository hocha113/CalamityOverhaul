namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys
{
    public class BaseBattery : MachineTP
    {
        /// <summary>true=受电口(管道灌入)；false=储能电池(管道抽取)，默认 false</summary>
        public virtual bool ReceivedEnergy => false;

        public sealed override void SetMachine() {
            SetBattery();
        }

        public virtual void SetBattery() {

        }
    }
}
