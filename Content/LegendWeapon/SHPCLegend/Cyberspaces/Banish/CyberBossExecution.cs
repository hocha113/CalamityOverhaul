using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>Boss 放逐后的权威雷击会话</summary>
    internal partial class CyberBossExecution : ICWRLoader
    {
        public const int ExecutionDuration = 150;
        public const int RamCostPerCast = 12;

        private const int TargetBoltCount = 5;
        private const float DamageMultiplier = 6f;
        private const int MaxExecutionDamage = 10_000_000;

        public static readonly List<ExecutionEntry> ActiveExecutions = [];

        void ICWRLoader.UnLoadData() => Reset();

        public static bool IsExecuting(int npcIndex) {
            for (int i = 0; i < ActiveExecutions.Count; i++) {
                ExecutionEntry entry = ActiveExecutions[i];
                if (entry.NpcIndex == npcIndex && IsEntryResolved(entry)) {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsExecuting(NetworkNPCIdentity identity) {
            for (int i = 0; i < ActiveExecutions.Count; i++) {
                ExecutionEntry entry = ActiveExecutions[i];
                if (entry.Identity == identity && IsEntryResolved(entry)) {
                    return true;
                }
            }
            return false;
        }

        public static bool IsBossTier(NPC npc)
            => NpcGroupHelper.IsBossTier(npc);

        internal static bool StartExecution(long activationId,
            NetworkNPCIdentity identity, Player owner) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || activationId <= 0 || owner?.active != true || owner.dead
                || !identity.TryResolve(out NPC npc) || !IsBossTier(npc)) {
                return false;
            }
            ExecutionEntry existing = FindExecution(activationId);
            if (existing != null) {
                return existing.Identity == identity;
            }
            if (IsExecuting(identity)) {
                return false;
            }

            ExecutionEntry entry = new() {
                ActivationId = activationId,
                Identity = identity,
                Timer = 0,
                Damage = ResolveExecutionDamage(owner),
                OwnerWho = owner.whoAmI,
                Authoritative = true,
                Resolved = true,
            };
            ActiveExecutions.Add(entry);
            PlayExecutionStart(npc);
            if (Main.netMode == NetmodeID.Server) {
                SendExecutionApply(entry);
            }
            return true;
        }

        private static int ResolveExecutionDamage(Player owner) {
            int baseDamage = Math.Clamp(SHPCOverride.GetStartDamage,
                1, MaxExecutionDamage);
            Item bestItem = null;
            int bestLevel = -1;
            if (owner != null) {
                for (int i = 0; i < owner.inventory.Length; i++) {
                    Item item = owner.inventory[i];
                    if (item == null || item.IsAir
                        || item.type != SHPCOverride.ID) {
                        continue;
                    }
                    int level = SHPCOverride.GetLevel(item);
                    if (level > bestLevel) {
                        bestLevel = level;
                        bestItem = item;
                    }
                }
            }
            if (bestItem != null) {
                baseDamage = Math.Clamp(SHPCOverride.GetOnDamage(bestItem),
                    1, MaxExecutionDamage);
            }

            ShootContext context = SHPCModificationSystem.Resolve(owner);
            float multiplier = float.IsFinite(context.DamageMul)
                ? MathHelper.Clamp(context.DamageMul, 0.1f, 100f)
                : 1f;
            double scaled = baseDamage * (double)multiplier * DamageMultiplier;
            return (int)Math.Clamp(scaled, 1d, MaxExecutionDamage);
        }

        public static void Update() {
            PruneReleasedExecutions();
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                UpdateClientPresentation();
                return;
            }

            for (int i = ActiveExecutions.Count - 1; i >= 0; i--) {
                ExecutionEntry entry = ActiveExecutions[i];
                if (!IsValidOwner(entry.OwnerWho)
                    || Main.player[entry.OwnerWho]?.active != true
                    || Main.player[entry.OwnerWho].dead
                    || !entry.Identity.TryResolve(out NPC npc)) {
                    RemoveExecution(entry,
                        broadcast: Main.netMode == NetmodeID.Server);
                    continue;
                }

                TickSpawnBolts(entry, npc);
                entry.Timer = Math.Min(ExecutionDuration,
                    entry.Timer
                    + TimeGear.PullFrameAdvance(ref entry.TimerCarry));
                if (entry.Timer >= ExecutionDuration) {
                    RemoveExecution(entry,
                        broadcast: Main.netMode == NetmodeID.Server);
                }
            }
        }

        private static void UpdateClientPresentation() {
            for (int i = 0; i < ActiveExecutions.Count; i++) {
                ExecutionEntry entry = ActiveExecutions[i];
                entry.Timer = Math.Min(ExecutionDuration - 1,
                    entry.Timer
                    + TimeGear.PullFrameAdvance(ref entry.TimerCarry));
            }
        }

        private static void TickSpawnBolts(ExecutionEntry entry, NPC npc) {
            float progress = entry.Timer / (float)ExecutionDuration;
            int expected = Math.Clamp(
                (int)(progress / 0.92f * TargetBoltCount),
                0, TargetBoltCount);
            int spawnedThisFrame = 0;
            while (entry.SpawnedCount < expected && spawnedThisFrame < 2) {
                SpawnSingleBolt(entry, npc);
                entry.SpawnedCount++;
                spawnedThisFrame++;
            }
        }

        private static void SpawnSingleBolt(ExecutionEntry entry, NPC npc) {
            float incomingAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            float startDistance = Main.rand.NextFloat(600f, 1100f);
            Vector2 startPosition = npc.Center
                + incomingAngle.ToRotationVector2() * startDistance
                + Main.rand.NextVector2Circular(60f, 60f);
            float pathAngle = (npc.Center - startPosition).ToRotation()
                + Main.rand.NextFloat(-0.18f, 0.18f);
            int delay = Main.rand.Next(0, 5);

            IEntitySource source = new EntitySource_Misc("CyberBossExecution");
            int index = Projectile.NewProjectile(source, startPosition,
                Vector2.Zero,
                ModContent.ProjectileType<CyberExecutionBoltProj>(),
                entry.Damage, 4f, entry.OwnerWho,
                ai0: pathAngle, ai1: delay, ai2: entry.Identity.Index);
            if (index < 0 || index >= Main.maxProjectiles) {
                return;
            }
            Projectile projectile = Main.projectile[index];
            if (projectile.ModProjectile is CyberExecutionBoltProj bolt) {
                bolt.InitializeTarget(entry.Identity,
                    Main.rand.Next(1, int.MaxValue));
            }
            // 服务器不是弹幕 owner（myPlayer=255），NewProjectile 不会自动下发，
            // netUpdate 也会被原版 owner 门吞掉——目标身份写完后必须显式 SyncProjectile
            SyncProjectileFromServer(projectile);
        }

        /// <summary>
        /// 服务器代玩家生成的弹幕不会自动同步；权威端补发 MessageID.SyncProjectile
        /// </summary>
        private static void SyncProjectileFromServer(Projectile projectile) {
            if (Main.netMode == NetmodeID.Server && projectile?.active == true) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null,
                    projectile.whoAmI);
            }
        }

        private static void PlayExecutionStart(NPC npc) {
            if (Main.dedServ || npc?.active != true) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.Thunder with {
                Volume = 0.7f,
                Pitch = -0.4f,
            }, npc.Center);
            SoundEngine.PlaySound(CWRSound.Fault with {
                Volume = 0.6f,
                Pitch = 0.2f,
            }, npc.Center);
        }

        private static bool IsEntryResolved(ExecutionEntry entry)
            => entry != null && entry.Resolved
            && entry.Identity.TryResolve(out _);

        private static ExecutionEntry FindExecution(long activationId) {
            for (int i = 0; i < ActiveExecutions.Count; i++) {
                if (ActiveExecutions[i].ActivationId == activationId) {
                    return ActiveExecutions[i];
                }
            }
            return null;
        }

        private static void RemoveExecution(ExecutionEntry entry,
            bool broadcast) {
            if (entry == null) {
                return;
            }
            if (broadcast) {
                SendExecutionRelease(entry.ActivationId);
            }
            TimeControlReplicationSystem.Cancel<CyberBossExecution>(
                entry.ActivationId);
            ActiveExecutions.Remove(entry);
            RememberReleasedExecution(entry.ActivationId);
        }

        public static void Reset() {
            for (int i = ActiveExecutions.Count - 1; i >= 0; i--) {
                RemoveExecution(ActiveExecutions[i], broadcast: false);
            }
            ActiveExecutions.Clear();
            TimeControlReplicationSystem.CancelAll<CyberBossExecution>();
            ClearReleasedExecutions();
        }
    }

    internal sealed class ExecutionEntry
    {
        internal long ActivationId;
        internal NetworkNPCIdentity Identity;
        public int Timer;
        internal float TimerCarry;
        public int SpawnedCount;
        public int Damage;
        public int OwnerWho;
        internal bool Authoritative;
        internal bool Resolved;

        public int NpcIndex => Identity.Index;
        public float Progress => MathHelper.Clamp(
            Timer / (float)CyberBossExecution.ExecutionDuration, 0f, 1f);
    }
}
