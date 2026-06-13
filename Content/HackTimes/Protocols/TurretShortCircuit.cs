using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>炮台短路，短暂停摆</summary>
    internal class TurretShortCircuit : QuickHackDef
    {
        //失效帧数（大约4秒）
        private const int DisableFrames = 60 * 4;

        public override void SetDefaults() {
            UploadTime = 90;
            RamCost = 3;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Turret;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableTurret turret) return false;
            //炮台权威状态仅施法端
            if (!HackTimeNetSync.IsRemoteApply) {
                turret.ApplyShortCircuit(DisableFrames, caster);
            }
            Vector2 center = turret.WorldCenter;

            if (!VaultUtils.isServer) {
                //电弧火花爆闪
                for (int i = 0; i < 22; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    Color c = Color.Lerp(new Color(120, 200, 255), new Color(220, 240, 255), Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.0f).Configure(false, 22);
                }
                //沿外圈再撒一圈冷色粒子
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f;
                    Vector2 dir = angle.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_Spark>(center + dir * 24f, dir * 3.5f, new Color(90, 180, 255, 120), 0.55f).Configure(false, 22);
                }
            }
            return true;
        }
    }
}
