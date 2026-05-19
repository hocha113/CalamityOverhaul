using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 炮台电路过载：向供电回路灌入反向电流，使炮台长时间彻底失效
    /// 演出用途：在关键阶段让塔防沉默足够久，创造突破窗口
    /// </summary>
    internal class TurretCircuitOverload : QuickHackDef
    {
        //失效帧数（大约12秒）
        private const int DisableFrames = 60 * 12;

        public override void SetDefaults() {
            UploadTime = 180;
            RamCost = 6;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Turret;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableTurret turret) return false;
            //炮台权威状态变更只在施法端执行，远端依靠 Actor 自身的同步链路还原
            if (!HackTimeNetSync.IsRemoteApply) {
                turret.ApplyCircuitOverload(DisableFrames, caster);
            }
            Vector2 center = turret.WorldCenter;

            if (!VaultUtils.isServer) {
                //大范围电浆爆裂：外围紫红色+内核炽白
                for (int i = 0; i < 34; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                    Color c = Color.Lerp(new Color(255, 120, 200), new Color(255, 240, 255), Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.4f).Configure(false, 32);
                }
                //烧毁冒烟般的外圈火花
                for (int i = 0; i < 16; i++) {
                    float angle = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(-0.1f, 0.1f);
                    Vector2 dir = angle.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_Spark>(center + dir * 32f, dir * 5.5f, new Color(220, 60, 140, 180), 0.9f).Configure(false, 30);
                }
            }
            return true;
        }
    }
}
