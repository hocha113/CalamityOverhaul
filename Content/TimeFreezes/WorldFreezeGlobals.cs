using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using InnoVault.GameSystem;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary>
    /// 更加强大的拦截 ai：
    /// 优先级 <see cref="WorldFreezeSystem"/> &gt; <c>CWRWorld.TimeFrozenTick</c> &gt; 各专属 AI 干预
    /// </summary>
    internal class WorldFreezeOverNPC : NPCOverride
    {
        public override int TargetID => -1;
        public override bool AI() {
            if (WorldFreezeSystem.IsActive) {
                if (!WorldFreezeSystem.ShouldFreezeNPC(npc)) {
                    return true;
                }
                int id = npc.whoAmI;
                WorldFreezeSystem.EnsureNPCSnapshot(npc);
                npc.position = WorldFreezeSystem.NPCFrozenPositions[id];
                npc.velocity = Vector2.Zero;
                npc.aiAction = 0;
                npc.frameCounter = 0;
                npc.timeLeft++;
                return false;
            }
            if (CWRWorld.TimeFrozenTick > 0) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }
            if (npc.Alives()) {
                bool? result = npc.GetGlobalNPC<HackEffectNPC>().PreAIByOverNPC(npc);
                if (result.HasValue) {
                    return result.Value;
                }
                result = CyberDomainFreezeGlobalNPC.PreAIByOverNPC(npc);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 统一 NPC 时间冻结拦截器，处理 <see cref="WorldFreezeSystem"/> 与 <c>CWRWorld.TimeFrozenTick</c> 两种冻结模式
    /// </summary>
    internal class WorldFreezeNPC : GlobalNPC
    {
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return true;
        }

        public override bool CanHitNPC(NPC npc, NPC target) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 统一弹幕时间冻结拦截器，处理 <see cref="WorldFreezeSystem"/> 与 <c>CWRWorld.TimeFrozenTick</c> 两种冻结模式
    /// </summary>
    internal class WorldFreezeProjectile : GlobalProjectile
    {
        public override bool PreAI(Projectile proj) {
            if (WorldFreezeSystem.IsActive) {
                if (!WorldFreezeSystem.ShouldFreezeProjectile(proj)) {
                    return true;
                }
                int id = proj.whoAmI;
                WorldFreezeSystem.EnsureProjectileSnapshot(proj);
                proj.position = WorldFreezeSystem.ProjFrozenPositions[id];
                proj.velocity = Vector2.Zero;
                proj.timeLeft++;
                return false;
            }
            if (CWRWorld.TimeFrozenTick > 0 && !proj.hide && !proj.friendly
                && !Main.projPet[proj.type] && !proj.minion && !Main.projHook[proj.type]
                && !CWRLoad.ProjValue.ImmuneFrozen[proj.type]) {
                proj.position = proj.oldPosition;
                proj.timeLeft++;
                return false;
            }
            return true;
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return null;
        }

        public override bool CanHitPlayer(Projectile projectile, Player target) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return true;
        }

        public override bool CanHitPvp(Projectile projectile, Player target) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return true;
        }
    }
}
