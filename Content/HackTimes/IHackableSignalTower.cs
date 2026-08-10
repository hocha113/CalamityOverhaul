using InnoVault.Actors;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>可骇入信号塔 Actor</summary>
    internal interface IHackableSignalTower : IHackTarget
    {
        Actor AsActor { get; }

        /// <summary>病毒广播，范围炮台短路</summary>
        void BeginVirusBroadcast(float radiusPixels, int disableFrames, Player caster);

        /// <summary>电网瘫痪，范围内机械停机断电</summary>
        void BeginGridBlackout(float radiusPixels, int disableFrames, Player caster);
    }
}
