using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>赛博精神病，狂暴攻击周围单位</summary>
    internal class Cyberpsychosis : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 150;
            RamCost = 5;
            Category = QuickHackCategory.Control;
        }

        public override int GetDuration() => 60 * 8; //8秒持续

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            //红色精神崩溃爆发
            EmitBurstParticles(npc);
            CombatText.NewText(npc.Hitbox, new Color(255, 0, 50), HackTime.Cyberpsychosis.Value, true);
            //群组扩散仅施法端，防远端重复 OnTick 伤害
            if (!HackTimeNetSync.IsRemoteApply) {
                //扩散到群组，HasEffect 短路防无限传播
                HackEffectTracker.PropagateNpcEffectToGroup(this, s.NpcIndex,
                    caster?.whoAmI ?? Main.myPlayer, EmitBurstParticles);
            }
            return true;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s) return true;
            NPC npc = Main.npc[s.NpcIndex];
            //周期性红色故障粒子
            if (elapsed % 10 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.4f, npc.height * 0.4f);
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(255, 50, 50), 0.6f).Configure(false, 15);
            }
            return true;
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not NpcScannable s) return;
            NPC npc = Main.npc[s.NpcIndex];
            //精神恢复闪光
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(180, 80, 80), 0.5f).Configure(false, 15);
            }
        }

        //爆发粒子，群组成员复用
        private static void EmitBurstParticles(NPC npc) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(255, 30, 30), 1.0f).Configure(false, 30);
            }
        }
    }
}
