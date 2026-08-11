using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 机械超频：期间用一份定额电托住机器，不会因缺电停摆；
    /// 到期一次性烧空，代价明码标价
    /// </summary>
    internal class MachineOverclock : QuickHackDef
    {
        /// <summary>单次激活总共补的电，按机器满容量的倍数算</summary>
        private const float BudgetMultiplier = 4f;

        private static readonly Color Surge = new(255, 230, 120);

        //机器左上格 → 本次激活还剩多少电可补。协议实例是单例，per-effect 状态只能外挂。
        //没有这笔账就是每帧把电托满、机器又一直往下游输出，等于一台无限电源
        private static readonly Dictionary<Point16, float> budgets = [];

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 10;

        public override void Unload() {
            base.Unload();
            budgets.Clear();
        }

        /// <summary>切世界时把定额账清空，机器坐标属于上一个世界</summary>
        internal static void ClearBudgets() => budgets.Clear();

        public override bool CanApplyTo(IHackTarget target) {
            return base.CanApplyTo(target) && HackTargets.TryMachine(target, out _);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryMachine(target, out MachineTP machine)) return false;
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                budgets[machine.Position] = Math.Max(0f, machine.MaxUEValue)
                    * BudgetMultiplier;
            }
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
                TopUp(machine);
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
                budgets.Remove(machine.Position);
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

        /// <summary>
        /// 从定额里补到满，补完为止。<br/>
        /// 超频期间机器一直在往下游输出，所以补出去的电是净增发；
        /// 到期把本地缓冲清零也收不回已经送走的那部分。
        /// 定额花光就不再托，不然它和调试用的无限电源没有区别
        /// </summary>
        private static void TopUp(MachineTP machine) {
            float missing = machine.MaxUEValue - machine.MachineData.UEvalue;
            if (missing <= 0f) return;
            if (!budgets.TryGetValue(machine.Position, out float remaining)
                || remaining <= 0f) {
                return;
            }
            float granted = Math.Min(missing, remaining);
            machine.MachineData.UEvalue += granted;
            budgets[machine.Position] = remaining - granted;
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
