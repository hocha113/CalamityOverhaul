using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 蜂群链接：把目标和附近的敌人接到一条总线上，打一个，旁边跟着吃。<br/>
    /// 连带由 <see cref="HackEffectNPCCombat"/> 在受击时结算
    /// </summary>
    internal class SwarmLink : QuickHackDef
    {
        /// <summary>连带给每个受体的伤害比例</summary>
        internal const float SplitRatio = 0.3f;
        /// <summary>链接半径（像素）</summary>
        private const float LinkRadius = 420f;
        /// <summary>单次受击最多带几个，没有上限就是一群小怪把帧数按死</summary>
        private const int MaxRecipients = 5;
        //连线火花的每帧预算，多段武器一帧里能进十几次 HitEffect
        private const int SharePartcleBudget = 30;

        private static readonly Color Link = new(255, 140, 220);

        private static ulong shareBudgetFrame = ulong.MaxValue;
        private static int shareBudget;

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

        /// <summary>把这次伤害按比例连带给附近的敌人</summary>
        internal static void SplitDamage(NPC root, int damage) {
            int share = (int)(damage * SplitRatio);
            if (share <= 0) return;

            //共享血池的成员一律排除：打体节等于重复扣同一条命，
            //一条八十节的蠕虫会把每一次命中乘上节数
            int rootAnchor = SharedLifeAnchor(root);
            int taken = 0;
            for (int i = 0; i < Main.maxNPCs && taken < MaxRecipients; i++) {
                NPC member = Main.npc[i];
                if (member.whoAmI == root.whoAmI || !member.active
                    || member.friendly || member.townNPC
                    || member.dontTakeDamage || member.immortal) {
                    continue;
                }
                if (SharedLifeAnchor(member) == rootAnchor) continue;
                if (Vector2.DistanceSquared(member.Center, root.Center)
                    > LinkRadius * LinkRadius) {
                    continue;
                }
                //Boss 不做受体：否则挂一只杂兵就等于给 Boss 白送三成额外伤害
                if (NpcGroupHelper.IsBossTier(member)) continue;

                member.SimpleStrikeNPC(Math.Max(1, share), 0, false, 0f, null,
                    false, 0f, true);
                taken++;
                if (Main.netMode != NetmodeID.Server) EmitShare(root, member);
            }
        }

        //只认 realLife，避免走 GetAnchorIndex 里那条按类型全场找头的分支
        private static int SharedLifeAnchor(NPC npc) {
            int realLife = npc.realLife;
            return realLife >= 0 && realLife < Main.maxNPCs ? realLife : npc.whoAmI;
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
            if (shareBudgetFrame != Main.GameUpdateCount) {
                shareBudgetFrame = Main.GameUpdateCount;
                shareBudget = SharePartcleBudget;
            }
            if (shareBudget <= 0) return;

            Vector2 delta = to.Center - from.Center;
            int steps = (int)MathHelper.Clamp(delta.Length() / 60f, 2f,
                Math.Min(8f, shareBudget));
            shareBudget -= steps + 1;
            for (int i = 0; i <= steps; i++) {
                Vector2 pos = from.Center + delta * (i / (float)steps);
                PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Link, 0.5f)
                    ?.Configure(false, 12);
            }
        }
    }
}
