using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.Narrative.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    /// <summary>
    /// 玩家侧 Boss 击杀登记（十三·#102）：只记「本玩家亲手参与击杀」的 boss 门槛身份类型，
    /// 随玩家存档走、跨世界累计。<see cref="LegendData.SyncTrialProgressFromWorld"/> 静默同步
    /// 只并入登记里有的试炼，世界打过但本玩家没打过的留给玩家自己触发对话/礼物叙事。
    /// 键与 <see cref="KikasaBossGate.IdentityTypeOf"/> 同口径：注册 boss 归并规范类型
    /// （世吞沉哪一节都记头），其余归并组锚点类型，与试炼路线里登记的 NPC 类型直接可比
    /// </summary>
    internal class LegendTrialKillLedgerPlayer : ModPlayer
    {
        //原版类型号跨会话稳定直接存；模组 boss 存 FullName 防类型号漂移，会话内解析缓存
        private readonly HashSet<int> vanillaKills = [];
        private readonly HashSet<string> moddedKills = [];
        private readonly HashSet<int> moddedResolved = [];

        public override void Initialize() {
            vanillaKills.Clear();
            moddedKills.Clear();
            moddedResolved.Clear();
        }

        internal static LegendTrialKillLedgerPlayer TryGet(Player player) {
            if (player == null || !player.TryGetModPlayer(out LegendTrialKillLedgerPlayer ledger)) {
                return null;
            }
            return ledger;
        }

        internal bool HasKilled(int npcType)
            => npcType < NPCID.Count ? vanillaKills.Contains(npcType) : moddedResolved.Contains(npcType);

        /// <summary>入账一笔，返回是否新增（服务器份是会话镜像，兼做重复单播抑制）</summary>
        internal bool Record(int npcType) {
            if (npcType <= NPCID.None || npcType >= NPCLoader.NPCCount) {
                return false;
            }
            if (npcType < NPCID.Count) {
                return vanillaKills.Add(npcType);
            }
            if (NPCLoader.GetNPC(npcType) is ModNPC modNPC && moddedKills.Add(modNPC.FullName)) {
                moddedResolved.Add(npcType);
                return true;
            }
            return false;
        }

        private void RebuildResolved() {
            moddedResolved.Clear();
            foreach (string fullName in moddedKills) {
                //卸了模组的条目静默留存，等模组回来再解析
                if (ModContent.TryFind(fullName, out ModNPC modNPC)) {
                    moddedResolved.Add(modNPC.Type);
                }
            }
        }

        public override void SaveData(TagCompound tag) {
            if (vanillaKills.Count > 0) {
                tag["LegendTrialKills"] = vanillaKills.ToList();
            }
            if (moddedKills.Count > 0) {
                tag["LegendTrialKillNames"] = moddedKills.ToList();
            }
        }

        public override void LoadData(TagCompound tag) {
            Initialize();
            if (tag.TryGet("LegendTrialKills", out List<int> vanilla)) {
                foreach (int type in vanilla) {
                    if (type > NPCID.None && type < NPCID.Count) {
                        vanillaKills.Add(type);
                    }
                }
            }
            if (tag.TryGet("LegendTrialKillNames", out List<string> names)) {
                foreach (string name in names) {
                    if (!string.IsNullOrEmpty(name)) {
                        moddedKills.Add(name);
                    }
                }
            }
            RebuildResolved();
        }
    }

    /// <summary>
    /// 登记的投递信道：击杀判定与 playerInteraction 都只在服务器（或单机本地）可靠，
    /// 服务器逐个单播回参与击杀的客户端，客户端写进自己的登记
    /// </summary>
    internal sealed class LegendTrialKillNet : CWRNetChannel
    {
        internal static void Deliver(int playerIndex, int npcType) {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return;
            }
            //单机直写；服务器写会话镜像（不落盘），新增才单播，世吞逐节死亡这类重复入账不再刷包
            LegendTrialKillLedgerPlayer ledger = LegendTrialKillLedgerPlayer.TryGet(Main.player[playerIndex]);
            if (ledger == null || !ledger.Record(npcType)) {
                return;
            }
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<LegendTrialKillNet>();
            packet.Write(npcType);
            packet.Send(toClient: playerIndex);
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读净载荷再判端，保流对齐
            int npcType = reader.ReadInt32();
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            Main.LocalPlayer.GetModPlayer<LegendTrialKillLedgerPlayer>().Record(npcType);
        }
    }

    /// <summary>
    /// 登记的死亡入口：boss 级死亡时给 playerInteraction 为真的每个玩家记一笔。
    /// 多人客户端本地的 playerInteraction 恒为空（原版 StrikeNPC 只在单机落标记，
    /// 联机由服务器落），所以只在服务器/单机端读取，客户端等单播。
    /// 世吞无 realLife、逐节死亡逐节触发，规范归并后同一玩家只入账一次；
    /// 打过任意一节即算参与，与原版战利品的参与口径一致
    /// </summary>
    internal sealed class LegendTrialKillNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (!KikasaBossGate.IsBossLevel(npc)) {
                return;
            }
            int identity = KikasaBossGate.IdentityTypeOf(npc);
            if (identity <= NPCID.None) {
                return;
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (!npc.playerInteraction[i] || Main.player[i]?.active != true) {
                    continue;
                }
                LegendTrialKillNet.Deliver(i, identity);
            }
        }
    }
}
