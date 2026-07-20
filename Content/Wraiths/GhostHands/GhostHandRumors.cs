using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 路标残句（公平"可先学"的第一渠道，依鬼律 10/14）：二幕起、本地玩家未与焦黑枯手
    /// 续签契约时，城镇闲聊以 1/7 概率替换为矿道怪谈。权重 3/4 双倍：
    /// 致死禁忌（别让它碰到你）与火阀门（火只救一回）优先曝光
    /// </summary>
    internal sealed class GhostHandRumors : GlobalNPC
    {
        public override void GetChat(NPC npc, ref string chat) {
            //上线闸:系统未开放期间不放传闻——路标不该指向永不活化的据点
            if (!WraithDirector.CanonContentActive) {
                return;
            }
            if (!npc.townNPC || !WraithActs.ActTwo || !Main.rand.NextBool(7)) {
                return;
            }
            //已认主者不再听闻(它已经在簿上认了这只手);无刀/未续签照常
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(Main.LocalPlayer);
            if (vessel.IsValid && vessel.Store.TryGet(nameof(GhostHand), out WraithProgressRecord record) && record.PactRenewed) {
                return;
            }
            if (GhostHand.Rumor1 == null) {
                return;
            }
            WeightedRandom<string> pool = new(Main.rand);
            pool.Add(GhostHand.Rumor1.Value, 1);
            pool.Add(GhostHand.Rumor2.Value, 1);
            pool.Add(GhostHand.Rumor3.Value, 2);
            pool.Add(GhostHand.Rumor4.Value, 2);
            pool.Add(GhostHand.Rumor5.Value, 1);
            chat = pool.Get();
        }
    }
}
