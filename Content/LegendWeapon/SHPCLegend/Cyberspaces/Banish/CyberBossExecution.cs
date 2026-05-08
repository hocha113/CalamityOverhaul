using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>
    /// Boss执行会话管理
    /// <br/>对Boss级目标启动后，会在持续时间内分波次召唤大量 <see cref="CyberExecutionBoltProj"/> 进行高伤打击
    /// <br/>不会抹除Boss，伤害根据当前持有的SHPC面板与改件加成实时计算
    /// </summary>
    internal class CyberBossExecution : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>
        /// 单次执行总时长（帧）
        /// </summary>
        public const int ExecutionDuration = 150;

        /// <summary>
        /// 单次执行消耗的RAM
        /// </summary>
        public const int RamCostPerCast = 12;

        /// <summary>
        /// 每次执行的目标主雷数，最多劈五下
        /// </summary>
        private const int TargetBoltCount = 5;

        /// <summary>
        /// 伤害最终倍率（基于SHPC面板伤害*改件DamageMul后再放大）
        /// </summary>
        private const float DamageMultiplier = 6f;

        public static readonly List<ExecutionEntry> ActiveExecutions = [];

        public static bool IsExecuting(int npcIndex) {
            for (int i = 0; i < ActiveExecutions.Count; i++) {
                if (ActiveExecutions[i].NpcIndex == npcIndex) return true;
            }
            return false;
        }

        /// <summary>
        /// 判定NPC是否属于Boss级目标，包含直接boss、应被计为boss的类型，以及群组任一成员为boss的情况
        /// </summary>
        public static bool IsBossTier(NPC npc) {
            if (npc == null || !npc.active) return false;
            if (npc.boss) return true;
            if (NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) return true;
            //群组判定：蠕虫体节本身npc.boss为false，但realLife指向的实际boss体上为true
            int rl = npc.realLife;
            if (rl >= 0 && rl < Main.maxNPCs) {
                NPC anchor = Main.npc[rl];
                if (anchor.active && (anchor.boss || NPCID.Sets.ShouldBeCountedAsBoss[anchor.type])) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 启动Boss执行打击
        /// </summary>
        public static void StartExecution(int npcIndex, Player owner) {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
            if (IsExecuting(npcIndex)) return;

            NPC npc = Main.npc[npcIndex];
            if (!npc.active) return;

            int damage = ResolveExecutionDamage(owner);

            ActiveExecutions.Add(new ExecutionEntry {
                NpcIndex = npcIndex,
                Timer = 0,
                Damage = damage,
                OwnerWho = owner?.whoAmI ?? 255,
                Seed = Main.rand.NextFloat(),
            });

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Thunder with {
                    Volume = 0.7f,
                    Pitch = -0.4f,
                }, npc.Center);
                SoundEngine.PlaySound(CWRSound.Fault with {
                    Volume = 0.6f,
                    Pitch = 0.2f,
                }, npc.Center);
            }
        }

        /// <summary>
        /// 计算执行雷的单发伤害：取本地玩家库存中等级最高的SHPC，按面板*改件DamageMul再乘 <see cref="DamageMultiplier"/>
        /// </summary>
        private static int ResolveExecutionDamage(Player owner) {
            int baseDamage = SHPCOverride.GetStartDamage;
            if (owner != null) {
                Item bestItem = null;
                int bestLevel = -1;
                for (int i = 0; i < owner.inventory.Length; i++) {
                    Item it = owner.inventory[i];
                    if (it == null || it.IsAir) continue;
                    if (it.type != SHPCOverride.ID) continue;
                    int lv = SHPCOverride.GetLevel(it);
                    if (lv > bestLevel) {
                        bestLevel = lv;
                        bestItem = it;
                    }
                }
                if (bestItem != null) {
                    baseDamage = SHPCOverride.GetOnDamage(bestItem);
                }
            }

            ShootContext ctx = SHPCModificationSystem.Resolve(owner);
            float scaled = baseDamage * Math.Max(ctx.DamageMul, 0.1f) * DamageMultiplier;
            int final = (int)scaled;
            if (final < 1) final = 1;
            return final;
        }

        public static void Update() {
            for (int i = ActiveExecutions.Count - 1; i >= 0; i--) {
                ExecutionEntry entry = ActiveExecutions[i];
                NPC npc = Main.npc[entry.NpcIndex];
                if (!npc.active) {
                    ActiveExecutions.RemoveAt(i);
                    continue;
                }

                //多人语义：仅由"发起者"客户端真正生成执行雷弹幕，避免每个客户端都各自再 spawn 一遍
                //其它端只推进 Timer 和 IsExecuting 状态，让放逐结束判定在所有端一致
                bool authoritative = Main.netMode == NetmodeID.SinglePlayer
                    || entry.OwnerWho == Main.myPlayer;
                if (authoritative) {
                    TickSpawnBolts(entry, npc);
                }

                entry.Timer++;
                if (entry.Timer >= ExecutionDuration) {
                    ActiveExecutions.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 收到远端 Boss 执行启动广播：在本机也加入对应 ActiveExecutions 记录，
        /// 让 IsExecuting 判定与放逐 / 冻结的过滤一致；仅发起者客户端会真正 spawn 雷击
        /// <br/>当前 <see cref="CyberBanish.Update"/> 在所有端都会触发 <see cref="StartExecution"/>，
        /// 因此该接口暂时不被实际使用，保留是为了对齐其它子系统的同步形态以备扩展
        /// </summary>
        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            int npcIdx = reader.ReadUInt16();
            int ownerWho = reader.ReadByte();
            int damage = reader.ReadInt32();
            float seed = reader.ReadSingle();
            if (npcIdx < 0 || npcIdx >= Main.maxNPCs) return;
            if (IsExecuting(npcIdx)) return;
            ActiveExecutions.Add(new ExecutionEntry {
                NpcIndex = npcIdx,
                Timer = 0,
                Damage = damage,
                OwnerWho = ownerWho,
                Seed = seed,
            });
        }

        /// <summary>
        /// 按时间计划在Boss附近召唤天雷，雷间隔大致均匀但带轻微随机抖动以避免节奏机械
        /// </summary>
        private static void TickSpawnBolts(ExecutionEntry entry, NPC npc) {
            if (npc.realLife > 0) {
                return;//排除子实体
            }
            //根据剩余进度决定本帧应已生成数量，弥补到该数量为止（每帧最多生成2发）
            float progress = (float)entry.Timer / ExecutionDuration;
            //0.95是为了把所有雷压到前95%生命周期内打完，最后一截留作收尾
            int expected = (int)(progress / 0.92f * TargetBoltCount);
            if (expected > TargetBoltCount) expected = TargetBoltCount;

            int spawnedThisFrame = 0;
            while (entry.SpawnedCount < expected && spawnedThisFrame < 2) {
                SpawnSingleBolt(entry, npc);
                entry.SpawnedCount++;
                spawnedThisFrame++;
            }
        }

        private static void SpawnSingleBolt(ExecutionEntry entry, NPC npc) {
            //从Boss外围随机点向内劈：起点距离Boss 600~1100像素
            float incomingAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            float startDist = Main.rand.NextFloat(600f, 1100f);
            //雷的"路径起点"在外围，路径角度沿incomingAngle反向（朝向Boss）
            //incomingAngle定义的是从Boss出发到起点的方向
            Vector2 startPos = npc.Center + incomingAngle.ToRotationVector2() * startDist;
            //轻微抖动让起点不过于规整
            startPos += Main.rand.NextVector2Circular(60f, 60f);
            float pathAngle = (npc.Center - startPos).ToRotation();
            //再加点随机摆动
            pathAngle += Main.rand.NextFloat(-0.18f, 0.18f);

            //短延迟使多发雷错开，营造连续轰击节奏
            int delay = Main.rand.Next(0, 5);

            EntitySource_Misc source = new EntitySource_Misc("CyberBossExecution");
            int idx = Projectile.NewProjectile(source, startPos, Vector2.Zero,
                ModContent.ProjectileType<CyberExecutionBoltProj>(),
                entry.Damage, 4f, entry.OwnerWho,
                ai0: pathAngle,
                ai1: delay,
                ai2: entry.NpcIndex);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[0] = 0f;
            }
        }

        public static void Reset() {
            ActiveExecutions.Clear();
        }
    }

    internal class ExecutionEntry
    {
        public int NpcIndex;
        public int Timer;
        public int SpawnedCount;
        public int Damage;
        public int OwnerWho;
        public float Seed;

        public float Progress => (float)Timer / CyberBossExecution.ExecutionDuration;
    }
}
