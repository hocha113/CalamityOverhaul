using CalamityOverhaul.Content.GameModes.UI;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.Blessings
{
    /// <summary>
    /// 祝福世界档案：修罗（含死神永生态）开启状态下讨伐过的 Boss 集合。
    /// 世界级旗标，随世界存档持久化，进档由 tML 世界数据同步；
    /// 运行时新讨伐由权威端登记后经 <see cref="BlessingUnlockNet"/> 广播各端落地演出
    /// </summary>
    internal class BlessingWorld : ModSystem
    {
        /// <summary>已讨伐（已解锁）的祝福 ID 集</summary>
        internal static readonly HashSet<string> Slain = [];

        /// <summary>最近一次解锁的祝福（本端表现层：HUD 灯焰腾起用）</summary>
        internal static Blessing RecentUnlock { get; private set; }

        /// <summary>最近解锁时刻（GameUpdateCount）</summary>
        internal static uint RecentUnlockTick { get; private set; }

        internal static bool IsUnlocked(Blessing blessing) => blessing != null && Slain.Contains(blessing.ID);

        /// <summary>已解锁总数（只数目录内的，防脏档虚高）</summary>
        internal static int UnlockedCount {
            get {
                int count = 0;
                foreach (Blessing blessing in BlessingRegistry.All) {
                    if (Slain.Contains(blessing.ID)) {
                        count++;
                    }
                }
                return count;
            }
        }

        public override void ClearWorld() {
            Slain.Clear();
            RecentUnlock = null;
            RecentUnlockTick = 0;
        }

        public override void SaveWorldData(TagCompound tag) {
            if (Slain.Count > 0) {
                tag["BlessingSlain"] = new List<string>(Slain);
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            Slain.Clear();
            if (!tag.TryGet("BlessingSlain", out List<string> list) || list == null) {
                return;
            }
            foreach (string id in list) {
                //目录外的键照弃（跨版本删减内容时静默收缩）
                if (BlessingRegistry.TryGet(id, out _)) {
                    Slain.Add(id);
                }
            }
        }

        public override void NetSend(BinaryWriter writer) {
            List<byte> seats = [];
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (Slain.Contains(blessing.ID) && blessing.Seat >= 0 && blessing.Seat <= byte.MaxValue) {
                    seats.Add((byte)blessing.Seat);
                }
            }
            writer.Write((byte)seats.Count);
            foreach (byte seat in seats) {
                writer.Write(seat);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            Slain.Clear();
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++) {
                int seat = reader.ReadByte();
                if (seat < BlessingRegistry.All.Count) {
                    Slain.Add(BlessingRegistry.All[seat].ID);
                }
            }
        }

        /// <summary>
        /// 权威端（服务端/单人）登记一次讨伐；新入档时广播并演出。客户端不得调用
        /// </summary>
        internal static void AuthorityRecord(Blessing blessing) {
            if (blessing == null || VaultUtils.isClient) {
                return;
            }
            if (!Slain.Add(blessing.ID)) {
                return;
            }

            if (VaultUtils.isServer) {
                ModPacket packet = CWRNetWork.GetPacket<BlessingUnlockNet>();
                packet.Write((byte)blessing.Seat);
                packet.Send();
            }
            OnUnlocked(blessing);
        }

        internal static void HandleNet(BinaryReader reader, int whoAmI) {
            int seat = reader.ReadByte();
            //回执只许客户端落地：堵死伪造包直写服务端档案的通道
            if (VaultUtils.isServer) {
                return;
            }
            if (seat >= BlessingRegistry.All.Count) {
                return;
            }
            Blessing blessing = BlessingRegistry.All[seat];
            if (Slain.Add(blessing.ID)) {
                OnUnlocked(blessing);
            }
        }

        /// <summary>解锁瞬间的本端演出：播报一行 + 记下时间戳供 HUD 灯焰腾起（专用服务器无表现层）</summary>
        private static void OnUnlocked(Blessing blessing) {
            if (Main.dedServ) {
                return;
            }
            RecentUnlock = blessing;
            RecentUnlockTick = Main.GameUpdateCount;
            Microsoft.Xna.Framework.Color accent = GameModeTheme.Ember(GameModeSystem.FaceOf(GameModeKind.Asura));
            VaultUtils.Text(BlessingSystemText.UnlockBroadcast.Format(blessing.DisplayName.Value), accent);
        }
    }

    /// <summary>祝福讨伐广播信道：服务端登记后下发席位号，客户端落地并演出</summary>
    internal sealed class BlessingUnlockNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => BlessingWorld.HandleNet(reader, whoAmI);
    }
}
