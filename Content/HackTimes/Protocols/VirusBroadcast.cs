using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 病毒广播：向信号塔注入蠕虫代码，令其向四周发射一道赛博电磁冲击波
    /// 广播半径极大，覆盖范围内的所有可骇入炮台会被长时间短路
    /// 演出重心在于波前扩散的视觉冲击，协议本身由信号塔触发扩散Actor
    /// </summary>
    internal class VirusBroadcast : QuickHackDef
    {
        //广播半径（px），保证能覆盖整个零号站点上下层
        private const float BroadcastRadiusPx = 6400f;
        //广播扩散帧长，即波前从0扩到满半径的时间
        private const int BroadcastLifeFrames = 150;
        //被命中炮台的短路帧数（约20秒）
        private const int TurretDisableFrames = 60 * 20;

        public override void SetDefaults() {
            UploadTime = 240;
            RamCost = 8;
            Category = QuickHackCategory.Contagion;
            SupportedTargets = HackTargetKind.SignalTower;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableSignalTower tower) return false;
            //信号塔权威广播只在施法端发起，远端依靠 Actor 自身的同步链路还原
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
            //具体的波前Actor由BeginVirusBroadcast内部负责Spawn（服务器或单机），
            //避免客户端提前猜测半径/生命周期
            return true;
        }

        //仅允许在信号塔未处于广播冷却时使用：协议本身不做限制，由目标自身的状态决定
    }
}
