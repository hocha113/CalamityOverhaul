using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal class CWRWorld : ModSystem
    {
        /// <summary>&gt;0 冻结世界活动，每帧减 1</summary>
        public static int TimeFrozenTick;
        /// <summary>世界有 Boss</summary>
        public static bool HasBoss;

        internal static bool BossRush => CWRRef.GetBossRushActive();
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

            //索引无效
            if (!index.TryGetNPC(out var npc)) {
                index = -1;
                return;
            }

            //已死或类型不对
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
            foreach (var info in WorldInfos) {
                info.OnWorldLoad();
            }
        }

        public override void OnWorldUnload() {
            foreach (var info in WorldInfos) {
                info.OnWorldUnLoad();
            }
        }

        public override void PostUpdateProjectiles() {
            if (ModifyCrabulon.mountPlayerHeldProj.TryGetProjectile(out var heldProj) && heldProj.IsOwnedByLocalPlayer()) {
                //持弹相对玩家偏移，绘制矫正
                ModifyCrabulon.mountPlayerHeldPosOffset = Main.LocalPlayer.To(heldProj.Center);
            }
        }

        /// <summary>是否冻结时间</summary>
        public static bool CanTimeFrozen() {
            if (Main.gameMenu) {
                return false;
            }
            if (TimeFreezes.WorldFreezeSystem.IsActive) {
                return true;
            }
            if (Main.LocalPlayer != null && Main.LocalPlayer.active
                && TimeFrozenTick > 0) {
                return true;
            }
            return false;
        }

        public override void PostUpdateEverything() {
            if (TimeFrozenTick > 0) {
                TimeFrozenTick--;
            }

            ChekPrimeArm();

            HasBoss = BossRush;
            if (!HasBoss) {
                foreach (var n in Main.ActiveNPCs) {
                    if (n.boss && !n.friendly) {
                        HasBoss = true;
                        break;
                    }
                    if (n.type == NPCID.EaterofWorldsHead) {//世吞无 boss 标签
                        HasBoss = true;
                        break;
                    }
                }
            }
        }


    }
}
