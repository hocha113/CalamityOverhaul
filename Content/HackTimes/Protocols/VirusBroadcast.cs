using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>病毒广播，信号塔电磁冲击波短路炮台</summary>
    internal class VirusBroadcast : QuickHackDef
    {
        //广播半径 px，覆盖零号站点上下层
        private const float BroadcastRadiusPx = 6400f;
        //波前扩散帧长
        private const int BroadcastLifeFrames = 150;
        //命中炮台短路帧数，约 20 秒
        private const int TurretDisableFrames = 60 * 20;

        public override void SetDefaults() {
            UploadTime = 240;
            RamCost = 8;
            Category = QuickHackCategory.Contagion;
            SupportedTargets = HackTargetKind.SignalTower;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableSignalTower tower) return false;
            //信号塔广播仅施法端，远端靠 Actor 同步
            if (!HackTimeNetSync.IsRemoteApply) {
                tower.BeginVirusBroadcast(BroadcastRadiusPx, TurretDisableFrames, caster);
            }
            Vector2 center = tower.WorldCenter;

            if (!VaultUtils.isServer) {
                //塔身附近的启动脉冲粒子：紫粉色核心 + 冷蓝外框
                for (int i = 0; i < 28; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    Color c = Color.Lerp(new Color(200, 80, 255), new Color(255, 200, 255), Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.2f).Configure(false, 30);
                }
                for (int i = 0; i < 18; i++) {
                    float angle = MathHelper.TwoPi * i / 18f;
                    Vector2 dir = angle.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_Spark>(center + dir * 28f, dir * 4.5f, new Color(140, 200, 255, 150), 0.75f).Configure(false, 26);
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = 0.1f }, center);
            }
            //波前 Actor 由 BeginVirusBroadcast 内部 Spawn
            return true;
        }

        //仅允许在信号塔未处于广播冷却时使用：协议本身不做限制，由目标自身的状态决定
    }
}
