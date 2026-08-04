using CalamityOverhaul.Content.TimeFreezes;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 时缓数据管理，TimeGear 缩放实体漂移
    /// <br/>实体运动快照由 <see cref="TimeFreezeSystem"/> 统一持有，保留 SlowFactor 缩放与 friendly 豁免语义
    /// <br/>NPC/弹幕来源登记见 <see cref="SandevistanNPC"/>、<see cref="SandevistanProjectile"/>
    /// </summary>
    internal class SandevistanTimeSlow : ModSystem
    {
        //TimeGear 注册名
        internal const string TimeGearKey = "Sandevistan";

        //时缓生效中
        public static bool IsActive { get; private set; }
        //速度缩放，0.08≈原速 8%
        public static float SlowFactor = 0.08f;

        public override void Unload() {
            Deactivate();
            SlowFactor = 0.08f;
        }

        public override void OnWorldUnload() => Deactivate();

        //开，TimeGear 注册+快照速度
        public static void Activate() {
            if (IsActive) {
                return;
            }
            IsActive = true;
            try {
                TimeGear.Register(TimeGearKey, SlowFactor);
                AcquireAllEntities();
            }
            catch (System.Exception exception) {
                IsActive = false;
                try {
                    ReleaseAllEntities();
                }
                finally {
                    TimeGear.Unregister(TimeGearKey);
                }
                CWRMod.Instance?.Logger.Error(
                    $"Sandevistan activation failed: {exception}");
            }
        }

        //关，释放统一运动租约
        public static void Deactivate() {
            if (!IsActive) {
                TimeGear.Unregister(TimeGearKey);
                return;
            }
            IsActive = false;
            try {
                ReleaseAllEntities();
            }
            finally {
                TimeGear.Unregister(TimeGearKey);
            }
        }

        private static void AcquireAllEntities() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (ShouldAffectNPC(npc)) {
                    EnsureNPCSource(npc);
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && ShouldAffectProjectile(proj)) {
                    EnsureProjectileSource(proj);
                }
            }
        }

        private static void ReleaseAllEntities() {
            if (Main.npc != null) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    TimeFreezeSystem.ReleaseVelocityScaleNPC<SandevistanTimeSlow>(npc);
                }
            }
            if (Main.projectile != null) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile projectile = Main.projectile[i];
                    if (projectile?.active == true) {
                        TimeFreezeSystem.ReleaseVelocityScaleProjectile<SandevistanTimeSlow>(
                            projectile);
                    }
                }
            }
        }

        internal static void EnsureNPCSource(NPC npc)
            => TimeFreezeSystem.AcquireVelocityScaleNPC<SandevistanTimeSlow>(
                npc, SlowFactor);

        internal static void EnsureProjectileSource(Projectile projectile)
            => TimeFreezeSystem.AcquireVelocityScaleProjectile<SandevistanTimeSlow>(
                projectile, SlowFactor);

        internal static bool ShouldAffectNPC(NPC npc) => npc.active;

        internal static bool ShouldAffectProjectile(Projectile proj) {
            if (proj.friendly || proj.hide) {
                return false;
            }
            if (Main.projPet[proj.type] || proj.minion || Main.projHook[proj.type]) {
                return false;
            }
            //ImmuneFrozen，免时停则免时缓
            if (CWRLoad.ProjValue.ImmuneFrozen[proj.type]) {
                return false;
            }
            return true;
        }
    }
}
