using CalamityOverhaul.Content.HackTimes.Protocols;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 「目标挨打时」这一个时机的统一分派。<br/>
    /// 强制注销的叠层、数据榨取的回流、蜂群链接的分摊都挂在这里，
    /// 三个协议各自去钩一遍 GlobalNPC 只会让触发顺序变得没法讲清楚。<br/>
    /// <see cref="NPC.HitEffect"/> 在各端都会跑，玩法一律用权威端结算
    /// </summary>
    internal class HackEffectNPCCombat : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>强制注销已叠层数</summary>
        private int exorciseStacks;

        //本类自己造成的伤害会再次进 HitEffect，不闸住就是无限递归
        private static bool resolving;

        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (resolving || hit.Damage <= 0 || !npc.active) return;

            resolving = true;
            try {
                TryLeech(npc, hit.Damage);
                TrySplit(npc, hit.Damage);
                TryStackExorcise(npc);
            } finally {
                resolving = false;
            }
        }

        private static void TryLeech(NPC npc, int damage) {
            ActiveHackEffect effect = HackEffectTracker.GetEffect<DataLeech>(npc.whoAmI);
            if (effect == null) return;
            DataLeech.ApplyLeech(ResolveCaster(effect), npc, damage,
                effect.ActivationId);
        }

        private static void TrySplit(NPC npc, int damage) {
            if (!HackEffectTracker.HasEffect<SwarmLink>(npc.whoAmI)) return;
            SwarmLink.SplitDamage(npc, damage);
        }

        private void TryStackExorcise(NPC npc) {
            ActiveHackEffect effect = HackEffectTracker.GetEffect<Exorcise>(npc.whoAmI);
            if (effect == null) {
                //效果早于叠层结束时把账清掉，免得下一次挂载继承上一轮的层数
                exorciseStacks = 0;
                return;
            }
            if (++exorciseStacks < Exorcise.TriggerStacks) return;

            exorciseStacks = 0;
            Exorcise.Detonate(npc, Exorcise.TriggerStacks);
            //注销已经结算，效果本身没必要再跑完剩下的时长
            HackEffectTracker.RemoveAuthorityEffect(effect.ActivationId, invokeRemove: false);
        }

        private static Player ResolveCaster(ActiveHackEffect effect) {
            int index = effect.CasterIndex;
            if (index < 0 || index >= Main.maxPlayers) return null;
            Player player = Main.player[index];
            return player?.active == true ? player : null;
        }
    }
}
