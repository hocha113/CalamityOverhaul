using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 路标残句。二幕起、未续签时城镇闲聊 1/7 换矿道怪谈
    /// </summary>
    internal sealed class GhostHandRumors : GlobalNPC
    {
        public override void GetChat(NPC npc, ref string chat) {
            //上线闸关不放传闻
            if (!WraithDirector.CanonContentActive) {
                return;
            }
            if (!npc.townNPC || !WraithActs.ActTwo || !Main.rand.NextBool(7)) {
                return;
            }
            //已认主不再听闻
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
