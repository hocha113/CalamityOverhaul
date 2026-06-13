using InnoVault.Actors;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>可骇入信号塔 Actor 契约</summary>
    internal interface IHackableSignalTower : IHackTarget
    {
        /// <summary>Actor 本体</summary>
        Actor AsActor { get; }

        /// <summary>病毒广播，范围炮台短路</summary>
        void BeginVirusBroadcast(float radiusPixels, int disableFrames, Player caster);
    }
}
