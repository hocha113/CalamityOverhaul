using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Machines
{
    //====================================================================
    //渣汽疏泄带喷口驱动(WAVE2-ENVIRONMENTS §6.2):静态喷口表由 SlagVentBelt
    //生成期登记(ZonePass 起头 Reset),挂 DungeonworldMachines.Update 总线逐帧驱动。
    //
    //行为镜像既有机器纪律(DungeonworldMachines):服务端裁决、Cycle 200 帧、
    //槽序错相 41 帧、WakeRange 60 惰性。喷发=生成原版间歇泉火柱弹幕
    //(ProjectileID.GeyserTrap=654,aiStyle 126,原版生成包自带同步,伤害走弹幕);
    //方向常规待游戏内核实,不符则降级 ProjectileID.FlamesTrap(188),
    //再降=只留静态渣池(喷口砖 443 自 1.4.4 起自带踩踏触发,近身仍有原版反馈)。
    //
    //前摇 25 帧嘶气+蒸汽:非专服本地演出(单机全量);联机远端客户端的可见提示
    //由弹幕自身表现兜底——表只在服务端存在,不为前摇造同步包。
    //
    //每帧成本硬帽(实施纪律7):表容量 ≤14(注册端拒超)、遍历零分配、距离早退,
    //远低于既有机器槽遍历。
    //====================================================================
    internal static class DungeonworldZoneVents
    {
        private const int Cycle = 200;          //≈3.33s,与原版喷口冷却同拍
        private const int PhaseStep = 41;       //槽序错相:同窖两口不齐射
        private const int WakeRangeTiles = 60;
        private const int TelegraphLead = 25;
        private const int VentDamage = 40;      //原版间歇泉基准(与活塞40同档)
        private const int MaxVents = 14;

        internal readonly struct VentSlot(Point tile, bool down)
        {
            /// <summary>喷口砖左格 tile 坐标</summary>
            internal readonly Point Tile = tile;
            /// <summary>true=倒装喷口(窖顶朝下喷)</summary>
            internal readonly bool Down = down;
        }

        private static readonly List<VentSlot> _vents = [];
        private static int _tick;

        /// <summary>最近一次喷发的帧号(D 路"渣芯漂魂"联动信号,服务端读;无喷发=int.MinValue)</summary>
        internal static int LastEruptionTick { get; private set; } = int.MinValue;

        /// <summary>喷口表只读视图(生成日志与 QA 用)</summary>
        internal static IReadOnlyList<VentSlot> Vents => _vents;

        internal static void Reset() {
            _vents.Clear();
            _tick = 0;
            LastEruptionTick = int.MinValue;
        }

        /// <summary>生成期登记喷口;超出硬帽拒收(喷口砖保留为纯踩踏触发)</summary>
        internal static bool Register(Point tile, bool down) {
            if (_vents.Count >= MaxVents) {
                CWRMod.Instance.Logger.Warn(
                    $"[ZoneVents] 喷口超每帧成本硬帽{MaxVents},拒收 at ({tile.X},{tile.Y})");
                return false;
            }
            _vents.Add(new VentSlot(tile, down));
            return true;
        }

        //由 DungeonworldMachines.Update 总线调(其 MultiplayerClient 早退在本调用之前);
        //这里再自守一道,不依赖调用方位置
        internal static void Update() {
            if (Main.netMode == NetmodeID.MultiplayerClient || _vents.Count == 0) {
                return;
            }
            _tick++;
            for (int i = 0; i < _vents.Count; i++) {
                int phase = (_tick + i * PhaseStep) % Cycle;
                bool telegraph = phase == Cycle - TelegraphLead;
                if (phase != 0 && !telegraph) {
                    continue;
                }
                VentSlot vent = _vents[i];
                if (!AnyPlayerNear(vent.Tile)) {
                    continue;
                }
                if (telegraph) {
                    Telegraph(vent);
                }
                else {
                    Erupt(vent);
                }
            }
        }

        //前摇:嘶气+蒸汽(运行时表现层随机走 Main.rand,与生成 RNG 严格分离)
        private static void Telegraph(VentSlot vent) {
            if (Main.dedServ) {
                return;
            }
            float dir = vent.Down ? 1f : -1f;
            var mouth = new Vector2(vent.Tile.X * 16f + 16f, vent.Tile.Y * 16f + (vent.Down ? 16f : 0f));
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = 0.45f, MaxInstances = 3 },
                mouth);
            for (int k = 0; k < 6; k++) {
                Dust dust = Dust.NewDustPerfect(
                    mouth + new Vector2(Main.rand.NextFloat(-8f, 8f), dir * 4f),
                    DustID.Smoke, new Vector2(0f, dir * Main.rand.NextFloat(0.6f, 1.6f)), 120,
                    default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
            }
        }

        //喷发:火柱弹幕由服务端生成、原版同步;各端表现随弹幕本地演
        private static void Erupt(VentSlot vent) {
            LastEruptionTick = _tick;
            float dir = vent.Down ? 1f : -1f;
            var origin = new Vector2(vent.Tile.X * 16f + 16f,
                vent.Tile.Y * 16f + (vent.Down ? 24f : -8f));
            Projectile.NewProjectile(new EntitySource_WorldEvent(), origin,
                new Vector2(0f, dir * 8f), ProjectileID.GeyserTrap, VentDamage, 2f, Main.myPlayer);
        }

        private static bool AnyPlayerNear(Point tile) {
            var center = new Vector2(tile.X * 16f + 16f, tile.Y * 16f + 8f);
            float rangeSq = WakeRangeTiles * 16f * (WakeRangeTiles * 16f);
            foreach (Player player in Main.player) {
                if (player.active && !player.dead
                    && Vector2.DistanceSquared(player.Center, center) <= rangeSq) {
                    return true;
                }
            }
            return false;
        }
    }
}
