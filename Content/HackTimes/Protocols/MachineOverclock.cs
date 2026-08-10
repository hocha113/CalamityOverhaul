using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 机械超频：期间把电量按满值托住，机器不会因缺电停摆；
    /// 到期一次性烧空，代价明码标价
    /// </summary>
    internal class MachineOverclock : QuickHackDef
    {
        private static readonly Color Surge = new(255, 230, 120);

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 10;

        public override bool CanApplyTo(IHackTarget target) {
            return base.CanApplyTo(target) && HackTargets.TryMachine(target, out _);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryMachine(target, out MachineTP machine)) return false;
            if (Main.netMode != NetmodeID.Server) EmitSurge(machine.CenterInWorld);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryMachine(target, out MachineTP machine)) {
                EmitSurge(machine.CenterInWorld);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryMachine(target, out MachineTP machine)) return true;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                machine.MachineData.UEvalue = machine.MaxUEValue;
                //每半秒推一次，别逐帧刷网络
                if (Main.netMode == NetmodeID.Server && elapsed % 30 == 0) {
                    machine.SendData();
                }
            }
            if (Main.netMode != NetmodeID.Server) EmitHum(machine.CenterInWorld, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryMachine(target, out MachineTP machine)) {
                EmitHum(machine.CenterInWorld, elapsed);
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryMachine(target, out MachineTP machine)) return;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                machine.MachineData.UEvalue = 0;
                if (Main.netMode == NetmodeID.Server) {
                    machine.SendData();
                }
            }
            if (Main.netMode != NetmodeID.Server) EmitBurnout(machine.CenterInWorld);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryMachine(target, out MachineTP machine)) {
                EmitBurnout(machine.CenterInWorld);
            }
        }

        private static void EmitSurge(Vector2 center) {
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Surge, 1.1f)
                    ?.Configure(false, 22);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.5f }, center);
            }
        }

        //持续期只留一点跳动的电火花，读作机器在超负荷跑
        private static void EmitHum(Vector2 center, int elapsed) {
            if (elapsed % 10 != 0) return;
            Vector2 offset = Main.rand.NextVector2Circular(22f, 22f);
            PRTLoader.NewParticle<PRT_Spark>(center + offset,
                new Vector2(0f, Main.rand.NextFloat(-1.4f, -0.3f)), Surge, 0.6f)
                ?.Configure(false, 16);
        }

        private static void EmitBurnout(Vector2 center) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.4f, 2.4f);
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(center, vel,
                    new Color(180, 90, 40), 0.9f)?.Configure(new Color(60, 20, 10), 30);
            }
        }
    }
}
