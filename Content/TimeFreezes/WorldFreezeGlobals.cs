using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using InnoVault.GameSystem;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary>NPC AI 冻结链 WorldFreezeSystem &gt; TimeFrozenTick &gt; 专属</summary>
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
                if (npc.CWR().TimeFrozenTick > 0) {
                    npc.CWR().TimeFrozenTick--;
                    CWRNpc.DoTimeFrozen(npc);
                    return false;
                }

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

    /// <summary>WorldFreeze 期间 NPC 禁碰撞</summary>
    internal class WorldFreezeNPC : GlobalNPC
    {
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            //全局 TimeFrozenTick 时停演出（村正次元斩/鬼切终之太刀）中玩家保持自由行动，
            //被冻结在原地的 NPC 不应再构成接触伤害威胁
            if (CWRWorld.TimeFrozenTick > 0) {
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

    /// <summary>WorldFreeze / TimeFrozenTick 弹幕 PreAI 拦截</summary>
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
            if (proj.Alives()) {
                if (proj.CWR().TimeFrozenTick > 0) {
                    proj.CWR().TimeFrozenTick--;
                    proj.position = proj.oldPosition;
                    proj.timeLeft++;
                    return false;
                }
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

    internal class WorldFreezeTileProcessor : GlobalTileProcessor
    {
        public override bool PreSingleInstanceUpdate(TileProcessor tileProcessor) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return base.PreSingleInstanceUpdate(tileProcessor);
        }

        public override bool PreUpdate(TileProcessor tileProcessor) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return base.PreUpdate(tileProcessor);
        }
    }
}
