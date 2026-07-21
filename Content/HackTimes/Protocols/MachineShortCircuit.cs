using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>机械短路，清空电能并电弧伤害</summary>
    internal class MachineShortCircuit : QuickHackDef
    {
        //电弧半径（像素）
        private const float ArcRadius = 160f;
        private const int ArcDamage = 40;

        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 3;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not TileScannable s) return false;
            return TryGetMachine(s.TileCoordX, s.TileCoordY, out _);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not TileScannable s) return false;
            if (!TryGetMachine(s.TileCoordX, s.TileCoordY, out MachineTP machine)) return false;

            Vector2 center = machine.CenterInWorld;

            //本端清电能+伤害+TileSquare，远端仅视觉
            if (!HackTimeNetSync.IsRemoteApply) {
                machine.MachineData.UEvalue = 0;

                if (!VaultUtils.isClient) {
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC npc = Main.npc[i];
                        if (!npc.active) continue;
                        if (Vector2.Distance(npc.Center, center) > ArcRadius) continue;
                        npc.StrikeNPC(new NPC.HitInfo {
                            Damage = ArcDamage,
                            Knockback = 4f,
                            HitDirection = npc.Center.X > center.X ? 1 : -1,
                            Crit = false,
                        });
                    }
                }

                if (Main.netMode != NetmodeID.SinglePlayer) {
                    int tileW = machine.Width / 16;
                    int tileH = machine.Height / 16;
                    NetMessage.SendTileSquare(-1, machine.Position.X, machine.Position.Y, tileW, tileH);
                }
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                    Color c = Color.Lerp(new Color(100, 180, 255), new Color(200, 220, 255), Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.2f).Configure(false, 25);
                }
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12f;
                    Vector2 dir = angle.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_Spark>(center + dir * 20f, dir * 4f, new Color(80, 200, 255, 120), 0.6f).Configure(false, 20);
                }

                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.7f, Pitch = -0.3f }, center);
            }

            return true;
        }

        private static bool TryGetMachine(int tileX, int tileY, out MachineTP machine) {
            machine = null;
            if (!VaultUtils.SafeGetTopLeft(tileX, tileY, out var topLeft)) return false;
            if (!TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out var tp)) return false;
            if (tp is not MachineTP m || !tp.Active) return false;
            machine = m;
            return true;
        }
    }
}
