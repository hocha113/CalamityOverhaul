using CalamityOverhaul.Content.HackTimes.CircuitNodes;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 信标伪造：信号塔发出假求救信号。生效期间施法者的刷怪率 ÷3、刷怪上限 ×3，
    /// 新刷出的敌怪被改放到塔周 1200–2400px 环带；6400px 内的既有敌怪在 AI 通道里
    /// 被位置伪装指向塔（PreAI 换值、PostAI 无条件还原，Boss 明确豁免）。<br/>
    /// 生成池不动，保留群系语义。招怪计数写在塔的扫描面板上（偏离设计稿的 HUD 读数，
    /// 免开新 HUD 件）。per-effect 状态挂本类静态账，OnRemove / Unload / 切世界清账
    /// </summary>
    internal class BeaconForge : QuickHackDef
    {
        //持续三十秒
        private const int DurationFrames = 1800;
        //既有敌怪的引怪半径 px
        internal const float AttractRange = 6400f;
        //新刷怪落点环带 px
        private const float RingMin = 1200f;
        private const float RingMax = 2400f;

        internal sealed class BeaconState
        {
            public CircuitActorKey TowerKey;
        }

        //施法者 → 信标。一名玩家同时只挂一座假信标，重复施放换塔
        private static readonly Dictionary<int, BeaconState> beacons = [];

        public override void SetDefaults() {
            UploadTime = 180;
            RamCost = 6;
            Category = QuickHackCategory.Contagion;
            SupportedTargets = HackTargetKind.SignalTower;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => DurationFrames;

        public override void Unload() {
            base.Unload();
            beacons.Clear();
        }

        /// <summary>切世界清账</summary>
        internal static void ClearBeacons() => beacons.Clear();

        public override bool CanApplyTo(IHackTarget target) {
            return base.CanApplyTo(target)
                && target is IHackableSignalTower && target is IDistressBeaconTower;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableSignalTower tower
                || target is not IDistressBeaconTower beaconTower) {
                return false;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (!CircuitActorKey.TryCapture(tower.AsActor, out CircuitActorKey key)) {
                    return false;
                }
                int casterIndex = caster?.whoAmI ?? -1;
                if (casterIndex < 0) {
                    return false;
                }
                //旧信标先熄再点新的
                if (beacons.TryGetValue(casterIndex, out BeaconState old)
                    && old.TowerKey.TryResolve(out var oldActor)
                    && oldActor is IDistressBeaconTower oldTower) {
                    oldTower.EndDistressBeacon();
                }
                beaconTower.BeginDistressBeacon(DurationFrames, caster);
                beacons[casterIndex] = new BeaconState { TowerKey = key };
            }

            if (Main.netMode != NetmodeID.Server) {
                EmitForgeBurst(tower.WorldCenter);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            EmitForgeBurst(target.WorldCenter);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return true;
            }
            //塔侧的信标计时是权威账，塔灭了或被别的信标顶掉就收
            return target is IDistressBeaconTower { DistressBeaconActive: true };
        }

        public override void OnRemove(IHackTarget target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (target is IDistressBeaconTower beaconTower) {
                beaconTower.EndDistressBeacon();
            }
            if (target is not IHackableSignalTower tower
                || !CircuitActorKey.TryCapture(tower.AsActor, out CircuitActorKey key)) {
                return;
            }
            //按塔反查持有者清账；塔已经没了就整表扫尾
            int owner = -1;
            foreach (var pair in beacons) {
                if (pair.Value.TowerKey == key) {
                    owner = pair.Key;
                    break;
                }
            }
            if (owner >= 0) {
                beacons.Remove(owner);
            }
        }

        /// <summary>该玩家的活跃信标塔，刷怪钩子与位置伪装共用</summary>
        internal static bool TryGetBeaconTower(int playerIndex, out SignalTowerActor tower) {
            tower = null;
            if (!beacons.TryGetValue(playerIndex, out BeaconState state)) {
                return false;
            }
            if (!state.TowerKey.TryResolve(out var actor)
                || actor is not SignalTowerActor resolved
                || !resolved.DistressBeaconActive) {
                return false;
            }
            tower = resolved;
            return true;
        }

        /// <summary>全部活跃信标塔，spoof 通道用；无分配热路径就不做缓存了</summary>
        internal static void CollectActiveTowers(List<SignalTowerActor> result) {
            result.Clear();
            foreach (var pair in beacons) {
                if (pair.Value.TowerKey.TryResolve(out var actor)
                    && actor is SignalTowerActor tower && tower.DistressBeaconActive) {
                    result.Add(tower);
                }
            }
        }

        internal static bool AnyBeaconActive => beacons.Count > 0;

        private static void EmitForgeBurst(Vector2 center) {
            //三层同心弧向内收，读作"信号在召唤"
            for (int ring = 2; ring >= 0; ring--) {
                float radius = 30f + ring * 22f;
                int count = 10 + ring * 5;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count;
                    Vector2 dir = angle.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_Spark>(center + dir * radius, -dir * (1.2f + ring * 0.8f),
                        new Color(255, 120, 60), 0.85f - ring * 0.15f)?.Configure(false, 26 + ring * 5);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.35f, Volume = 0.8f }, center);
            }
        }
    }

    /// <summary>
    /// 信标伪造的刷怪接线：刷怪率与上限只对持有信标的玩家改写，
    /// 新刷出的敌怪改放到塔周环带（生成包在 NewNPC 的调用方发出，
    /// OnSpawn 里改坐标来得及上车）
    /// </summary>
    internal class BeaconForgeSpawnControl : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!BeaconForge.TryGetBeaconTower(player.whoAmI, out _)) {
                return;
            }
            spawnRate = System.Math.Max(1, spawnRate / 3);
            maxSpawns *= 3;
        }

        public override void OnSpawn(NPC npc, IEntitySource source) {
            if (Main.netMode == NetmodeID.MultiplayerClient || source is not EntitySource_SpawnNPC) {
                return;
            }
            if (!BeaconForge.AnyBeaconActive || !IsRelocatable(npc)) {
                return;
            }
            //挑离生成点最近的信标塔；太远的塔与这次生成无关
            SignalTowerActor best = null;
            float bestDistSq = BeaconForge.AttractRange * 1.5f * (BeaconForge.AttractRange * 1.5f);
            towerBuffer.Clear();
            BeaconForge.CollectActiveTowers(towerBuffer);
            for (int i = 0; i < towerBuffer.Count; i++) {
                float distSq = Vector2.DistanceSquared(towerBuffer[i].Center, npc.Center);
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    best = towerBuffer[i];
                }
            }
            if (best == null) {
                return;
            }
            if (TryRelocateToRing(npc, best.Center)) {
                best.BeaconLureCount++;
                npc.netUpdate = true;
            }
        }

        private static readonly List<SignalTowerActor> towerBuffer = [];

        private static bool IsRelocatable(NPC npc) {
            return npc.active && !npc.boss && !npc.townNPC && !npc.friendly
                && !npc.CountsAsACritter && !npc.SpawnedFromStatue && npc.realLife < 0;
        }

        private static bool TryRelocateToRing(NPC npc, Vector2 towerCenter) {
            const float ringMin = 1200f;
            const float ringMax = 2400f;
            for (int attempt = 0; attempt < 12; attempt++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(ringMin, ringMax);
                Vector2 candidate = towerCenter + angle.ToRotationVector2() * radius;
                int tx = (int)(candidate.X / 16f);
                int ty = (int)(candidate.Y / 16f);
                if (tx < 40 || tx >= Main.maxTilesX - 40 || ty < 40 || ty >= Main.maxTilesY - 40) {
                    continue;
                }
                //飞行怪直接空放，地面怪向下找可站的地
                if (npc.noGravity) {
                    if (!Collision.SolidCollision(candidate - npc.Size / 2f, npc.width, npc.height)) {
                        npc.position = candidate - npc.Size / 2f;
                        return true;
                    }
                    continue;
                }
                for (int drop = 0; drop < 30; drop++) {
                    int yy = ty + drop;
                    if (yy >= Main.maxTilesY - 40) {
                        break;
                    }
                    if (!WorldGen.SolidTile(tx, yy)) {
                        continue;
                    }
                    Vector2 feet = new(tx * 16f + 8f, yy * 16f);
                    Vector2 topLeft = feet - new Vector2(npc.width / 2f, npc.height);
                    if (Collision.SolidCollision(topLeft, npc.width, npc.height)) {
                        break;
                    }
                    npc.position = topLeft;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 信标伪造的位置伪装通道（F1 的本地实现）：PreAI 把目标玩家的 position
    /// 换成塔位，PostAI 按记录无条件还原；tML 在 PreAI 返回什么都会照调 PostAI，
    /// 配对不裂。另挂一道帧末兜底，防第三方钩子把 AI 中途打断
    /// </summary>
    internal class BeaconForgeAiSpoof : GlobalNPC
    {
        //本帧待还原表：npc.whoAmI → 目标玩家原位置
        private static readonly Dictionary<int, (int PlayerIndex, Vector2 Position)> restore = [];

        public override bool PreAI(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !BeaconForge.AnyBeaconActive) {
                return true;
            }
            if (npc?.active != true || npc.boss || npc.friendly || npc.townNPC
                || npc.CountsAsACritter || npc.damage <= 0) {
                return true;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                return true;
            }
            Player targetPlayer = Main.player[npc.target];
            if (targetPlayer?.active != true) {
                return true;
            }
            if (!BeaconForge.TryGetBeaconTower(npc.target, out SignalTowerActor tower)) {
                return true;
            }
            if (Vector2.DistanceSquared(npc.Center, tower.Center)
                > BeaconForge.AttractRange * BeaconForge.AttractRange) {
                return true;
            }

            restore[npc.whoAmI] = (npc.target, targetPlayer.position);
            targetPlayer.position = tower.Center - targetPlayer.Size / 2f;
            return true;
        }

        public override void PostAI(NPC npc) {
            if (restore.Count == 0 || !restore.TryGetValue(npc.whoAmI, out var entry)) {
                return;
            }
            restore.Remove(npc.whoAmI);
            Player player = Main.player[entry.PlayerIndex];
            if (player != null) {
                player.position = entry.Position;
            }
        }

        /// <summary>把没被 PostAI 收走的伪装全部还原并清表；漏还一帧就是把玩家钉在塔位上</summary>
        internal static void FlushLeftovers() {
            if (restore.Count == 0) {
                return;
            }
            foreach (var entry in restore.Values) {
                Player player = Main.player[entry.PlayerIndex];
                if (player != null) {
                    player.position = entry.Position;
                }
            }
            restore.Clear();
        }
    }

    /// <summary>伪装还原的帧末兜底：正常情况下表应当已空，非空即修</summary>
    internal class BeaconForgeSpoofSafety : ModSystem
    {
        public override void PostUpdateNPCs() => BeaconForgeAiSpoof.FlushLeftovers();
    }
}
