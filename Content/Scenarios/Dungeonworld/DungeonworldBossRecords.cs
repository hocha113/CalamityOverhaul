using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld
{
    /// <summary>
    /// 地牢子世界 Boss 击杀记录（跨进入持久，挂玩家存档；世界为回放制不落盘，
    /// 一切进度只能长在 ModPlayer 上）。同时是首杀必掉的判据与后续波次的进度接口。
    /// 联机所有权线（服务器不能写客户端存档面）：
    /// 1) 客户端进世界把自己的记录快照上行（自报值，只用于掉落慷慨度，不构成权限）；
    /// 2) 服务器 OnKill 圈定结算名单（绑定半径内的在场玩家）→ 自增服务器镜像
    ///    （供连杀时的掉落判据即时读取）→ 逐人掷出该 Boss 的印信饰品（首杀必掉/复杀 25%）
    ///    → 广播击杀包；
    /// 3) 名单内客户端各自自增本地 ModPlayer 并随存档落盘。
    /// 镜像 dict 按 whoAmI 键控并存玩家名做二重校验（槽位复用识别），断线清槽。
    /// </summary>
    internal class DungeonworldBossRecords : UndrownedModPlayer
    {
        /// <summary>三座 Boss 共用一张记录表：任一门闩开启即加载（覆写基类的单门判定）</summary>
        public override bool IsLoadingEnabled(Mod mod) => AnyGateEnabled;

        internal static bool AnyGateEnabled => UndrownedGate.Enabled || FoundryOverseerGate.Enabled || DeepGaolWraithGate.Enabled;

        internal const byte BossIdUndrowned = 0;
        internal const byte BossIdOverseer = 1;
        internal const byte BossIdWraith = 2;

        //协议子操作：0=进世界快照上行(客户端→服务器) 1=击杀名单下发(服务器→客户端)
        private const byte OpSnapshot = 0;
        private const byte OpKillAward = 1;

        /// <summary>不溺者击杀数（per-player 持久）</summary>
        internal int undrownedKills;
        /// <summary>铸造监工击杀数（预留）</summary>
        internal int overseerKills;
        /// <summary>深牢怨灵击杀数（per-player 持久）</summary>
        internal int wraithKills;

        //==================== 存档 ====================

        public override void SaveData(TagCompound tag) {
            tag[nameof(undrownedKills)] = undrownedKills;
            tag[nameof(overseerKills)] = overseerKills;
            tag[nameof(wraithKills)] = wraithKills;
        }

        public override void LoadData(TagCompound tag) {
            undrownedKills = tag.TryGet(nameof(undrownedKills), out int a) ? a : 0;
            overseerKills = tag.TryGet(nameof(overseerKills), out int b) ? b : 0;
            wraithKills = tag.TryGet(nameof(wraithKills), out int c) ? c : 0;
        }

        //==================== 服务器镜像（会话态，per-player 按 whoAmI 键控，非 static 单值）====================

        private struct MirrorSlot
        {
            internal bool Known;
            internal string Name;
            internal int UndrownedKills;
            internal int OverseerKills;
            internal int WraithKills;
        }

        private static readonly MirrorSlot[] serverMirror = new MirrorSlot[Main.maxPlayers + 1];

        internal static void ResetServerMirror() {
            for (int i = 0; i < serverMirror.Length; i++) {
                serverMirror[i] = default;
            }
        }

        /// <summary>读服务器镜像里的指定 Boss 击杀数；未上行过按 0（首杀慷慨侧失败）</summary>
        private static int MirrorKills(Player player, byte bossId) {
            MirrorSlot slot = serverMirror[player.whoAmI];
            //槽位复用识别：名字对不上视为新玩家
            if (!slot.Known || slot.Name != player.name) {
                return 0;
            }
            return bossId switch {
                BossIdUndrowned => slot.UndrownedKills,
                BossIdOverseer => slot.OverseerKills,
                _ => slot.WraithKills,
            };
        }

        private static void MirrorBump(Player player, byte bossId) {
            ref MirrorSlot slot = ref serverMirror[player.whoAmI];
            if (!slot.Known || slot.Name != player.name) {
                slot = new MirrorSlot { Known = true, Name = player.name };
            }
            if (bossId == BossIdUndrowned) {
                slot.UndrownedKills++;
            }
            else if (bossId == BossIdOverseer) {
                slot.OverseerKills++;
            }
            else {
                slot.WraithKills++;
            }
        }

        //==================== 进出世界：快照上行 / 断线清槽 ====================

        public override void OnEnterWorld() {
            //本地端钩子：单人直接写自己的镜像，联机上行快照
            if (Main.netMode == NetmodeID.SinglePlayer) {
                serverMirror[Player.whoAmI] = new MirrorSlot {
                    Known = true,
                    Name = Player.name,
                    UndrownedKills = undrownedKills,
                    OverseerKills = overseerKills,
                    WraithKills = wraithKills,
                };
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                ModPacket packet = CWRNetWork.GetPacket<DungeonworldBossKillNet>();
                packet.Write(OpSnapshot);
                packet.Write((byte)Player.whoAmI);
                packet.Write(undrownedKills);
                packet.Write(overseerKills);
                packet.Write(wraithKills);
                packet.Send();
            }
        }

        public override void PlayerDisconnect() {
            if (!VaultUtils.isClient) {
                serverMirror[Player.whoAmI] = default;
            }
        }

        //==================== 网络协议（读净 payload 再校验，1.1 纪律）====================

        internal static void HandlePacket(BinaryReader reader, int whoAmI) {
            byte op = reader.ReadByte();
            if (op == OpSnapshot) {
                //客户端→服务器：进世界记录快照（自报槽位仅为对齐字节，身份以连线槽位为准）
                _ = reader.ReadByte();
                int undrowned = reader.ReadInt32();
                int overseer = reader.ReadInt32();
                int wraith = reader.ReadInt32();
                //字节读净后再校验（门闩关闭端照读照弃）
                if (!AnyGateEnabled || Main.netMode != NetmodeID.Server) {
                    return;
                }
                if (whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active) {
                    return;
                }
                serverMirror[whoAmI] = new MirrorSlot {
                    Known = true,
                    Name = Main.player[whoAmI].name,
                    //自报值只影响掉落慷慨度：钳制到合理域
                    UndrownedKills = Utils.Clamp(undrowned, 0, 100000),
                    OverseerKills = Utils.Clamp(overseer, 0, 100000),
                    WraithKills = Utils.Clamp(wraith, 0, 100000),
                };
                return;
            }
            if (op == OpKillAward) {
                //服务器→客户端：击杀名单
                byte bossId = reader.ReadByte();
                int count = reader.ReadByte();
                bool selfIncluded = false;
                for (int i = 0; i < count; i++) {
                    int who = reader.ReadByte();
                    if (who == Main.myPlayer) {
                        selfIncluded = true;
                    }
                }
                //字节读净后再校验（门闩关闭端照读照弃）
                if (!AnyGateEnabled || Main.netMode != NetmodeID.MultiplayerClient || !selfIncluded) {
                    return;
                }
                DungeonworldBossRecords records = Main.LocalPlayer.GetModPlayer<DungeonworldBossRecords>();
                if (bossId == BossIdUndrowned) {
                    records.undrownedKills++;
                }
                else if (bossId == BossIdOverseer) {
                    records.overseerKills++;
                }
                else if (bossId == BossIdWraith) {
                    records.wraithKills++;
                }
            }
        }

        //==================== 击杀结算（服务器 OnKill 专用）====================

        /// <summary>
        /// 圈定名单 → 逐人掷出该 Boss 的印信饰品（首杀必掉/复杀 25%，掷在 lootPos）→
        /// 服务器镜像自增 → 广播名单包。单人路径全部就地结算。三座 Boss 共用本口，
        /// charmType 由各自 OnKill 传入（不溺者=沉锚镣环，监工=验工印章，怨灵=锈蚀的镣铐）。
        /// </summary>
        internal static void ServerSettleKill(byte bossId, NPC npc, Vector2 lootPos, int charmType) {
            if (VaultUtils.isClient) {
                return;
            }
            Player[] roster = new Player[Main.maxPlayers];
            int rosterCount = 0;
            foreach (Player player in Main.ActivePlayers) {
                //在场即计入（含刚战死者：他打了这场仗）
                if (Vector2.Distance(player.Center, npc.Center) < FloodGalleryWatcherBind()) {
                    roster[rosterCount++] = player;
                }
            }

            for (int i = 0; i < rosterCount; i++) {
                Player player = roster[i];
                bool firstKill = MirrorKills(player, bossId) <= 0;
                //首杀必掉；复杀 25%（服务器裁决掷骰，结果以实体掉落落地）
                if (firstKill || Main.rand.NextBool(4)) {
                    Item.NewItem(npc.GetSource_FromAI(), (int)lootPos.X, (int)lootPos.Y - 16, 16, 16, charmType);
                }
                MirrorBump(player, bossId);
            }

            if (Main.netMode == NetmodeID.Server && rosterCount > 0) {
                ModPacket packet = CWRNetWork.GetPacket<DungeonworldBossKillNet>();
                packet.Write(OpKillAward);
                packet.Write(bossId);
                packet.Write((byte)rosterCount);
                for (int i = 0; i < rosterCount; i++) {
                    packet.Write((byte)roster[i].whoAmI);
                }
                packet.Send();
            }
            else if (Main.netMode == NetmodeID.SinglePlayer && rosterCount > 0) {
                //单人：镜像已自增，这里同步落到存档面
                DungeonworldBossRecords records = Main.LocalPlayer.GetModPlayer<DungeonworldBossRecords>();
                if (bossId == BossIdUndrowned) {
                    records.undrownedKills++;
                }
                else if (bossId == BossIdOverseer) {
                    records.overseerKills++;
                }
                else if (bossId == BossIdWraith) {
                    records.wraithKills++;
                }
            }
        }

        /// <summary>结算绑定半径（沿看守口径；避免直接引用生成类型形成环）</summary>
        private static float FloodGalleryWatcherBind()
            => Gen.BossRooms.FloodGalleryWatcher.RoomBindDistance;
    }

    /// <summary>地牢子世界 Boss 击杀记录信道：快照上行与名单下发共用一条通道</summary>
    internal sealed class DungeonworldBossKillNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => DungeonworldBossRecords.HandlePacket(reader, whoAmI);
    }
}
