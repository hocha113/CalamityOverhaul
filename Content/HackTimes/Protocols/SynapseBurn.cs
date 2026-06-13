using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>突触焚毁，持续热伤害</summary>
    internal class SynapseBurn : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 90;
            RamCost = 3;
            Category = QuickHackCategory.Lethal;
        }

        public override int GetDuration() => 60 * 5; //5秒持续

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            //初始神经脉冲爆发
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(255, 120, 20), 1.2f).Configure(false, 25);
            }
            return true;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s) return true;
            NPC npc = Main.npc[s.NpcIndex];
            //每 15 帧伤害，5 秒共 20 次
            if (elapsed % 15 == 0) {
                int dmg = Math.Max(10, (int)(npc.lifeMax * 0.002f));
                npc.SimpleStrikeNPC(dmg, 0, false, 0f, null, false, 0f, true);
            }
            //持续焚烧粒子
            if (elapsed % 3 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.3f, npc.height * 0.3f);
                Vector2 vel = new(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.5f));
                Color c = Color.Lerp(new Color(255, 80, 0), new Color(255, 200, 50), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, 0.8f).Configure(false, 20);
            }
            return true;
        }
    }
}
