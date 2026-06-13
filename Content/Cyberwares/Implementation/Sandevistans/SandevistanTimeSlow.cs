using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 时缓数据管理，TimeGear 缩放实体漂移
    /// <br/>NPC/弹幕拦截见 SandevistanNPC、SandevistanProjectile
    /// </summary>
    internal class SandevistanTimeSlow : ModSystem
    {
        //时缓生效中
        public static bool IsActive { get; private set; }
        //速度缩放，0.08≈原速 8%
        public static float SlowFactor = 0.08f;

        //激活瞬间抓取的 NPC 速度
        internal static Vector2[] NPCCachedVelocities;
        internal static bool[] NPCHasCache;
        //弹幕速度快照
        internal static Vector2[] ProjCachedVelocities;
        internal static bool[] ProjHasCache;

        public override void Load() {
            NPCCachedVelocities = new Vector2[Main.maxNPCs];
            NPCHasCache = new bool[Main.maxNPCs];
            ProjCachedVelocities = new Vector2[Main.maxProjectiles];
            ProjHasCache = new bool[Main.maxProjectiles];
        }

        public override void Unload() {
            NPCCachedVelocities = null;
            NPCHasCache = null;
            ProjCachedVelocities = null;
            ProjHasCache = null;
        }

        //开启：TimeGear 注册 + 快照速度
        public static void Activate() {
            if (IsActive) {
                return;
            }
            IsActive = true;
            TimeGear.Register("Sandevistan", SlowFactor);
            SnapshotAllEntities();
        }

        //关闭：TimeGear 注销 + 清缓存
        public static void Deactivate() {
            if (!IsActive) {
                return;
            }
            IsActive = false;
            TimeGear.Unregister("Sandevistan");
            ClearAllCache();
        }

        private static void SnapshotAllEntities() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (ShouldAffectNPC(npc)) {
                    NPCCachedVelocities[npc.whoAmI] = npc.velocity;
                    NPCHasCache[npc.whoAmI] = true;
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && ShouldAffectProjectile(proj)) {
                    ProjCachedVelocities[i] = proj.velocity;
                    ProjHasCache[i] = true;
                }
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
