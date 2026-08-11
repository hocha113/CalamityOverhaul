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

        //假信标口（HACK32 自扩展接口 IDistressBeaconTower 并回），BeaconForge 协议专用

        /// <summary>信标生效中</summary>
        bool DistressBeaconActive { get; }

        void BeginDistressBeacon(int frames, Player caster);

        void EndDistressBeacon();

        //提权上行口（HACK32 自扩展接口 IPrivilegeUplinkTower 并回），只驱动演出，折扣状态在 PrivilegeEscalateState

        void BeginPrivilegeUplink(int frames, Player caster);
    }
}
