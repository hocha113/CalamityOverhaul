using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 蜂群链接：把目标和它的同群接到一条总线上，打一个，全群分摊。<br/>
    /// 分摊由 <see cref="HackEffectNPCCombat"/> 在受击时结算
    /// </summary>
    internal class SwarmLink : QuickHackDef
    {
        /// <summary>分摊给每个同群成员的伤害比例</summary>
        internal const float SplitRatio = 0.3f;

        private static readonly Color Link = new(255, 140, 220);
        private static readonly List<NPC> groupBuffer = [];

        public override void SetDefaults() {
            UploadTime = 130;
            RamCost = 5;
            Category = QuickHackCategory.Control;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 5;

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitLink(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitLink(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitPulse(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitPulse(npc, elapsed);
        }

        /// <summary>把这次伤害按比例摊给同群其余成员</summary>
        internal static void SplitDamage(NPC root, int damage) {
            int share = (int)(damage * SplitRatio);
            if (share <= 0) return;

            NpcGroupHelper.CollectGroup(root, groupBuffer);
            for (int i = 0; i < groupBuffer.Count; i++) {
                NPC member = groupBuffer[i];
                if (member.whoAmI == root.whoAmI || !member.active
                    || member.dontTakeDamage || member.immortal) {
                    continue;
                }
                member.SimpleStrikeNPC(Math.Max(1, share), 0, false, 0f, null,
                    false, 0f, true);
                if (Main.netMode != NetmodeID.Server) EmitShare(root, member);
            }
            groupBuffer.Clear();
        }

        private static void EmitLink(NPC npc) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.4f, 3.4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Link, 0.9f)
                    ?.Configure(false, 20);
            }
        }

        private static void EmitPulse(NPC npc, int elapsed) {
            if (elapsed % 22 != 0) return;
            PRTLoader.NewParticle<PRT_Spark>(npc.Center, Vector2.Zero, Link, 0.6f)
                ?.Configure(false, 16);
        }

        //沿两点连线铺一串点，让"电流串过去"这件事看得见
        private static void EmitShare(NPC from, NPC to) {
            Vector2 delta = to.Center - from.Center;
            int steps = (int)MathHelper.Clamp(delta.Length() / 28f, 2f, 14f);
            for (int i = 0; i <= steps; i++) {
                Vector2 pos = from.Center + delta * (i / (float)steps);
                PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Link, 0.5f)
                    ?.Configure(false, 12);
            }
        }
    }
}
