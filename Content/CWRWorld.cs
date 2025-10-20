using CalamityMod;
using CalamityMod.Events;
using CalamityMod.World;
using CalamityOverhaul.Content.NPCs.Modifys;
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

        internal static bool BossRush => BossRushEvent.BossRushActive || MachineRebellion;
        internal static bool MasterMode => Main.masterMode || BossRush;
        internal static bool ExpertMode => Main.expertMode || BossRush;
        internal static bool Death => CalamityWorld.death || BossRush;
        internal static bool Revenge => CalamityWorld.revenge || BossRush;

        internal static bool IsAcidRainEventIsOngoing() => AcidRainEvent.AcidRainEventIsOngoing;

        public override void Load() {
            VaultUtils.InvasionEvent += IsAcidRainEventIsOngoing;
        }

        public override void Unload() {
            VaultUtils.InvasionEvent -= IsAcidRainEventIsOngoing;
        }

        public override void OnWorldLoad() {
            MachineRebellionDowned = false;
        }

        public override void OnWorldUnload() {
            MachineRebellionDowned = false;
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

        public static void UpdateMachineRebellion() {
            if (!MachineRebellion) {
                return;
            }

            NPC.mechQueen = -1;

            if (!Main.dedServ) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Metal");
            }

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

            HasBoss = false;
            foreach (var n in Main.ActiveNPCs) {
                if (n.boss) {
                    HasBoss = true;
                    break;
                }
            }
        }

        public override void NetSend(BinaryWriter writer) {
            BitsByte flags1 = new BitsByte();
            flags1[0] = DownedBossSystem.downedPrimordialWyrm;
            flags1[1] = MachineRebellionDowned;
            writer.Write(flags1);
            writer.Write(InWorldBossPhase.YharonKillCount);
        }

        public override void NetReceive(BinaryReader reader) {
            BitsByte flags1 = reader.ReadByte();
            DownedBossSystem.downedPrimordialWyrm = flags1[0];
            MachineRebellionDowned = flags1[1];
            InWorldBossPhase.YharonKillCount = reader.ReadInt32();
        }

        public override void SaveWorldData(TagCompound tag) {
            tag.Add("_InWorldBossPhase_YharonKillCount", InWorldBossPhase.YharonKillCount);
            tag.Add("_MachineRebellion", MachineRebellionDowned);
        }

        public override void LoadWorldData(TagCompound tag) {
            if (tag.TryGet("_InWorldBossPhase_YharonKillCount", out int _yharonKillCount)) {
                InWorldBossPhase.YharonKillCount = _yharonKillCount;
            }
            if (!tag.TryGet("_MachineRebellion", out MachineRebellionDowned)) {
                MachineRebellionDowned = false;
            }
        }
    }
}
