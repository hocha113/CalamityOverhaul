using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 时缓数据管理，TimeGear 缩放实体漂移
    /// <br/>快照/槽位校验/还原参考 <see cref="WorldFreezeSystem"/>，保留 SlowFactor 缩放与 friendly 豁免语义
    /// <br/>NPC/弹幕拦截见 <see cref="SandevistanNPC"/>、<see cref="SandevistanProjectile"/>
    /// </summary>
    internal class SandevistanTimeSlow : ModSystem
    {
        //TimeGear 注册名
        internal const string TimeGearKey = "Sandevistan";

        //时缓生效中
        public static bool IsActive { get; private set; }
        //速度缩放，0.08≈原速 8%
        public static float SlowFactor = 0.08f;

        //激活瞬间抓取的 NPC 原速度
        internal static Vector2[] NPCCachedVelocities;
        internal static bool[] NPCHasCache;
        //NPC 类型校验，防槽位复用套旧数据
        internal static int[] NPCSnapshotTypes;

        //弹幕速度快照
        internal static Vector2[] ProjCachedVelocities;
        internal static bool[] ProjHasCache;
        //弹幕 type/owner/identity 校验，防槽位复用套旧数据
        internal static int[] ProjSnapshotTypes;
        internal static int[] ProjSnapshotOwners;
        internal static int[] ProjSnapshotIdentities;

        public override void Load() {
            NPCCachedVelocities = new Vector2[Main.maxNPCs];
            NPCHasCache = new bool[Main.maxNPCs];
            NPCSnapshotTypes = new int[Main.maxNPCs];
            ProjCachedVelocities = new Vector2[Main.maxProjectiles];
            ProjHasCache = new bool[Main.maxProjectiles];
            ProjSnapshotTypes = new int[Main.maxProjectiles];
            ProjSnapshotOwners = new int[Main.maxProjectiles];
            ProjSnapshotIdentities = new int[Main.maxProjectiles];
        }

        public override void Unload() {
            NPCCachedVelocities = null;
            NPCHasCache = null;
            NPCSnapshotTypes = null;
            ProjCachedVelocities = null;
            ProjHasCache = null;
            ProjSnapshotTypes = null;
            ProjSnapshotOwners = null;
            ProjSnapshotIdentities = null;
        }

        //开启：TimeGear 注册 + 快照速度
        public static void Activate() {
            if (IsActive) {
                return;
            }
            IsActive = true;
            TimeGear.Register(TimeGearKey, SlowFactor);
            SnapshotAllEntities();
        }

        //关闭：TimeGear 注销 + restore 原速度 + 清缓存
        //不 restore 会让弹幕永久卡在 SlowFactor 倍速（大多数弹幕 velocity 是固定的，没有 AI 重算）
        public static void Deactivate() {
            if (!IsActive) {
                return;
            }
            IsActive = false;
            RestoreSnapshots();
            ClearAllCache();
            TimeGear.Unregister(TimeGearKey);
        }

        private static void SnapshotAllEntities() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (ShouldAffectNPC(npc)) {
                    CaptureNPC(npc);
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && ShouldAffectProjectile(proj)) {
                    CaptureProjectile(proj);
                }
            }
        }

        /// <summary>PreAI 兜底：缓速期间首次出现或槽位被复用则重新抓取</summary>
        internal static void EnsureNPCSnapshot(NPC npc) {
            int id = npc.whoAmI;
            if (!NPCHasCache[id] || NPCSnapshotTypes[id] != npc.type) {
                CaptureNPC(npc);
            }
        }

        /// <summary>PreAI 兜底：缓速期间首次出现或槽位被复用则重新抓取</summary>
        internal static void EnsureProjectileSnapshot(Projectile proj) {
            int id = proj.whoAmI;
            if (!ProjHasCache[id]
                || ProjSnapshotTypes[id] != proj.type
                || ProjSnapshotOwners[id] != proj.owner
                || ProjSnapshotIdentities[id] != proj.identity) {
                CaptureProjectile(proj);
            }
        }

        private static void CaptureNPC(NPC npc) {
            int id = npc.whoAmI;
            NPCCachedVelocities[id] = npc.velocity;
            NPCHasCache[id] = true;
            NPCSnapshotTypes[id] = npc.type;
        }

        private static void CaptureProjectile(Projectile proj) {
            int id = proj.whoAmI;
            ProjCachedVelocities[id] = proj.velocity;
            ProjHasCache[id] = true;
            ProjSnapshotTypes[id] = proj.type;
            ProjSnapshotOwners[id] = proj.owner;
            ProjSnapshotIdentities[id] = proj.identity;
        }

        //把每帧被 PreAI 改写成 slowVel 的 velocity 还原为原速度，校验槽位防越槽
        private static void RestoreSnapshots() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (!NPCHasCache[i]) {
                    continue;
                }
                NPC npc = Main.npc[i];
                if (!npc.active || NPCSnapshotTypes[i] != npc.type) {
                    continue;
                }
                npc.velocity = NPCCachedVelocities[i];
            }

            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (!ProjHasCache[i]) {
                    continue;
                }
                Projectile proj = Main.projectile[i];
                if (!proj.active) {
                    continue;
                }
                if (ProjSnapshotTypes[i] != proj.type
                    || ProjSnapshotOwners[i] != proj.owner
                    || ProjSnapshotIdentities[i] != proj.identity) {
                    continue;
                }
                proj.velocity = ProjCachedVelocities[i];
            }
        }

        private static void ClearAllCache() {
            Array.Clear(NPCHasCache);
            Array.Clear(ProjHasCache);
        }

        internal static bool ShouldAffectNPC(NPC npc) => npc.active;

        internal static bool ShouldAffectProjectile(Projectile proj) {
            if (proj.friendly || proj.hide) {
                return false;
            }
            if (Main.projPet[proj.type] || proj.minion || Main.projHook[proj.type]) {
                return false;
            }
            //ImmuneFrozen 表：免疫时停则免疫时缓
            if (CWRLoad.ProjValue.ImmuneFrozen[proj.type]) {
                return false;
            }
            return true;
        }
    }
}
