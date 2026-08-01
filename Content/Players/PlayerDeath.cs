using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
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
        private int scapeGhostPendingTicks;

        //服务端只消费原版 PlayerHurtV2 产生的一次性致死事件
        private bool serverLethalHurtPending;
        private Player.HurtInfo serverLethalHurt;

        //RuleKill 先行包与原版 PlayerDeathV2 之间的短时死亡通行证
        private int ruleDeathPermitTicks;
        private const int ScapeGhostRequestTimeout = 60 * 5;

        public override void ResetEffects() {
            Doomed = false;
            scapeUsedThisTick = false;
            if (ruleDeathPermitTicks > 0) {
                ruleDeathPermitTicks--;
            }
        }

        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                ProcessServerLethalHurt();
                return;
            }
            if (!VaultUtils.isClient || !ScapeGhostPending) {
                return;
            }
            if (++scapeGhostPendingTicks >= ScapeGhostRequestTimeout) {
                //死亡裁定始终属于服务器；超时只结束本地等待，不能制造两端生死分叉
                ClearScapeSession();
                Player.statLife = Math.Max(Player.statLife, 1);
            }
        }

        public override bool? On_PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Doomed || ruleDeathPermitTicks > 0) {
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
            if (VaultUtils.isClient && Player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (VaultUtils.isClient && ScapeGhostPending) {
                Player.statLife = Math.Max(Player.statLife, 1);
                return true;
            }
            if (scapeUsedThisTick || !HasScapeGhostContract(Player)) {
                return false;
            }
            scapeUsedThisTick = true;

            if (VaultUtils.isClient) {
                ScapeGhostPending = true;
                scapeGhostPendingTicks = 0;
                Player.statLife = Math.Max(Player.statLife, 1);
                return true;
            }

            //服务端从原版 PlayerHurtV2 记录中执行；不能在 PlayerDeathV2 处理器里取消后仍被原版广播死亡
            if (VaultUtils.isServer) {
                return false;
            }

            //单人直接权威执行
            return ExecuteScapeGhostAuthority(Player, damage, hitDirection, damageSource);
        }

        /// <summary>服务端在原版 Hurt 扣血前登记一次经原版协议处理的致死事件。</summary>
        internal void NoteServerLethalHurt(Player.HurtInfo info) {
            if (!VaultUtils.isServer || serverLethalHurtPending || Player.dead
                || Player.statLife <= 0 || info.Damage < Player.statLife
                || !HasScapeGhostContract(Player)) {
                return;
            }
            serverLethalHurt = info;
            serverLethalHurtPending = true;
        }

        private void ProcessServerLethalHurt() {
            if (!serverLethalHurtPending) {
                return;
            }
            Player.HurtInfo info = serverLethalHurt;
            serverLethalHurtPending = false;
            serverLethalHurt = default;
            if (Player.dead) {
                return;
            }

            bool escaped = ExecuteScapeGhostAuthority(Player, info.Damage, info.HitDirection
                , info.DamageSource);
            if (escaped) {
                return;
            }

            WraithRegistry.TryGet("ScapeGhost", out WraithDefinition definition);
            if (definition != null) {
                WraithLethality.Kill(Player, definition, WraithSystemText.ScapeGhostNoTarget);
            }
        }

        /// <summary>
        /// 权威端替死执行。单人直接调用；服务端由 WraithNet.HandleScapeGhostRequest 调用。
        /// </summary>
        internal static bool ExecuteScapeGhostAuthority(Player player, double damage, int hitDirection
            , PlayerDeathReason damageSource) {
            if (VaultUtils.isClient || !HasScapeGhostContract(player)) {
                return false;
            }
            var (proxies, isFriendly) = FindScapeTargetsFor(player);
            if (proxies == null) {
                return false;
            }

            NPC primary = proxies[0];
            Vector2 from = player.Center;
            Vector2 to = primary.Center;
            string targetName = primary.FullName;

            foreach (NPC proxy in proxies) {
                if (!CanReceiveScapeHit(proxy)) {
                    return false;
                }
            }
            foreach (NPC proxy in proxies) {
                NPC.HitInfo hit = BuildTransferredHit(proxy, damage, hitDirection, damageSource);
                proxy.StrikeNPC(hit);
                if (VaultUtils.isServer) {
                    NetMessage.SendStrikeNPC(proxy, hit);
                }
            }
            foreach (NPC proxy in proxies) {
                //CheckDead/阶段转换可以拒绝死亡；未真正失活就不能提交替死成功
                if (proxy.active) {
                    return false;
                }
            }
            BroadcastScapeDeathMessage(player, primary, damageSource, proxies.Length);

            //友善替死推进倍率；敌怪替死不推进
            if (isFriendly) {
                player.GetModPlayer<WraithPlayer>().AdvanceScapeMultiplier();
            }

            WraithPlayer wraithPlayer = player.GetModPlayer<WraithPlayer>();
            bool revivalKilled = wraithPlayer.AddRevival(0.25f);
            if (!revivalKilled) {
                player.statLife = Math.Max(player.statLife, Math.Max(1, (int)(player.statLifeMax2 * 0.12f)));
                player.immune = true;
                player.immuneTime = Math.Max(player.immuneTime, 75);
                wraithPlayer.AddErosion(0.30f);
                int afterburnType = ModContent.BuffType<Wraiths.Buffs.ScapeAfterburn>();
                const int afterburnTime = 60 * 12;
                player.AddBuff(afterburnType, afterburnTime);
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.AddPlayerBuff, -1, -1, null, player.whoAmI
                        , afterburnType, afterburnTime);
                }
            }

            if (VaultUtils.isServer) {
                WraithNet.SendScapeGhostFx(from, to, player.whoAmI, targetName, revivalKilled);
                if (!revivalKilled) {
                    NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, player.whoAmI);
                }
            }
            else {
                ScapeArmRenderer.Trigger(from, to);
            }
            return true;
        }

        private static bool HasScapeGhostContract(Player player) {
            if (player == null || !player.active) {
                return false;
            }
            //替死鬼是鬼切出厂契约；资格锚定实际物品类型，客户端可写的进度簿不参与服侧裁定
            foreach (Item item in player.inventory) {
                if (item != null && !item.IsAir && item.type == OnikiriOverride.ID) {
                    return true;
                }
            }
            return false;
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
        /// 客户端收到服务端替死成功包后调用（在 WraithNet 的包处理器中）。
        /// </summary>
        internal void ApplyScapeSuccess(Vector2 from, Vector2 to, string targetName
            , bool revivalKilled) {
            ClearScapeSession();
            if (!revivalKilled) {
                Player.GetModPlayer<WraithPlayer>().AddErosion(0.30f);
            }
            ScapeArmRenderer.Trigger(from, to);
            string name = string.IsNullOrWhiteSpace(targetName)
                ? WraithSystemText.ScapeGhostUnknownTarget.Value : targetName;
            VaultUtils.Text(WraithSystemText.ScapeGhostActivated.Format(name), new Color(178, 34, 44));
        }

        internal void PrepareRuleDeath() {
            ClearScapeSession();
            serverLethalHurtPending = false;
            serverLethalHurt = default;
            ruleDeathPermitTicks = 120;
            Doomed = true;
        }

        internal void ClearScapeSession() {
            ScapeGhostPending = false;
            scapeGhostPendingTicks = 0;
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
            NPC[] found = new NPC[Main.maxNPCs];
            float[] dists = new float[Main.maxNPCs];
            int n = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!CanReceiveScapeHit(npc) || !IsFriendlyScapeTarget(npc)) { continue; }
                float distSq = Vector2.DistanceSquared(player.Center, npc.Center);
                if (distSq > radiusSq) { continue; }
                found[n] = npc;
                dists[n] = distSq;
                n++;
            }
            if (n < count) { return null; }
            //插排取前 count 个（count≤32，n≤Main.maxNPCs，不需要全量排序）
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
            => npc.townNPC || npc.CountsAsACritter;

        /// <summary>
        /// 距离加权选最近敌怪：score = enemyPriority * EnemyPriorityPx + distance。<br/>
        /// 无友善目标时才会调用；中性生物优先，boss 最后。
        /// </summary>
        private static NPC FindBestEnemyTarget(Player player) {
            NPC best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!CanReceiveScapeHit(npc) || npc.friendly || IsFriendlyScapeTarget(npc)
                    || !npc.CanBeChasedBy(player)) { continue; }
                float dist = Vector2.Distance(player.Center, npc.Center);
                float score = GetEnemyPriority(npc) * EnemyPriorityPx + dist;
                if (score < bestScore) {
                    bestScore = score;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>中性=0, 普通敌=1（Boss 级目标已统一排除）</summary>
        private static int GetEnemyPriority(NPC npc) {
            return npc.damage <= 0 ? 0 : 1;
        }

        private static bool CanReceiveScapeHit(NPC npc)
            => npc != null && npc.active && npc.life > 0 && npc.lifeMax > 1
                && !npc.dontTakeDamage && !npc.immortal && !npc.SpawnedFromStatue
                && npc.realLife < 0 && npc.type != NPCID.TargetDummy
                && !NPCID.Sets.ProjectileNPC[npc.type]
                && !NPCID.Sets.PositiveNPCTypesExcludedFromDeathTally[npc.type]
                && !NpcGroupHelper.IsBossTier(npc);

        /// <summary>
        /// 将 PlayerDeathReason 中仍存活的致因实体投影成 NPC HitInfo。<br/>
        /// 保留弹幕伤害类型、基础伤害、击退与袭击方向；无法解析时退化为最终致死伤害。
        /// </summary>
        internal static NPC.HitInfo BuildTransferredHit(NPC proxy, double damage, int hitDirection
            , PlayerDeathReason damageSource) {
            int receivedDamage = Math.Max(1, (int)Math.Min(Math.Ceiling(damage), int.MaxValue));
            int sourceDamage = receivedDamage;
            int transferredDamage = Math.Max(receivedDamage, proxy.life);
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
