using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 熔毁协议：锁死散热回路，引信走完后过热炸开并波及近处。<br/>
    /// 首个靠芯片解锁的协议，<see cref="QuickHackDef.UnlockedByDefault"/> 为 false
    /// </summary>
    internal class MeltdownProtocol : QuickHackDef
    {
        //引信帧数与爆发半径
        private const int FuseFrames = 60 * 4;
        private const float BlastRadius = 168f;

        private static readonly Color HeatHot = new(255, 214, 132);
        private static readonly Color HeatCold = new(126, 24, 12);

        public override void SetDefaults() {
            UploadTime = 150;
            RamCost = 5;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.Npc;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => FuseFrames;

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            if (Main.netMode != NetmodeID.Server) EmitIgnite(Main.npc[s.NpcIndex]);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (TryGetNpc(target, out NPC npc)) EmitIgnite(npc);
        }

        //引信点着：一圈余烬从体表窜起
        private static void EmitIgnite(NPC npc) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.2f, 3.2f);
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(npc.Center, vel, HeatHot, 1.1f)
                    ?.Configure(HeatCold, 34);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s) return true;
            if (Main.netMode != NetmodeID.Server) EmitFuse(Main.npc[s.NpcIndex], elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (TryGetNpc(target, out NPC npc)) EmitFuse(npc, elapsed);
        }

        //越接近引爆越密：从零星冒烟烧到通体过热
        private static void EmitFuse(NPC npc, int elapsed) {
            float heat = MathHelper.Clamp(elapsed / (float)FuseFrames, 0f, 1f);
            int interval = heat > 0.8f ? 2 : heat > 0.45f ? 4 : 8;
            if (elapsed % interval != 0) return;

            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                npc.width * 0.42f, npc.height * 0.42f);
            Vector2 vel = new(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.6f, -0.4f));
            PRTLoader.NewParticle<PRT_SHPCThermalEmber>(pos, vel,
                Color.Lerp(HeatCold, HeatHot, heat), 0.6f + heat * 0.7f)
                ?.Configure(HeatCold, 26);
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not NpcScannable s) return;
            NPC npc = Main.npc[s.NpcIndex];
            Vector2 center = npc.Center;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int core = Math.Max(120, (int)(npc.lifeMax * 0.08f));
                npc.SimpleStrikeNPC(core, 0, false, 0f, null, false, 0f, true);
                SplashNearby(center, s.NpcIndex);
            }
            if (Main.netMode != NetmodeID.Server) EmitBlast(center);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (TryGetNpc(target, out NPC npc)) EmitBlast(npc.Center);
        }

        //波及近处：只打能挨打的敌对目标，城镇居民与无敌单位跳过
        private static void SplashNearby(Vector2 center, int sourceIndex) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (i == sourceIndex) continue;
                NPC other = Main.npc[i];
                if (!other.active || other.friendly || other.townNPC
                    || other.dontTakeDamage || other.immortal) {
                    continue;
                }
                if (Vector2.DistanceSquared(other.Center, center) > BlastRadius * BlastRadius) {
                    continue;
                }
                int splash = Math.Max(60, (int)(other.lifeMax * 0.04f));
                other.SimpleStrikeNPC(splash, 0, false, 0f, null, false, 0f, true);
            }
        }

        private static void EmitBlast(Vector2 center) {
            PRTLoader.NewParticle<PRT_MechExplosion>(center, Vector2.Zero, HeatHot, 1.9f)
                ?.Configure(38, new Color(255, 138, 46));
            for (int i = 0; i < 22; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7.5f, 7.5f)
                    * Main.rand.NextFloat(0.35f, 1f);
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(center, vel, HeatHot, 1.35f)
                    ?.Configure(HeatCold, 42);
            }
        }

        private static bool TryGetNpc(IHackTarget target, out NPC npc) {
            npc = null;
            if (target is not NpcScannable s || s.NpcIndex < 0 || s.NpcIndex >= Main.maxNPCs) {
                return false;
            }
            npc = Main.npc[s.NpcIndex];
            return npc.active;
        }
    }
}
