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

        /// <summary>劫持敌我判定，期间转为替施法者开火</summary>
        void ApplyHijack(int frames, Player caster);

        //弹药覆写口（HACK32 自扩展接口 IMunitionFeedTurret 并回，设计稿原意如此）

        /// <summary>弹药覆写生效中</summary>
        bool MunitionOverrideActive { get; }

        /// <summary>覆写弹药的物品类型，未覆写时为 0</summary>
        int MunitionAmmoType { get; }

        /// <summary>换上玩家的弹药：改发射物、改伤害、翻转 IFF，每发从喂弹者背包扣一</summary>
        void ApplyMunitionOverride(int ammoItemType, int projType, int damage, Player feeder, int frames);

        /// <summary>提前终止弹药覆写（弹尽或效果被移除）</summary>
        void ClearMunitionOverride();

        //组网口（HACK32 自扩展接口 IMeshFireTurret 并回），成员的开火与瞄准由 TurretMesh 协议统一编排

        /// <summary>入网：唤醒停摆并压掉本机索敌</summary>
        void JoinMesh(int rootSlot, int frames);

        /// <summary>离网，恢复本机行为</summary>
        void LeaveMesh();

        /// <summary>协议逐帧写入的齐射瞄准点</summary>
        void SetMeshAim(Vector2 worldTarget);

        /// <summary>朝目标打出一发友方弹，弹池扣账在协议侧</summary>
        void MeshFire(Vector2 worldTarget, Player caster);
    }
}
