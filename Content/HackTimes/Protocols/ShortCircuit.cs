using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 短路：释放电磁脉冲造成即时伤害并短暂麻痹
    /// </summary>
    internal class ShortCircuit : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 60;
            RamCost = 2;
            Category = QuickHackCategory.Lethal;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            //权威性伤害仅在施法端执行，远端复刻只播放视觉/听觉效果，避免重复扣血
            if (!HackTimeNetSync.IsRemoteApply) {
                //即时重击
                int dmg = Math.Max(30, (int)(npc.lifeMax * 0.02f));
                npc.SimpleStrikeNPC(dmg, 0, false, 0f, null, false, 0f, true);
            }
            //电弧爆发粒子
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(100, 180, 255), 1.5f).Configure(false, 15);
            }
            //内层白色核心闪光
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Color.White, 2.0f).Configure(false, 8);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.ShortCircuit, npc.Center);
            }
            return true;
        }
    }
}
