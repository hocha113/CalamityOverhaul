using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using CalamityOverhaul.Content.Wraiths.VFX;
using InnoVault.GameSystem;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Players
{
    internal class PlayerDeath : PlayerOverride
    {
        public bool Doomed { get; set; }

        //防重入：同一 tick 只触发一次替死
        private bool scapeUsedThisTick;

        //联机：客户端挂起等待服务端裁定
        internal bool ScapeGhostPending { get; private set; }
        private double scapeGhostPendingDamage;
        private int scapeGhostPendingHitDir;

        //延迟死亡：fail 包到达后在 PostUpdate 执行，避免从包处理器直接调 KillMe
        private bool scapeGhostKillDeferred;

        public override void ResetEffects() {
            Doomed = false;
            scapeUsedThisTick = false;
        }

        public override void PostUpdate() {
            if (!scapeGhostKillDeferred || !VaultUtils.isClient) {
                return;
            }
            scapeGhostKillDeferred = false;
            ScapeGhostPending = false;
            Doomed = true;
            //HP 归零后游戏循环会触发 KillMe；直接调 KillMe 以确保立即生效
            Player.KillMe(
                PlayerDeathReason.ByCustomReason(NetworkText.FromKey(WraithSystemText.ScapeGhostNoTarget.Key)),
                scapeGhostPendingDamage, scapeGhostPendingHitDir, false);
        }

        public override bool? On_PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Doomed) {
                return true;
            }

            if (Player.GetModPlayer<SirenMusicalBoxPlayer>().IsCursed) {
                if (Player.TryGetOverride(out HalibutPlayer halibutPlayer)
                    && halibutPlayer.ResurrectionSystem.Ratio == 1f) {
                    return true;
                }

                Player.statLife = Math.Clamp(Player.statLife, 1, Player.statLifeMax2);
                return false;//八音盒诅咒
            }

            if (Player.CountProjectilesOfID<RestartEffectProj>() > 0) {
                return false;//正在重启，不死
            }

            if (Player.CountProjectilesOfID<YourLevelIsTooLowProj>() > 0) {
                return false;//无限重启，不死
            }

            if (TryInvokeScapeGhost(damage, hitDirection, damageSource)) {
                return false;
            }

            return null;
        }

        private bool TryInvokeScapeGhost(double damage, int hitDirection, PlayerDeathReason damageSource) {
            if (scapeUsedThisTick) {
                return false;
            }
            scapeUsedThisTick = true;

            if (VaultUtils.isClient) {
                if (ScapeGhostPending) {
                    //已发出请求，封锁死亡画面直到服务端回包
                    Player.statLife = Math.Max(Player.statLife, 1);
                    return true;
                }
                ScapeGhostPending = true;
                scapeGhostPendingDamage = damage;
                scapeGhostPendingHitDir = hitDirection;
                Player.statLife = Math.Max(Player.statLife, 1);
                WraithNet.SendScapeGhostRequest(Player.whoAmI, damage, hitDirection);
                return true;
            }

            //单人或服务端：权威执行
            return ExecuteScapeGhostAuthority(Player, damage, hitDirection, damageSource);
        }

        /// <summary>
        /// 权威端替死执行。单人直接调用；服务端由 WraithNet.HandleScapeGhostRequest 调用。
        /// </summary>
        internal static bool ExecuteScapeGhostAuthority(Player player, double damage, int hitDirection
            , PlayerDeathReason damageSource) {
            NPC proxy = FindScapeTargetFor(player);
            if (proxy == null) {
                return false;
            }

            Vector2 from = player.Center;
            Vector2 to = proxy.Center;
            string targetName = proxy.FullName;
            NPC.HitInfo hit = BuildTransferredHit(proxy, damage, hitDirection, damageSource);
            proxy.StrikeNPC(hit);

            player.statLife = Math.Max(player.statLife, Math.Max(1, (int)(player.statLifeMax2 * 0.12f)));
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 75);
            player.GetModPlayer<WraithPlayer>().AddErosion(0.30f);

            if (VaultUtils.isServer) {
                WraithNet.SendScapeGhostFx(from, to, player.whoAmI, targetName);
                NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, player.whoAmI);
            }
            else {
                ScapeArmRenderer.Trigger(from, to);
                VaultUtils.Text(WraithSystemText.ScapeGhostActivated.Format(targetName)
                    , new Color(178, 34, 44));
            }
            return true;
        }

        /// <summary>
        /// 客户端收到服务端替死结果包后调用（在 WraithNet 的包处理器中）。<br/>
        /// 成功时触发本地 FX；失败时挂延迟死亡，在下一帧 PostUpdate 执行。
        /// </summary>
        internal void ApplyScapeResult(bool success, Vector2 from, Vector2 to, string targetName) {
            ScapeGhostPending = false;
            if (success) {
                ScapeArmRenderer.Trigger(from, to);
                string name = string.IsNullOrWhiteSpace(targetName)
                    ? WraithSystemText.ScapeGhostUnknownTarget.Value
                    : targetName;
                VaultUtils.Text(WraithSystemText.ScapeGhostActivated.Format(name), new Color(178, 34, 44));
            }
            else {
                //服务端未找到代理目标，延迟一帧执行真实死亡
                scapeGhostKillDeferred = true;
            }
        }

        /// <summary>
        /// 距离不封顶；先比较牺牲层级，再在同层选最近目标。<br/>
        /// 城镇居民 → 动物/友善生物 → 中性生物 → 普通敌怪 → Boss。
        /// </summary>
        private static NPC FindScapeTargetFor(Player player) {
            NPC best = null;
            int bestPriority = int.MaxValue;
            float bestDistanceSq = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!CanReceiveScapeHit(npc)) {
                    continue;
                }

                int priority = GetScapePriority(npc);
                float distanceSq = Vector2.DistanceSquared(player.Center, npc.Center);
                if (priority < bestPriority || priority == bestPriority && distanceSq < bestDistanceSq) {
                    best = npc;
                    bestPriority = priority;
                    bestDistanceSq = distanceSq;
                }
            }
            return best;
        }

        private static bool CanReceiveScapeHit(NPC npc)
            => npc != null && npc.active && npc.life > 0 && npc.lifeMax > 1
                && !npc.dontTakeDamage && !npc.immortal && npc.type != NPCID.TargetDummy;

        private static int GetScapePriority(NPC npc) {
            if (npc.townNPC) {
                return 0;
            }
            if (npc.CountsAsACritter || npc.friendly) {
                return 1;
            }
            if (npc.damage <= 0 && !npc.boss) {
                return 2;
            }
            return npc.boss ? 4 : 3;
        }

        /// <summary>
        /// 将 PlayerDeathReason 中仍存活的致因实体投影成 NPC HitInfo。<br/>
        /// 保留弹幕伤害类型、基础伤害、击退与袭击方向；无法解析时退化为最终致死伤害。
        /// </summary>
        internal static NPC.HitInfo BuildTransferredHit(NPC proxy, double damage, int hitDirection
            , PlayerDeathReason damageSource) {
            int receivedDamage = Math.Max(1, (int)Math.Min(Math.Ceiling(damage), int.MaxValue));
            int sourceDamage = receivedDamage;
            int transferredDamage = proxy.boss ? receivedDamage : Math.Max(receivedDamage, proxy.life);
            float knockback = 4f;
            int direction = hitDirection == 0 ? (proxy.direction == 0 ? 1 : -proxy.direction) : hitDirection;
            DamageClass damageType = DamageClass.Default;

            if (damageSource?.TryGetCausingEntity(out Entity cause) == true) {
                Vector2 causeCenter = cause.Center;
                direction = proxy.Center.X >= causeCenter.X ? 1 : -1;
                if (cause is Projectile projectile) {
                    damageType = projectile.DamageType ?? DamageClass.Default;
                    sourceDamage = Math.Max(sourceDamage, projectile.damage);
                    knockback = Math.Max(projectile.knockBack, 0f);
                }
                else if (cause is NPC npc) {
                    sourceDamage = Math.Max(sourceDamage, npc.damage);
                    knockback = MathHelper.Clamp(npc.velocity.Length() * 0.35f, 2f, 12f);
                }
            }

            return new NPC.HitInfo {
                DamageType = damageType,
                SourceDamage = sourceDamage,
                Damage = transferredDamage,
                Knockback = knockback,
                HitDirection = direction,
                Crit = false,
            };
        }
    }
}