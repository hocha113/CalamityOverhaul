using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary>
    /// 通用的世界时间冻结系统（reason 标签计数）
    /// <list type="bullet">
    ///   <item>提供 NPC / 弹幕 / 液体 / 玩家装备的统一暂停语义，
    ///     供骇客时间、义体雷达等上层系统按需调用</item>
    ///   <item>通过 <see cref="Activate(string)"/> / <see cref="Deactivate(string)"/> 以 reason 计数，
    ///     任一活跃 reason 都保持冻结状态；全部释放后才真正解冻</item>
    ///   <item>多人模式下不做强冻结（仅由调用方借助 <see cref="AllowFreeze"/> 提前判定），
    ///     避免改变其他玩家的世界状态</item>
    ///   <item>独立于 <c>CWRWorld.TimeFrozenTick</c>，二者可叠加</item>
    /// </list>
    /// </summary>
    internal class WorldFreezeSystem : ICWRLoader
    {
        //Liquid.UpdateLiquid 拦截委托
        private delegate void Hook_UpdateLiquid(Action orig);
        //Player.UpdateEquips 拦截委托
        private delegate void Hook_UpdateEquips(Action<Player, int> orig, Player self, int i);

        //TimeGear 注册名，仅作内部时间速率叠加用
        private const string TimeGearKey = "WorldFreezeSystem";

        void ICWRLoader.UnLoadData() {
            IsActive = false;
            activeReasons?.Clear();
            NPCFrozenPositions = null;
            NPCFrozenVelocities = null;
            NPCSnapshotCaptured = null;
            NPCSnapshotTypes = null;
            ProjFrozenPositions = null;
            ProjFrozenVelocities = null;
            ProjSnapshotCaptured = null;
            ProjSpawnedDuringFreeze = null;
            ProjSnapshotTypes = null;
            ProjSnapshotOwners = null;
            ProjSnapshotIdentities = null;
        }

        /// <summary>
        /// 当前是否至少有一个 reason 持有冻结
        /// </summary>
        public static bool IsActive { get; private set; }

        /// <summary>
        /// 多人模式下不允许强冻结世界，由调用方在 <see cref="Activate(string)"/> 之前自检
        /// </summary>
        public static bool AllowFreeze => Main.netMode == NetmodeID.SinglePlayer;

        /// <summary>
        /// 当前所有持有冻结的 reason 集合（只读视图供调试/UI 引用）
        /// </summary>
        public static IReadOnlyCollection<string> ActiveReasons => activeReasons;

        //内部 reason 计数。同一 reason 重复 Activate 不重复入集合
        private static readonly HashSet<string> activeReasons = [];

        //NPC 冻结位置快照
        internal static Vector2[] NPCFrozenPositions;
        //NPC 冻结速度快照
        internal static Vector2[] NPCFrozenVelocities;
        //NPC 快照是否有效
        internal static bool[] NPCSnapshotCaptured;
        //NPC 快照对应类型，用于避免复用槽位时套用旧快照
        internal static int[] NPCSnapshotTypes;
        //弹幕冻结位置快照
        internal static Vector2[] ProjFrozenPositions;
        //弹幕冻结速度快照
        internal static Vector2[] ProjFrozenVelocities;
        //弹幕快照是否有效
        internal static bool[] ProjSnapshotCaptured;
        //标记该弹幕是否在冻结期间新生成，解冻时需清理避免造成爆发伤害
        internal static bool[] ProjSpawnedDuringFreeze;
        //弹幕快照对应类型/归属/身份，避免复用槽位时套用旧快照
        internal static int[] ProjSnapshotTypes;
        internal static int[] ProjSnapshotOwners;
        internal static int[] ProjSnapshotIdentities;

        void ICWRLoader.LoadData() {
            NPCFrozenPositions = new Vector2[Main.maxNPCs];
            NPCFrozenVelocities = new Vector2[Main.maxNPCs];
            NPCSnapshotCaptured = new bool[Main.maxNPCs];
            NPCSnapshotTypes = new int[Main.maxNPCs];
            ProjFrozenPositions = new Vector2[Main.maxProjectiles];
            ProjFrozenVelocities = new Vector2[Main.maxProjectiles];
            ProjSnapshotCaptured = new bool[Main.maxProjectiles];
            ProjSpawnedDuringFreeze = new bool[Main.maxProjectiles];
            ProjSnapshotTypes = new int[Main.maxProjectiles];
            ProjSnapshotOwners = new int[Main.maxProjectiles];
            ProjSnapshotIdentities = new int[Main.maxProjectiles];
        }

        void ICWRLoader.SetupData() {
            //拦截液体更新，使水流在冻结期间不再传播
            MethodInfo liquidMethod = typeof(Liquid).GetMethod("UpdateLiquid"
                , BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (liquidMethod != null) {
                VaultHook.Add(liquidMethod, (Hook_UpdateLiquid)OnUpdateLiquidHook);
            }

            //拦截玩家装备更新，阻止饰品在冻结期间继续运行（生成弹幕、扣除冷却等）
            MethodInfo equipMethod = typeof(Player).GetMethod("UpdateEquips"
                , BindingFlags.Public | BindingFlags.Instance, null, [typeof(int)], null);
            if (equipMethod != null) {
                VaultHook.Add(equipMethod, (Hook_UpdateEquips)OnUpdateEquipsHook);
            }
        }

        private static void OnUpdateLiquidHook(Action orig) {
            if (IsActive) return;
            orig();
        }

        private static void OnUpdateEquipsHook(Action<Player, int> orig, Player self, int i) {
            if (IsActive) return;
            orig(self, i);
        }

        /// <summary>
        /// 请求一个 reason 的冻结。同一 reason 重复调用幂等
        /// <br/>未在单机模式时直接 no-op（由 <see cref="AllowFreeze"/> 控制）
        /// </summary>
        public static void Activate(string reason) {
            if (string.IsNullOrEmpty(reason)) {
                return;
            }
            if (!AllowFreeze) {
                return;
            }
            bool wasInactive = !IsActive;
            activeReasons.Add(reason);
            if (wasInactive) {
                IsActive = true;
                TimeGear.Register(TimeGearKey, 0f);
                SnapshotPositions();
            }
        }

        /// <summary>
        /// 释放某 reason 的冻结；当所有 reason 都释放后才真正解冻
        /// </summary>
        public static void Deactivate(string reason) {
            if (string.IsNullOrEmpty(reason)) {
                return;
            }
            if (!activeReasons.Remove(reason)) {
                return;
            }
            if (activeReasons.Count == 0 && IsActive) {
                FinalizeDeactivate();
            }
        }

        /// <summary>
        /// 立刻清空所有 reason 并解冻（用于玩家死亡 / 世界卸载等异常路径）
        /// </summary>
        public static void DeactivateAll() {
            if (activeReasons.Count == 0 && !IsActive) {
                return;
            }
            activeReasons.Clear();
            if (IsActive) {
                FinalizeDeactivate();
            }
        }

        /// <summary>
        /// 检查某 reason 是否持有冻结
        /// </summary>
        public static bool HasReason(string reason)
            => !string.IsNullOrEmpty(reason) && activeReasons.Contains(reason);

        private static void FinalizeDeactivate() {
            RestoreSnapshots();
            KillProjectilesSpawnedDuringFreeze();
            ClearSnapshots();
            IsActive = false;
            TimeGear.Unregister(TimeGearKey);
        }

        private static void SnapshotPositions() {
            ClearSnapshots();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active) {
                    CaptureNPC(npc);
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active) {
                    //冻结开始前已存在的弹幕不算"冻结期间新生成"
                    CaptureProjectile(proj, spawnedDuringFreeze: false);
                }
            }
        }

        internal static void EnsureNPCSnapshot(NPC npc) {
            int id = npc.whoAmI;
            if (!NPCSnapshotCaptured[id] || NPCSnapshotTypes[id] != npc.type) {
                CaptureNPC(npc);
            }
        }

        internal static void EnsureProjectileSnapshot(Projectile proj) {
            int id = proj.whoAmI;
            if (!ProjSnapshotCaptured[id]
                || ProjSnapshotTypes[id] != proj.type
                || ProjSnapshotOwners[id] != proj.owner
                || ProjSnapshotIdentities[id] != proj.identity) {
                //首次出现且当前处于冻结状态，说明是冻结期间被生成
                CaptureProjectile(proj, spawnedDuringFreeze: IsActive);
            }
        }

        private static void CaptureNPC(NPC npc) {
            int id = npc.whoAmI;
            NPCFrozenPositions[id] = npc.position;
            NPCFrozenVelocities[id] = npc.velocity;
            NPCSnapshotCaptured[id] = true;
            NPCSnapshotTypes[id] = npc.type;
        }

        private static void CaptureProjectile(Projectile proj, bool spawnedDuringFreeze) {
            int id = proj.whoAmI;
            ProjFrozenPositions[id] = proj.position;
            ProjFrozenVelocities[id] = proj.velocity;
            ProjSnapshotCaptured[id] = true;
            ProjSpawnedDuringFreeze[id] = spawnedDuringFreeze;
            ProjSnapshotTypes[id] = proj.type;
            ProjSnapshotOwners[id] = proj.owner;
            ProjSnapshotIdentities[id] = proj.identity;
        }

        private static void RestoreSnapshots() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !NPCSnapshotCaptured[i] || NPCSnapshotTypes[i] != npc.type) continue;
                npc.velocity = NPCFrozenVelocities[i];
            }

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !ProjSnapshotCaptured[i]) continue;
                if (ProjSnapshotTypes[i] != proj.type
                    || ProjSnapshotOwners[i] != proj.owner
                    || ProjSnapshotIdentities[i] != proj.identity) {
                    continue;
                }
                proj.velocity = ProjFrozenVelocities[i];
            }
        }

        private static void KillProjectilesSpawnedDuringFreeze() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (!ProjSpawnedDuringFreeze[i]) continue;
                Projectile proj = Main.projectile[i];
                if (!proj.active) continue;
                //校验槽位未被复用
                if (ProjSnapshotTypes[i] != proj.type
                    || ProjSnapshotOwners[i] != proj.owner
                    || ProjSnapshotIdentities[i] != proj.identity) {
                    continue;
                }
                proj.Kill();
            }
        }

        private static void ClearSnapshots() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPCSnapshotCaptured[i] = false;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                ProjSnapshotCaptured[i] = false;
                ProjSpawnedDuringFreeze[i] = false;
            }
        }

        /// <summary>
        /// 判断该 NPC 是否应被冻结（目前一律冻结，留作未来按类型放行的扩展点）
        /// </summary>
        internal static bool ShouldFreezeNPC(NPC npc) {
            if (!npc.active) return false;
            return true;
        }

        /// <summary>
        /// 判断该弹幕是否应被冻结（目前一律冻结，留作未来按类型放行的扩展点）
        /// </summary>
        internal static bool ShouldFreezeProjectile(Projectile proj) {
            if (!proj.active) return false;
            return true;
        }
    }
}
