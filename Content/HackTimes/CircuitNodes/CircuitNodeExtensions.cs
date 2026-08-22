using InnoVault.Actors;
using System.IO;

namespace CalamityOverhaul.Content.HackTimes.CircuitNodes
{
    //HACK32 整合：四个扩展接口的成员已并回 IHackableTurret / IHackableSignalTower
    //（设计稿原意如此，本批实现时因文件所有权拆成扩展接口）。
    //空壳继承保留类型名，协议里的 is 判定与成员调用照旧成立，不破坏任何调用点

    /// <summary>炮台弹药覆写口；成员见 <see cref="IHackableTurret"/></summary>
    internal interface IMunitionFeedTurret : IHackableTurret { }

    /// <summary>炮台组网口；成员见 <see cref="IHackableTurret"/></summary>
    internal interface IMeshFireTurret : IHackableTurret { }

    /// <summary>信号塔假信标口；成员见 <see cref="IHackableSignalTower"/></summary>
    internal interface IDistressBeaconTower : IHackableSignalTower { }

    /// <summary>信号塔提权上行口；成员见 <see cref="IHackableSignalTower"/></summary>
    internal interface IPrivilegeUplinkTower : IHackableSignalTower { }

    /// <summary>
    /// Actor 的跨端稳定身份：槽位 + 代 + 类型。<br/>
    /// 槽位会被复用，代由服务器在生成时分配并随生成包下发（<see cref="Actor.Generation"/>），
    /// 三元组一起才是身份，镜像 NetworkNPCIdentity 的形状。<br/>
    /// 现阶段炮台/信号塔目标被 HackEffectTracker 限在单人，本结构是为解除限制预留的线上格式，
    /// 读写两侧的接入点见 Doc/patches/HACK32-Circuit.md
    /// </summary>
    internal readonly record struct CircuitActorKey(int Slot, ushort Generation, int TypeId)
    {
        internal bool IsValid => Slot >= 0 && Slot < ActorLoader.MaxActorCount
            && Generation != 0 && TypeId >= 0;

        /// <summary>从活体 Actor 采身份，失败给 default</summary>
        internal static bool TryCapture(Actor actor, out CircuitActorKey key) {
            key = default;
            if (actor == null || !actor.Active || actor.Generation == 0) {
                return false;
            }
            key = new CircuitActorKey(actor.WhoAmI, actor.Generation, actor.ID);
            return key.IsValid;
        }

        /// <summary>按身份反查活体 Actor，代或类型不符视作目标已死</summary>
        internal bool TryResolve(out Actor actor) {
            actor = null;
            if (!IsValid || ActorLoader.Actors == null) {
                return false;
            }
            Actor candidate = ActorLoader.Actors[Slot];
            if (candidate == null || !candidate.Active
                || candidate.Generation != Generation || candidate.ID != TypeId) {
                return false;
            }
            actor = candidate;
            return true;
        }

        /// <summary>定长 10 字节负载；读侧必须无条件吃完再校验</summary>
        internal void Write(BinaryWriter writer) {
            writer.Write(Slot);
            writer.Write(Generation);
            writer.Write(TypeId);
        }

        internal static bool TryRead(BinaryReader reader, out CircuitActorKey key) {
            //先读干净：负载定长，非法值靠 IsValid 拒绝而不是提前 return
            int slot = reader.ReadInt32();
            ushort generation = reader.ReadUInt16();
            int typeId = reader.ReadInt32();
            key = new CircuitActorKey(slot, generation, typeId);
            return key.IsValid;
        }
    }
}
