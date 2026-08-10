using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 数据榨取：在目标身上开一道回流口，打它就回 RAM。<br/>
    /// 回流由 <see cref="HackEffectNPCCombat"/> 在受击时结算
    /// </summary>
    internal class DataLeech : QuickHackDef
    {
        /// <summary>每点伤害折算的 RAM</summary>
        internal const float LeechPerDamage = 0.004f;
        /// <summary>单次受击回流上限，免得一发大招把 RAM 直接灌满</summary>
        internal const float LeechCap = 1.2f;

        private static readonly Color Siphon = new(120, 255, 180);

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.Contagion;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 6;

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitTap(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitTap(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitIdle(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitIdle(npc, elapsed);
        }

        /// <summary>把这次伤害折算成 RAM 还给施法者</summary>
        internal static void ApplyLeech(Player caster, NPC npc, int damage) {
            if (caster == null || damage <= 0) return;
            float amount = MathHelper.Min(damage * LeechPerDamage, LeechCap);
            if (amount <= 0f) return;
            RamSystem.Restore(caster, amount, out _);
            if (Main.netMode != NetmodeID.Server) EmitDrain(npc, caster);
        }

        private static void EmitTap(NPC npc) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.6f, 2.6f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Siphon, 0.9f)
                    ?.Configure(false, 20);
            }
        }

        private static void EmitIdle(NPC npc, int elapsed) {
            if (elapsed % 20 != 0) return;
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                npc.width * 0.45f, npc.height * 0.45f);
            PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Siphon, 0.4f)
                ?.Configure(false, 18);
        }

        //回流朝施法者飞，看得见钱从哪儿来
        private static void EmitDrain(NPC npc, Player caster) {
            Vector2 toCaster = (caster.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = toCaster.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Siphon, 0.8f)
                    ?.Configure(false, 16);
            }
        }
    }
}
