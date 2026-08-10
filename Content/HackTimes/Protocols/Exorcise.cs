using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 强制注销：给目标挂上注销标记，每挨一次打叠一层，
    /// 叠满就把它整个从名册里划掉。<br/>
    /// 叠层由 <see cref="HackEffectNPCCombat"/> 在受击时记，协议本身只管演出
    /// </summary>
    internal class Exorcise : QuickHackDef
    {
        /// <summary>触发注销所需层数</summary>
        internal const int TriggerStacks = 5;

        private static readonly Color Pale = new(210, 190, 255);

        public override void SetDefaults() {
            UploadTime = 110;
            RamCost = 4;
            Category = QuickHackCategory.Paranormal;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 5;

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitMark(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitMark(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitDrift(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitDrift(npc, elapsed);
        }

        /// <summary>叠满后的一次性注销，层数越多打得越重</summary>
        internal static void Detonate(NPC npc, int stacks) {
            int damage = Math.Max(90, (int)(npc.lifeMax * 0.015f * stacks));
            npc.SimpleStrikeNPC(damage, 0, false, 0f, null, false, 0f, true);
            if (Main.netMode != NetmodeID.Server) EmitErase(npc);
        }

        private static void EmitMark(NPC npc) {
            //环身一圈冷白点，像被谁在名册上圈了个记号
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 offset = angle.ToRotationVector2()
                    * new Vector2(npc.width * 0.6f, npc.height * 0.6f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + offset,
                    Vector2.Zero, Pale, 0.7f)?.Configure(false, 24);
            }
        }

        private static void EmitDrift(NPC npc, int elapsed) {
            if (elapsed % 16 != 0) return;
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                npc.width * 0.5f, npc.height * 0.5f);
            PRTLoader.NewParticle<PRT_Spark>(pos,
                new Vector2(0f, Main.rand.NextFloat(-0.9f, -0.2f)), Pale, 0.45f)
                ?.Configure(false, 26);
        }

        private static void EmitErase(NPC npc) {
            for (int i = 0; i < 24; i++) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(
                    npc.width * 0.7f + 12f, npc.height * 0.7f + 12f);
                //向心收束，读作被抹掉而不是被炸开
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + offset,
                    -offset * 0.12f, Pale, 1.1f)?.Configure(false, 20);
            }
            PRTLoader.NewParticle<PRT_Spark>(npc.Center, Vector2.Zero,
                Color.White, 2.2f)?.Configure(false, 12);
            CombatText.NewText(npc.Hitbox, Pale, HackTime.Erased.Value, true);
        }
    }
}
