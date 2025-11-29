using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Sulfseas.OldDukes
{
    internal class OldDukeEffect : ModSystem
    {
        public static bool IsActive;
        public static int ActiveTimer;

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeEffect);
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.OldDukeEffect) {
                IsActive = reader.ReadBoolean();
                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.OldDukeEffect);
                    packet.Write(IsActive);
                    packet.Send(-1, whoAmI);
                }
            }
        }

        public override void PostUpdateEverything() {
            if (IsActive) {
                ActiveTimer++;
                
                //播放硫磺海音乐
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityModMusic/Sounds/Music/AcidRainTier1");

                //超时保护（3分钟）
                if (ActiveTimer > 60 * 60 * 3) {
                    IsActive = false;
                    ActiveTimer = 0;
                }
            }
            else {
                ActiveTimer = 0;
            }
        }

        public override void Unload() {
            IsActive = false;
            ActiveTimer = 0;
        }
    }
}
