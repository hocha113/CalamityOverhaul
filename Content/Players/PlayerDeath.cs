using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using CalamityOverhaul.Content.Wraiths.VFX;
using InnoVault.GameSystem;
using System;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
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
            var (proxies, isFriendly) = FindScapeTargetsFor(player);
            if (proxies == null) {
                return false;
            }

            NPC primary = proxies[0];
            Vector2 from = player.Center;
            Vector2 to = primary.Center;
            string targetName = primary.FullName;

            foreach (NPC proxy in proxies) {
                NPC.HitInfo hit = BuildTransferredHit(proxy, damage, hitDirection, damageSource);
                proxy.StrikeNPC(hit);
            }
            BroadcastScapeDeathMessage(player, primary, damageSource, proxies.Length);

            //友善替死推进倍率；敌怪替死不推进
            if (isFriendly) {
                player.GetModPlayer<WraithPlayer>().AdvanceScapeMultiplier();
            }

            player.statLife = Math.Max(player.statLife, Math.Max(1, (int)(player.statLifeMax2 * 0.12f)));
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 75);
            player.GetModPlayer<WraithPlayer>().AddErosion(0.30f);
            player.GetModPlayer<WraithPlayer>().AddRevival(0.25f);
            player.AddBuff(ModContent.BuffType<Wraiths.Buffs.ScapeAfterburn>(), 60 * 12);

            if (VaultUtils.isServer) {
                WraithNet.SendScapeGhostFx(from, to, player.whoAmI, targetName);
                NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, player.whoAmI);
            }
            else {
                ScapeArmRenderer.Trigger(from, to);
            }
            return true;
        }

        /// <summary>
        /// 全服广播替死公告。单人直接显示；服务端通过 ChatHelper 广播。<br/>
        /// 复用 PlayerDeathReason.GetDeathText 提取原始致死文本，无死因时退化为仅含 NPC 名。
        /// </summary>
        private static void BroadcastScapeDeathMessage(Player player, NPC proxy
            , PlayerDeathReason damageSource, int victimCount = 1) {
            if (Main.dedServ && !VaultUtils.isServer) {
                return;
            }
            //多于1个受害者时显示首个名称+数量
            string primaryName = victimCount > 1
                ? $"{proxy.FullName} 等{victimCount}名生灵"
                : proxy.FullName;
            NetworkText causeTex = damageSource != null
                ? damageSource.GetDeathText(primaryName)
                : NetworkText.FromLiteral(primaryName);
            NetworkText msg = NetworkText.FromKey(
                WraithSystemText.ScapeGhostDeathBroadcast.Key,
                NetworkText.FromLiteral(primaryName),
                NetworkText.FromLiteral(player.name),
                causeTex);
            Color broadcast = new Color(200, 42, 52);
            if (VaultUtils.isServer) {
                ChatHelper.BroadcastChatMessage(msg, broadcast);
            }
            else {
                Main.NewText(msg.ToString(), broadcast);
            }
        }

        /// <summary>
        /// 客户端收到服务端替死结果包后调用（在 WraithNet 的包处理器中）。<br/>
        /// 成功时触发本地 FX；失败时挂延迟死亡，在下一帧 PostUpdate 执行。
        /// </summary>
        internal void ApplyScapeResult(bool success, Vector2 from, Vector2 to, string targetName) {
            ScapeGhostPending = false;
            if (success) {
                ScapeArmRenderer.Trigger(from, to);
            }
            else {
                //服务端未找到代理目标，延迟一帧执行真实死亡
                scapeGhostKillDeferred = true;
            }
        }

        /// <summary>
        /// 两阶段目标选取：<br/>
        /// 1. 扩展屏幕范围内优先找友善目标（城镇居民/友善生物），需满足当前倍率数量，不足则失败进入第2阶段。<br/>
        /// 2. 全局查找最近的敌怪，距离可压过优先级差异，单个目标，不推进倍率。<br/>
        /// 两者都找不到则返回 null（替死失败）。
        /// </summary>
        private static (NPC[] proxies, bool isFriendly) FindScapeTargetsFor(Player player) {
            int multiplier = player.GetModPlayer<WraithPlayer>().ScapeMultiplier;
            NPC[] friendly = CollectFriendlyTargets(player, multiplier);
            if (friendly != null) {
                return (friendly, true);
            }
            NPC enemy = FindBestEnemyTarget(player);
            if (enemy != null) {
                return ([enemy], false);
            }
            return (null, false);
        }

        /// <summary>屏幕范围扩大约1.5倍（≈2400px），按距离升序取最近的 count 个友善目标</summary>
        private const float FriendlyScapeRadius = 2400f;
        /// <summary>优先级单位距离权重（px）：各优先级差等价于此距离，越小越偏重距离</summary>
        private const float EnemyPriorityPx = 600f;

        private static NPC[] CollectFriendlyTargets(Player player, int count) {
            float radiusSq = FriendlyScapeRadius * FriendlyScapeRadius;
            //手动收集+排序，避免 LINQ 依赖
            NPC[] found = new NPC[64];
            float[] dists = new float[64];
            int n = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!CanReceiveScapeHit(npc) || !IsFriendlyScapeTarget(npc)) { continue; }
                float distSq = Vector2.DistanceSquared(player.Center, npc.Center);
                if (distSq > radiusSq) { continue; }
                if (n < found.Length) {
                    found[n] = npc;
                    dists[n] = distSq;
                    n++;
                }
            }
            if (n < count) { return null; }
            //插排取前 count 个（count≤32，n≤64，不需要 Array.Sort）
            for (int i = 0; i < count; i++) {
                int minIdx = i;
                for (int j = i + 1; j < n; j++) {
                    if (dists[j] < dists[minIdx]) { minIdx = j; }
                }
                (found[i], found[minIdx]) = (found[minIdx], found[i]);
                (dists[i], dists[minIdx]) = (dists[minIdx], dists[i]);
            }
            NPC[] result = new NPC[count];
            Array.Copy(found, result, count);
            return result;
        }

        private static bool IsFriendlyScapeTarget(NPC npc)
            => npc.townNPC || npc.CountsAsACritter || npc.friendly;

        /// <summary>
        /// 距离加权选最近敌怪：score = enemyPriority * EnemyPriorityPx + distance。<br/>
        /// 无友善目标时才会调用；中性生物优先，boss 最后。
        /// </summary>
        private static NPC FindBestEnemyTarget(Player player) {
            NPC best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!CanReceiveScapeHit(npc) || IsFriendlyScapeTarget(npc)) { continue; }
                float dist = Vector2.Distance(player.Center, npc.Center);
                float score = GetEnemyPriority(npc) * EnemyPriorityPx + dist;
                if (score < bestScore) {
                    bestScore = score;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>中性=0, 普通敌=1, boss=2（评分越低越优先）</summary>
        private static int GetEnemyPriority(NPC npc) {
            if (npc.damage <= 0 && !npc.boss) { return 0; }
            return npc.boss ? 2 : 1;
        }

        private static bool CanReceiveScapeHit(NPC npc)
            => npc != null && npc.active && npc.life > 0 && npc.lifeMax > 1
                && !npc.dontTakeDamage && !npc.immortal && npc.type != NPCID.TargetDummy;

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