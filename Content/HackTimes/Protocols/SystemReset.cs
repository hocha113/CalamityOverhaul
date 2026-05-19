using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 系统重启：强制目标系统重启导致长时间晕眩
    /// </summary>
    internal class SystemReset : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 4;
            Category = QuickHackCategory.Control;
        }

        public override int GetDuration() => 60 * 6; //6秒晕眩

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            EmitApplyParticles(npc);
            CombatText.NewText(npc.Hitbox, new Color(40, 150, 255), HackTime.Rebooting.Value, true);
            //群组扩散涉及向 HackEffectTracker 注册新效果，仅在施法端进行
            if (!HackTimeNetSync.IsRemoteApply) {
                //群组扩散，蠕虫各体节、月总各实体一并被强制重启
                HackEffectTracker.PropagateNpcEffectToGroup(this, s.NpcIndex,
                    caster?.whoAmI ?? Main.myPlayer, EmitApplyParticles);
            }
            return true;
        }

        //初始蓝屏粒子，抽出复用给群组成员
        private static void EmitApplyParticles(NPC npc) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(40, 120, 255), 1.0f).Configure(false, 25);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s) return true;
            NPC npc = Main.npc[s.NpcIndex];
            //向上飘散的数据流粒子
            if (elapsed % 8 == 0) {
                Vector2 pos = new(
                    npc.Center.X + Main.rand.NextFloat(-npc.width * 0.4f, npc.width * 0.4f),
                    npc.position.Y + npc.height);
                Vector2 vel = new(0, Main.rand.NextFloat(-2f, -0.5f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(40, 100, 255), 0.5f).Configure(false, 30);
            }
            //偶尔闪烁蓝色光点
            if (elapsed % 30 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.3f, npc.height * 0.3f), Vector2.Zero, new Color(80, 160, 255), 1.2f).Configure(false, 10);
            }
            return true;
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not NpcScannable s) return;
            NPC npc = Main.npc[s.NpcIndex];
            //重启完成闪光
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.5f, 2.5f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(100, 200, 255), 0.8f).Configure(false, 12);
            }
            CombatText.NewText(npc.Hitbox, new Color(100, 200, 255), HackTime.SystemOnline.Value, false);
        }
    }
}
