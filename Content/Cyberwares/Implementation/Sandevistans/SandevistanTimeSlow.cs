using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦的时缓核心数据管理器。
    /// 通过冻结实体AI并让它们按缓存速度的缩放值缓慢漂移来模拟子弹时间。
    /// 实际的NPC和弹幕拦截由同目录下的SandevistanNPC和SandevistanProjectile独立处理。
    /// </summary>
    internal class SandevistanTimeSlow : ModSystem
    {
        //时缓是否正在生效
        public static bool IsActive { get; private set; }
        //速度缩放系数，值越小世界越慢。0.08就是原速的8%
        public static float SlowFactor = 0.08f;

        //NPC的速度快照，时缓激活瞬间抓取
        internal static Vector2[] NPCCachedVelocities;
        internal static bool[] NPCHasCache;
        //弹幕的速度快照
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

        //开启时缓，记录当前场上所有敌对实体的速度
        public static void Activate() {
            if (IsActive) {
                return;
            }
            IsActive = true;
            TimeGear.Register("Sandevistan", SlowFactor);
            SnapshotAllEntities();
        }

        //关闭时缓，释放缓存的速度数据
        public static void Deactivate() {
            if (!IsActive) {
                return;
            }
            IsActive = false;
            TimeGear.Unregister("Sandevistan");
            ClearAllCache();
        }

        //遍历场上所有合法目标，把它们当前的速度存下来
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

        //判断这个NPC是否应该被时缓影响
        internal static bool ShouldAffectNPC(NPC npc) => npc.active;

        //判断这个弹幕是否应该被时缓影响
        internal static bool ShouldAffectProjectile(Projectile proj) {
            if (proj.friendly || proj.hide) {
                return false;
            }
            if (Main.projPet[proj.type] || proj.minion || Main.projHook[proj.type]) {
                return false;
            }
            //复用现有的冻结豁免表，免疫时停的弹幕也免疫时缓
            if (CWRLoad.ProjValue.ImmuneFrozen[proj.type]) {
                return false;
            }
            return true;
        }
    }
}
