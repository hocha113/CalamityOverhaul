using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    internal class CWRWorld : ModSystem
    {
        /// <summary>
        /// 是否在进行机械暴乱
        /// </summary>
        internal static bool MachineRebellion;
        /// <summary>
        /// 接下来多少tick的更新里面不能关闭机械暴乱
        /// </summary>
        internal static int DontCloseMachineRebellion;
        /// <summary>
        /// 是否在当前世界击败了机械暴乱
        /// </summary>
        public static bool MachineRebellionDowned;
        /// <summary>
        /// 值大于0时会停止大部分的游戏活动模拟冻结效果，这个值每帧会自动减1
        /// </summary>
        public static int TimeFrozenTick;
        /// <summary>
        /// 当前世界是否存在Boss
        /// </summary>
        public static bool HasBoss;

        internal static bool BossRush => CWRRef.GetBossRushActive() || MachineRebellion;
        internal static bool MasterMode => Main.masterMode || BossRush;
        internal static bool ExpertMode => Main.expertMode || BossRush;
        internal static bool Death => CWRRef.GetDeathMode() || BossRush;
        internal static bool Revenge => CWRRef.GetRevengeMode() || BossRush;

        internal static int primeLaser = -1;
        internal static int primeCannon = -1;
        internal static int primeVice = -1;
        internal static int primeSaw = -1;

        internal static List<IWorldInfo> WorldInfos { get; private set; }

        internal static bool IsAcidRainEventIsOngoing() => CWRRef.GetAcidRainEventIsOngoing();

        public static void CheckNPCIndexByType(ref int index, int npcID) {
            if (index < 0)
                return;

            //若获取失败，NPC无效
            if (!index.TryGetNPC(out var npc)) {
                index = -1;
                return;
            }

            //NPC 已死亡或类型不匹配
            if (!npc.Alives() || npc.type != npcID) {
                index = -1;
                return;
            }
        }

        public static void ChekPrimeArm() {
            CheckNPCIndexByType(ref primeLaser, NPCID.PrimeLaser);
            CheckNPCIndexByType(ref primeCannon, NPCID.PrimeCannon);
            CheckNPCIndexByType(ref primeVice, NPCID.PrimeVice);
            CheckNPCIndexByType(ref primeSaw, NPCID.PrimeSaw);
        }

        public override void Load() {
            VaultUtils.InvasionEvent += CWRRef.GetAcidRainEventIsOngoing;
            WorldInfos = VaultUtils.GetDerivedInstances<IWorldInfo>();
        }

        public override void Unload() {
            VaultUtils.InvasionEvent -= CWRRef.GetAcidRainEventIsOngoing;
        }

        public override void OnWorldLoad() {
            MachineRebellionDowned = false;
            foreach (var info in WorldInfos) {
                info.OnWorldLoad();
            }
        }

        public override void OnWorldUnload() {
            MachineRebellionDowned = false;
            foreach (var info in WorldInfos) {
                info.OnWorldUnLoad();
            }
        }

        public override void PostUpdateProjectiles() {
            if (ModifyCrabulon.mountPlayerHeldProj.TryGetProjectile(out var heldProj) && heldProj.IsOwnedByLocalPlayer()) {
                //这里缓存手持弹幕和玩家的位置差，用于在绘制函数中二次设置进行矫正
                ModifyCrabulon.mountPlayerHeldPosOffset = Main.LocalPlayer.To(heldProj.Center);
            }
        }

        /// <summary>
        /// 用于判断是否应该冻结时间
        /// </summary>
        /// <returns></returns>
        public static bool CanTimeFrozen() {
            if (Main.gameMenu) {
                return false;
            }
            if (Main.LocalPlayer != null && Main.LocalPlayer.active
                && TimeFrozenTick > 0) {
                return true;
            }
            return false;
        }

        public static void StartMetalMusic() {
            if (VaultUtils.isServer) {
                return;
            }

            if (MachineRebellion) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Metal");
                return;
            }

            if (!HasBoss) {
                return;
            }

            if (HeadPrimeAI.DontReform()) {
                return;
            }

            bool found = false;
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.SkeletronPrime) {
                    found = true;
                    break;
                }
                else if (npc.type == NPCID.TheDestroyer) {
                    found = true;
                    break;
                }
                else if (npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism) {
                    found = true;
                    break;
                }
            }
            if (!found) {
                return;
            }

            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Metal");
        }

        public static void UpdateMachineRebellion() {
            if (!MachineRebellion) {
                return;
            }

            NPC.mechQueen = -1;

            bool noBoss = true;
            //在机械暴乱开启下，检测如果全程机械Boss被杀死了后就自动关闭
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.SkeletronPrime) {
                    noBoss = false;
                }
                else if (npc.type == NPCID.Spazmatism) {
                    noBoss = false;
                }
                else if (npc.type == NPCID.Retinazer) {
                    noBoss = false;
                }
                else if (npc.type == NPCID.TheDestroyer) {
                    noBoss = false;
                }
            }

            if (DontCloseMachineRebellion > 0) {
                DontCloseMachineRebellion--;
            }

            if (noBoss && DontCloseMachineRebellion <= 0) {
                MachineRebellion = false;
            }
        }

        public override void PostUpdateEverything() {
            if (TimeFrozenTick > 0) {
                TimeFrozenTick--;
            }

            UpdateMachineRebellion();
            ChekPrimeArm();

            HasBoss = BossRush;
            if (!HasBoss) {
                foreach (var n in Main.ActiveNPCs) {
                    if (n.boss && !n.friendly) {
                        HasBoss = true;
                        break;
                    }
                }
            }

            StartMetalMusic();
        }

        public override void NetSend(BinaryWriter writer) {
            BitsByte flags1 = new BitsByte();
            flags1[0] = MachineRebellionDowned;
            writer.Write(flags1);
        }

        public override void NetReceive(BinaryReader reader) {
            BitsByte flags1 = reader.ReadByte();
            MachineRebellionDowned = flags1[0];
        }

        public override void SaveWorldData(TagCompound tag) {
            tag.Add("_MachineRebellion", MachineRebellionDowned);
        }

        public override void LoadWorldData(TagCompound tag) {
            if (!tag.TryGet("_MachineRebellion", out MachineRebellionDowned)) {
                MachineRebellionDowned = false;
            }
        }
    }
}
