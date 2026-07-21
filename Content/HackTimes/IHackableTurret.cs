using InnoVault.Actors;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>可骇入炮台 Actor</summary>
    internal interface IHackableTurret : IHackTarget
    {
        Actor AsActor { get; }

        /// <summary>电路过载失效中</summary>
        bool IsCircuitDisabled { get; }

        /// <summary>剩余失效帧数</summary>
        int CircuitDisabledFrames { get; }

        /// <summary>电路短路，一次性放电</summary>
        void ApplyShortCircuit(int frames, Player caster);

        /// <summary>电路过载，长时间失效</summary>
        void ApplyCircuitOverload(int frames, Player caster);
    }
}
