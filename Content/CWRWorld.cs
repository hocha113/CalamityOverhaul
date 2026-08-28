using CalamityOverhaul.Content.GameModes;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using CalamityOverhaul.Content.TimeFreezes;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal class CWRWorld : ModSystem
    {
        /// <summary>世界有 Boss</summary>
        public static bool HasBoss;

        internal static bool BossRush => CWRRef.GetBossRushActive();
        internal static bool MasterMode => Main.masterMode || BossRush;
        internal static bool ExpertMode => Main.expertMode || BossRush;
        /// <summary>残酷世界(终焉之战期间视同开启)</summary>
        internal static bool Brutal => GameModeSystem.BrutalActive || BossRush;
        /// <summary>修罗地狱(终焉之战期间视同开启)</summary>
        internal static bool Asura => GameModeSystem.AsuraActive || BossRush;

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
            return TimeFreezeSystem.IsAnyGlobalFreezeActive;
        }

        //世吞在场闩锁：有货时每帧核销，无货时每 4 帧补扫一次
        private static bool eowAlive;

        public override void PostUpdateEverything() {
            ChekPrimeArm();

            HasBoss = BossRush;
            //原版旗在 NPC 循环后发布、本钩子前新鲜；旗不含 friendly 判定，
            //为真时再按旧谓词确认一遍（仅战斗期，成本有界），空闲世界零扫描
            if (!HasBoss && Main.CurrentFrameFlags.AnyActiveBossNPC) {
                foreach (var n in Main.ActiveNPCs) {
                    if (n.boss && !n.friendly) {
                        HasBoss = true;
                        break;
                    }
                }
            }
            if (!HasBoss) {
                //世吞无 boss 标签、也不在原版危险集合里：单独补扫。
                //闩锁着时每帧核销（死亡当帧感知），无货时降到每 4 帧一次
                if (eowAlive || Main.GameUpdateCount % 4 == 0) {
                    eowAlive = false;
                    foreach (var n in Main.ActiveNPCs) {
                        if (n.type == NPCID.EaterofWorldsHead) {
                            eowAlive = true;
                            break;
                        }
                    }
                }
                HasBoss = eowAlive;
            }
        }


    }
}
