using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>斯安威斯坦全局实体时缓来源</summary>
    internal sealed class SandevistanTimeSlow : ModSystem
    {
        private const float ScaleEpsilon = 0.0001f;
        private static float appliedScale = 1f;

        public static bool IsActive => appliedScale < 1f - ScaleEpsilon;
        public static float SlowFactor => appliedScale;

        internal static void ApplyAggregate(float scale) {
            if (!float.IsFinite(scale) || scale <= 0f || scale > 1f) {
                scale = 1f;
            }
            scale = Math.Clamp(scale, 0.001f, 1f);
            if (MathF.Abs(scale - appliedScale) <= ScaleEpsilon) {
                if (IsActive) {
                    TimeGear.Register<SandevistanTimeSlow>(appliedScale);
                }
                return;
            }

            appliedScale = scale;
            if (IsActive) {
                TimeGear.Register<SandevistanTimeSlow>(appliedScale);
                ReconcileAllEntities();
            }
            else {
                TimeGear.Unregister<SandevistanTimeSlow>();
                //失活沿必须显式全表清场：Reconcile 系列在 !IsActive 时早退，
                //靠它们清不掉已挂的实体时缓，Boss 会永远留在慢放里（反馈 #6/#31）
                ClearAllEntities();
            }
        }

        internal static void Reset() {
            appliedScale = 1f;
            TimeGear.Unregister<SandevistanTimeSlow>();
            ClearAllEntities();
        }

        internal static void ReconcileNPC(NPC npc) {
            //失活期间无事可做：失活沿的 ClearAllEntities 已全表清场，
            //新生实体也不会带残留，这里直接早退省掉每实体每帧的全局实例访问
            if (!IsActive) {
                return;
            }
            if (npc?.active != true) {
                return;
            }
            if (ShouldAffectNPC(npc)) {
                TimeFreezeSystem.SetNPCTimeScale<SandevistanTimeSlow>(
                    npc, appliedScale);
            }
            else {
                TimeFreezeSystem.ClearNPCTimeScale<SandevistanTimeSlow>(npc);
            }
        }

        internal static void ReconcileProjectile(Projectile projectile) {
            //同 ReconcileNPC：失活期间直接早退
            if (!IsActive) {
                return;
            }
            if (projectile?.active != true) {
                return;
            }
            if (ShouldAffectProjectile(projectile)) {
                TimeFreezeSystem.SetProjectileTimeScale<SandevistanTimeSlow>(
                    projectile, appliedScale);
            }
            else {
                TimeFreezeSystem.ClearProjectileTimeScale<SandevistanTimeSlow>(
                    projectile);
            }
        }

        internal static bool ShouldAffectNPC(NPC npc)
            => npc?.active == true && !npc.friendly && !npc.townNPC
            && !npc.CountsAsACritter && npc.type != NPCID.TargetDummy;

        internal static bool ShouldAffectProjectile(Projectile projectile) {
            if (projectile?.active != true || !projectile.hostile
                || projectile.friendly) {
                return false;
            }

            int type = projectile.type;
            if (type <= ProjectileID.None || type >= ProjectileLoader.ProjectileCount) {
                return false;
            }
            if (type < Main.projPet.Length && Main.projPet[type]
                || projectile.minion
                || type < Main.projHook.Length && Main.projHook[type]) {
                return false;
            }
            if (CWRLoad.ProjValue.ImmuneFrozen.TryGetValue(type,
                out bool immuneFrozen) && immuneFrozen) {
                return false;
            }
            return true;
        }

        private static void ReconcileAllEntities() {
            if (Main.npc != null) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    ReconcileNPC(npc);
                }
            }
            if (Main.projectile == null) {
                return;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile?.active == true) {
                    ReconcileProjectile(projectile);
                }
            }
        }

        private static void ClearAllEntities() {
            if (Main.npc != null) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    TimeFreezeSystem.ClearNPCTimeScale<SandevistanTimeSlow>(npc);
                }
            }
            if (Main.projectile == null) {
                return;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile?.active == true) {
                    TimeFreezeSystem.ClearProjectileTimeScale<SandevistanTimeSlow>(
                        projectile);
                }
            }
        }
    }
}
