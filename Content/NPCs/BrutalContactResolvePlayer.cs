using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs
{
    /// <summary>
    /// 残酷重制 Boss 的体撞兜底结算（#57 克眼/公爵冲刺穿身无伤）。
    /// 原版接触伤害在 Player.Update 末段的 Update_NPCCollision 里裁决，
    /// 链路上任何一环被外部系统吃掉（钩子否决、免疫槽异常、第三方 IL 补丁）
    /// 都表现为"冲刺穿身无伤"，且对静态审计不可见（#57 三轮审计全绿仍复发）。
    /// 本类在玩家帧末按与原版完全相同的门槛补一次裁决：
    /// 原版命中过则无敌帧已挂、此处必然跳过，永不双结算；
    /// 原版被吞时由此处落刀，并写日志指认，供真机定位吞点。
    /// 伤害窗口仍完全由各状态写入的 npc.damage 声明，绝不越过设计上的无伤窗。
    /// 接触伤害是端侧结算（每个客户端只裁决本机玩家），Hurt 自带网络同步。
    /// </summary>
    internal class BrutalContactResolvePlayer : ModPlayer
    {
        /// <summary>日志限频帧标（兜底落刀与 DEBUG 探针共用）</summary>
        private uint lastLogTick;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            if (Player.dead || Player.ghost || Player.creativeGodMode) {
                return;
            }

            Rectangle playerRect = Player.getRect();
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)) {
                    continue;
                }

                bool brutalBound = false;
                foreach (NPCOverride inds in overrides.Values) {
                    if (inds is BrutalNPCOverride) {
                        brutalBound = true;
                        break;
                    }
                }
                if (!brutalBound) {
                    continue;
                }

                TryResolveContact(npc, playerRect);
            }
        }

        private void TryResolveContact(NPC npc, Rectangle playerRect) {
            //免疫槽初值与原版 Update_NPCCollision 的类型表一致：月总体节与光女走 Boss 槽位
            int slot = npc.type switch {
                NPCID.MoonLordHead or NPCID.MoonLordHand or NPCID.MoonLordCore
                    or NPCID.MoonLordLeechBlob or NPCID.MoonLordFreeEye or NPCID.HallowBoss => 1,
                _ => -1,
            };

            //原版同款碰撞盒修正与倍率（特殊姿态的盒体变形由 GetMeleeCollisionData 统一处理）
            float damageMultiplier = 1f;
            Rectangle npcRect = npc.getRect();
            NPC.GetMeleeCollisionData(playerRect, npc.whoAmI, ref slot, ref damageMultiplier, ref npcRect);
            if (!playerRect.Intersects(npcRect)) {
                return;
            }

            //伤害窗关闭属状态设计（公平阀/演出/蓄力），只在 DEBUG 里指认，不结算
            if (npc.damage <= 0) {
                Probe(npc, "伤害窗关闭");
                return;
            }
            //原版盾冲期间对被撞怪免疫
            if (Player.dash == 2 && npc.whoAmI == Player.eocHit && Player.eocDash > 0) {
                Probe(npc, "盾冲免疫");
                return;
            }
            if (Player.npcTypeNoAggro[npc.type]) {
                Probe(npc, "npcTypeNoAggro");
                return;
            }
            //钩子链与原版一致：时停/骇入虚弱/相位无害窗等合法否决都在此生效
            if (!NPCLoader.CanHitPlayer(npc, Player, ref slot)
                || !PlayerLoader.CanBeHitByNPC(Player, npc, ref slot)) {
                Probe(npc, "CanHitPlayer钩子否决");
                return;
            }
            //原版同款无敌口径：通用无敌帧或指定免疫槽——原版已命中时必然在此跳过
            bool immune = slot < 0 ? Player.immune : Player.hurtCooldowns[slot] > 0;
            if (immune) {
                return;
            }

            int hitDirection = npc.Center.X < Player.Center.X ? 1 : -1;
            int damage = Main.DamageVar(npc.damage * damageMultiplier, -Player.luck);
            Player.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), damage, hitDirection, cooldownCounter: slot);

            //兜底落刀=原版裁决被吞的运行期实锤，留档指认（限频防刷屏）
            if (Main.GameUpdateCount - lastLogTick > 30) {
                lastLogTick = Main.GameUpdateCount;
                CWRMod.Instance.Logger.Info(
                    $"[BrutalContact] 原版接触裁决未生效，已兜底：{npc.FullName}#{npc.whoAmI} " +
                    $"state={npc.ai[2]} dmg={damage} vel={npc.velocity.Length():0.0}");
            }
        }

        /// <summary>DEBUG 探针：与重制 Boss 重叠却未结算时，指认拦下它的那道门</summary>
        [System.Diagnostics.Conditional("DEBUG")]
        private void Probe(NPC npc, string gate) {
            if (Main.GameUpdateCount - lastLogTick < 60) {
                return;
            }
            lastLogTick = Main.GameUpdateCount;
            CWRMod.Instance.Logger.Debug(
                $"[BrutalContact] 重叠未结算 gate={gate} npc={npc.FullName}#{npc.whoAmI} " +
                $"state={npc.ai[2]} dmg={npc.damage} vel={npc.velocity.Length():0.0} immune={Player.immune}");
        }
    }
}
