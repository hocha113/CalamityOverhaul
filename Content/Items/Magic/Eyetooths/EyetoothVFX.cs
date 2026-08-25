using CalamityOverhaul.Content.Items.Summon.EyekiteStaffs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Eyetooths
{
    /// <summary>泣血瞳牙色板与血液演出，粒子与血迹拖尾复用缚瞳风筝一系</summary>
    internal static class EyetoothVFX
    {
        public static readonly Color BloodDeep = new(42, 7, 9);
        public static readonly Color Blood = new(168, 22, 32);
        public static readonly Color Arterial = new(210, 36, 45);
        public static readonly Color Bone = new(232, 218, 204);

        public const int TrailPoints = 14;

        /// <summary>出手瞬间断根向后甩血</summary>
        public static void LaunchSpit(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 back = -vel.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 3; i++) {
                Vector2 v = back.RotatedByRandom(0.55f) * Main.rand.NextFloat(1.6f, 4.2f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v, i == 0 ? Arterial : Blood
                    , Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26), 0.24f, 0.988f);
            }
        }

        /// <summary>飞行途中从血根撕下的细珠</summary>
        public static void FlightDrip(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = vel.SafeNormalize(Vector2.UnitX);
            Vector2 v = vel * Main.rand.NextFloat(0.12f, 0.3f)
                + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-0.8f, 0.8f);
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos - dir * Main.rand.NextFloat(4f, 10f), v
                , Main.rand.NextBool(3) ? BloodDeep : Blood
                , Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(12, 20), 0.22f, 0.987f);
        }

        /// <summary>首咬入肉，沿入射向溅血</summary>
        public static void BiteSplat(Vector2 pos, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            Vector2 n = dir.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero, Arterial, 0.42f);
            for (int i = 0; i < 6; i++) {
                Vector2 v = n.RotatedByRandom(0.8f) * Main.rand.NextFloat(2.4f, 6.5f);
                v.Y -= Main.rand.NextFloat(0.3f, 1.2f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v
                    , Main.rand.NextBool(4) ? Arterial : Blood
                    , Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(18, 28), 0.3f, 0.986f);
            }
            PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, n.RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 4f)
                , Blood, Main.rand.NextFloat(0.8f, 1.1f))
                ?.Configure(Main.rand.Next(30, 44), 0.42f, 0.99f, stuckLifetime: Main.rand.Next(30, 44));
        }

        /// <summary>崩起拔出，牙尖带出一弧血</summary>
        public static void RipOut(Vector2 pos, Vector2 popDir) {
            if (Main.dedServ) {
                return;
            }
            Vector2 n = popDir.SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < 5; i++) {
                Vector2 v = n.RotatedByRandom(0.55f) * Main.rand.NextFloat(2f, 5.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v
                    , Main.rand.NextBool(3) ? BloodDeep : Blood
                    , Main.rand.NextFloat(0.42f, 0.8f))?.Configure(Main.rand.Next(18, 30), 0.3f, 0.985f);
            }
            PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, n * 0.8f, Color.White
                , Main.rand.NextFloat(0.06f, 0.09f))
                ?.Configure(Main.rand.Next(16, 24), Blood, BloodDeep, 0.012f);
        }

        /// <summary>俯咬命中，动脉血泉上喷加贴渍</summary>
        public static void SlamBurst(Vector2 pos, Vector2 slamDir) {
            if (Main.dedServ) {
                return;
            }
            Vector2 n = -slamDir.SafeNormalize(Vector2.UnitY);
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero, Arterial, 0.62f);
            for (int i = 0; i < 9; i++) {
                Vector2 v = n.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 8.5f);
                v.Y -= Main.rand.NextFloat(0.6f, 2f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v
                    , Main.rand.NextBool(4) ? Arterial : (Main.rand.NextBool(3) ? BloodDeep : Blood)
                    , Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(22, 34), 0.32f, 0.985f);
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, n.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.6f, 4f)
                    , Main.rand.NextBool() ? Blood : BloodDeep, Main.rand.NextFloat(0.9f, 1.3f))
                    ?.Configure(Main.rand.Next(36, 54), 0.44f, 0.988f, stuckLifetime: Main.rand.Next(36, 54));
            }
            PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos + Main.rand.NextVector2Circular(5f, 4f)
                , n * Main.rand.NextFloat(0.5f, 1.2f), Color.White, Main.rand.NextFloat(0.07f, 0.11f))
                ?.Configure(Main.rand.Next(18, 28), Arterial, BloodDeep, 0.011f);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood
                    , n.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.2f, 3f), 100, default, Main.rand.NextFloat(1f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>俯咬落空砸进地面，骨屑加血花</summary>
        public static void TileShatter(Vector2 pos, Vector2 impactVel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 n = -impactVel.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Bone
                    , n.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4.5f), 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
            for (int i = 0; i < 4; i++) {
                Vector2 v = n.RotatedByRandom(0.7f) * Main.rand.NextFloat(1.8f, 4.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v
                    , Main.rand.NextBool(3) ? BloodDeep : Blood
                    , Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(16, 26), 0.32f, 0.985f);
            }
            PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, n * Main.rand.NextFloat(1.2f, 2.6f)
                , BloodDeep, Main.rand.NextFloat(0.8f, 1.1f))
                ?.Configure(Main.rand.Next(30, 46), 0.44f, 0.988f, stuckLifetime: Main.rand.Next(30, 46));
        }

        /// <summary>死亡余痕，沿旧轨迹留几粒回落血珠，活得比牙镖久</summary>
        public static void Residue(Projectile proj) {
            if (Main.dedServ || proj.oldPos == null) {
                return;
            }
            for (int i = 2; i < proj.oldPos.Length; i += 4) {
                if (proj.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 pos = proj.oldPos[i] + proj.Size * 0.5f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(3f, 3f)
                    , proj.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , Main.rand.NextBool(3) ? BloodDeep : Blood
                    , Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22), 0.26f, 0.987f);
            }
        }

        /// <summary>牙创渗血，挂在中招 NPC 身上限频洒珠</summary>
        public static void WoundDrip(NPC npc) {
            if (Main.dedServ) {
                return;
            }
            Vector2 pos = npc.Center + new Vector2(
                Main.rand.NextFloat(-0.4f, 0.4f) * npc.width,
                Main.rand.NextFloat(-0.3f, 0.4f) * npc.height);
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos
                , new Vector2(npc.velocity.X * 0.2f, Main.rand.NextFloat(0.8f, 1.5f))
                , Main.rand.NextBool(3) ? Arterial : Blood
                , Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(18, 28), 0.26f, 0.99f);
        }

        /// <summary>血迹拖尾，复用缚瞳风筝的 EocBloodTrail 通道</summary>
        public static void DrawBloodTrail(Projectile proj, ref Trail trail, Vector2[] points
            , TrailThicknessCalculator width, TrailColorEvaluator color, float heat) {
            EyekiteVFX.FillOldPosTrail(proj, points);
            EyekiteVFX.DrawChargeTrail(ref trail, points, width, color, heat);
        }
    }
}
