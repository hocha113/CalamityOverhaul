using CalamityOverhaul.Content.HackTimes.CircuitNodes;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.PRT;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 炮台联网：以命中炮台为根，把 3000px 内所有可组网炮台连成一个 mesh
    /// （含已停摆的，联网顺手唤醒它们），全员朝施法者光标齐射，
    /// 共用四十发总弹池；弹池打空或效果结束，全体成员长时间报废——这是代价。<br/>
    /// 共享弹池与成员表是 per-effect 状态，挂在本类静态账上，
    /// OnRemove / Unload / 世界切换（CircuitNodeSpawner.OnWorldUnload）三处清账
    /// </summary>
    internal class TurretMesh : QuickHackDef
    {
        //持续十五秒
        private const int DurationFrames = 900;
        //共享总弹池
        private const int TotalShots = 40;
        //组网半径 px
        private const float LinkRadius = 3000f;
        //解散后成员报废三十秒
        private const int ScrapFrames = 1800;
        //成员射击间隔：原生 78f ÷ 0.6 ≈ 130f
        private const int MemberFireInterval = 130;
        //施法者光标上行节拍（帧）；不走每帧 netUpdate（tml-netcode-pitfalls §9.3）
        private const int AimSyncInterval = 10;

        private sealed class MeshState
        {
            public readonly List<CircuitActorKey> Members = [];
            public readonly List<int> Cooldowns = [];
            public int ShotsLeft = TotalShots;
            public int CasterIndex;
            public Vector2 LastAim;
        }

        //根炮台身份 → mesh 状态。协议实例是单例，per-effect 状态只能外挂
        private static readonly Dictionary<CircuitActorKey, MeshState> meshes = [];

        public override void SetDefaults() {
            UploadTime = 200;
            RamCost = 7;
            Category = QuickHackCategory.Contagion;
            SupportedTargets = HackTargetKind.Turret;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => DurationFrames;

        public override void Unload() {
            base.Unload();
            meshes.Clear();
        }

        /// <summary>切世界清账，成员身份属于上一个世界</summary>
        internal static void ClearMeshes() => meshes.Clear();

        public override bool CanApplyTo(IHackTarget target) {
            //停摆的根也允许——联网本来就会唤醒它
            return base.CanApplyTo(target)
                && target is IHackableTurret && target is IMeshFireTurret;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableTurret rootTurret
                || target is not IMeshFireTurret) {
                return false;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (!CircuitActorKey.TryCapture(rootTurret.AsActor, out CircuitActorKey rootKey)) {
                    return false;
                }
                var state = new MeshState {
                    CasterIndex = caster?.whoAmI ?? -1,
                    LastAim = rootTurret.WorldCenter,
                };

                Vector2 rootCenter = rootTurret.AsActor.Center;
                foreach (Actor actor in ActorLoader.GetActiveActors<Actor>()) {
                    if (actor is not IMeshFireTurret member || actor is not IHackableTurret) {
                        continue;
                    }
                    if (Vector2.DistanceSquared(actor.Center, rootCenter) > LinkRadius * LinkRadius) {
                        continue;
                    }
                    if (!CircuitActorKey.TryCapture(actor, out CircuitActorKey memberKey)) {
                        continue;
                    }
                    member.JoinMesh(rootTurret.AsActor.WhoAmI, DurationFrames);
                    state.Members.Add(memberKey);
                    //错开首发，别让整组同帧齐鸣成一声
                    state.Cooldowns.Add(state.Members.Count * 24 % MemberFireInterval);
                }

                if (state.Members.Count == 0) {
                    return false;
                }
                meshes[rootKey] = state;
            }

            if (Main.netMode != NetmodeID.Server) {
                EmitLinkBurst(target.WorldCenter);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            EmitLinkBurst(target.WorldCenter);
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            //施法者客户端以慢节拍把光标上行给服务器（齐射点跟手的另一半）；
            //旁观客户端在 ResolveEffectCaster 处对不上本机索引，天然静默
            if (Main.netMode != NetmodeID.MultiplayerClient
                || elapsed % AimSyncInterval != 0) {
                return;
            }
            if (target is not IHackableTurret rootTurret
                || !CircuitActorKey.TryCapture(rootTurret.AsActor,
                    out CircuitActorKey rootKey)) {
                return;
            }
            Player caster = HackEffectTracker.ResolveEffectCaster(this, target);
            if (caster == null || caster.whoAmI != Main.myPlayer) {
                return;
            }
            SendAim(rootKey, Main.MouseWorld);
        }

        private static void SendAim(CircuitActorKey rootKey, Vector2 aim) {
            if (!float.IsFinite(aim.X) || !float.IsFinite(aim.Y)) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.TurretMeshAim);
            rootKey.Write(packet);
            packet.Write(aim.X);
            packet.Write(aim.Y);
            packet.Send();
        }

        /// <summary>服务器收施法者上行的齐射瞄准点；非该网施法者的包静默丢弃</summary>
        internal static void HandleAim(BinaryReader reader, int whoAmI) {
            //定长负载先读干净（10 字节 key + 8 字节座标），再做守卫
            bool keyValid = CircuitActorKey.TryRead(reader, out CircuitActorKey rootKey);
            Vector2 aim = new(reader.ReadSingle(), reader.ReadSingle());
            if (Main.netMode != NetmodeID.Server || !keyValid
                || !float.IsFinite(aim.X) || !float.IsFinite(aim.Y)) {
                return;
            }
            if (!meshes.TryGetValue(rootKey, out MeshState state)
                || state.CasterIndex != whoAmI) {
                //网已散或不是这张网的施法者——正常时序里只会是迟到包
                return;
            }
            state.LastAim = aim;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return true;
            }
            if (target is not IHackableTurret rootTurret
                || !CircuitActorKey.TryCapture(rootTurret.AsActor, out CircuitActorKey rootKey)
                || !meshes.TryGetValue(rootKey, out MeshState state)) {
                return false;
            }

            Player caster = ResolveCaster(state.CasterIndex);
            if (caster == null) {
                return false;
            }

            //齐射点＝施法者光标。单人里权威端就是施法者本机，直接读；
            //联机时施法者客户端在 OnReplicatedTick 里慢节拍上行，HandleAim 写进来
            if (state.CasterIndex == Main.myPlayer) {
                state.LastAim = Main.MouseWorld;
            }

            for (int i = 0; i < state.Members.Count; i++) {
                if (!state.Members[i].TryResolve(out Actor actor)
                    || actor is not IMeshFireTurret member) {
                    continue;
                }
                //中途被打瘫的成员不参与齐射，也不许白吃弹池
                if (actor is IHackableTurret { IsCircuitDisabled: true }) {
                    continue;
                }
                member.SetMeshAim(state.LastAim);
                if (state.Cooldowns[i] > 0) {
                    state.Cooldowns[i]--;
                    continue;
                }
                if (state.ShotsLeft <= 0) {
                    continue;
                }
                member.MeshFire(state.LastAim, caster);
                state.ShotsLeft--;
                state.Cooldowns[i] = MemberFireInterval;
            }

            //弹池打空 → mesh 解散，OnRemove 统一报废
            return state.ShotsLeft > 0;
        }

        public override void OnRemove(IHackTarget target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (target is not IHackableTurret rootTurret
                || !CircuitActorKey.TryCapture(rootTurret.AsActor, out CircuitActorKey rootKey)
                || !meshes.TryGetValue(rootKey, out MeshState state)) {
                return;
            }
            meshes.Remove(rootKey);

            Player caster = ResolveCaster(state.CasterIndex);
            for (int i = 0; i < state.Members.Count; i++) {
                if (!state.Members[i].TryResolve(out Actor actor)
                    || actor is not IMeshFireTurret member
                    || actor is not IHackableTurret turret) {
                    continue;
                }
                member.LeaveMesh();
                //齐射把这片区域的火力烧干，用完即报废是明码标价的代价
                turret.ApplyCircuitOverload(ScrapFrames, caster);
                if (!Main.dedServ) {
                    EmitScrapBurst(actor.Center);
                }
            }
        }

        private static Player ResolveCaster(int index) {
            if (index < 0 || index >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[index];
            return player?.active == true && !player.dead ? player : null;
        }

        private static void EmitLinkBurst(Vector2 center) {
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 dir = angle.ToRotationVector2();
                PRTLoader.NewParticle<PRT_Spark>(center + dir * 20f, dir * 6f,
                    new Color(0, 200, 210), 1.0f)?.Configure(false, 26);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item53 with { Pitch = 0.4f, Volume = 0.8f }, center);
            }
        }

        private static void EmitScrapBurst(Vector2 center) {
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f)
                    - new Vector2(0f, 1.5f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel,
                    new Color(90, 110, 140), 0.8f)?.Configure(true, 30);
            }
        }
    }
}
