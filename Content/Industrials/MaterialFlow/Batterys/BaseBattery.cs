namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys
{
    internal class BaseBattery : MachineTP
    {
        /// <summary>
        /// 作为一个电池是否是接收能量的端口？
        /// 存储能量的电池默认将这个设置为false，否则抽取管道会将能量灌输进这里
        /// 反之，能量抽取管道会从这里抽取能量
        /// </summary>
        public virtual bool ReceivedEnergy => false;

        public sealed override void SetMachine() {
            SetBattery();
        }

        public virtual void SetBattery() {

        }
    }
}
