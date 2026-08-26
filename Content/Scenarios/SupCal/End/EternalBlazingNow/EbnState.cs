using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    /// <summary>永燃焰狱个人标记同步，类本身即信道</summary>
    internal class EbnState : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => HandleNetSync(reader, whoAmI);

        public static bool OnEbn(Player player)
            => Read(player, d => d.EternalBlazingNow, d => d.EternalBlazingNow);

        public static bool IsConquered(Player player)
            => Read(player, d => d.SupCalYharonQuestReward, d => d.SupCalYharonQuestReward);

        public static void SendEbnSync(Player player, int toWho = -1, int fromWho = -1) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }

            ModPacket packet = CWRNetWork.GetPacket<EbnState>();
            packet.Write((byte)player.whoAmI);
            packet.Write(OnEbn(player));
            packet.Send(toWho, fromWho);
        }

        internal static void HandleNetSync(BinaryReader reader, int whoAmI) {
            int playerIndex = reader.ReadByte();
            bool ebnState = reader.ReadBoolean();

            if (!playerIndex.TryGetPlayer(out Player player)) {
                return;
            }

            Write(player, d => d.EternalBlazingNow = ebnState, d => d.EternalBlazingNow = ebnState);

            if (VaultUtils.isServer) {
                ModPacket packet = CWRNetWork.GetPacket<EbnState>();
                packet.Write((byte)playerIndex);
                packet.Write(ebnState);
                packet.Send(-1, whoAmI);
            }
        }

        private static bool Read(Player player, System.Func<SupCalStoryData, bool> story, System.Func<SupCalStoryData, bool> legacy) {
            if (player?.active == true && story(player.GetModPlayer<StoryPlayer>().Get<SupCalStoryData>())) {
                return true;
            }

            return player?.active == true && legacy(player.GetModPlayer<StoryPlayer>().Get<SupCalStoryData>());
        }

        private static void Write(Player player, System.Action<SupCalStoryData> story, System.Action<SupCalStoryData> legacy) {
            if (player == null) {
                return;
            }

            SupCalStoryData data = player.GetModPlayer<StoryPlayer>().Get<SupCalStoryData>();
            story(data);
            legacy(data);
        }
    }
}
