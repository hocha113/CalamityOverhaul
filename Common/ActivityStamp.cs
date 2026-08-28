using Terraria;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 帧戳闩锁：被扫描的活跃方每帧盖戳，扫描方先问"最近几帧内有没有人盖过戳"，
    /// 无戳即跳过全表扫描。无需配对注册/注销，实体消失后戳自然过期；
    /// 换世界后 <see cref="Main.GameUpdateCount"/> 无论前进还是回绕，无符号差值都会判为过期，天然安全。
    /// </summary>
    internal struct ActivityStamp
    {
        private uint lastStampFrame;
        private bool stamped;

        /// <summary>活跃方在自己的每帧更新里调用</summary>
        public void Stamp() {
            lastStampFrame = Main.GameUpdateCount;
            stamped = true;
        }

        /// <summary>最近 <paramref name="withinTicks"/> 帧内是否有人盖过戳</summary>
        public readonly bool ActiveWithin(uint withinTicks = 2)
            => stamped && Main.GameUpdateCount - lastStampFrame <= withinTicks;

        public void Reset() => stamped = false;
    }
}
