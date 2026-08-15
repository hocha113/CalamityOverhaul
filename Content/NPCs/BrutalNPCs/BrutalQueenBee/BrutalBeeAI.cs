using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee
{
    /// <summary>
    /// 编队蜂接管(Bee)：仅当 ai[3]==SwarmDirector.BeeMarker 时生效，普通蜜蜂走原版AI<br/>
    /// ai[0]=持久槽位 ai[1]=模式(0编队/1掷镖) ai[2]=女王whoAmI ai[3]=标记<br/>
    /// localAI[0]=属性已初始化 localAI[1]=镖向角 localAI[2]=镖计时<br/>
    /// 编队目标 = 确定性函数(女王同步ai+编队时钟+槽位)，全端一致，服务端周期纠偏
    /// </summary>
    internal class BrutalBeeAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.Bee;

        /// <summary>掷镖全程帧数</summary>
        private const int DartDuration = 52;

        public override bool? CanCWROverride() {
            return null;
        }

        protected bool IsMarked => npc.ai[3] == SwarmDirector.BeeMarker;

        public override bool AI() {
            if (!IsMarked) {
                return true;
            }
            return FormationBeeAI(npc);
        }

        public override bool CheckActive() => !IsMarked;

        /// <summary>编队蜂主逻辑，Bee/BeeSmall共用</summary>
        internal static bool FormationBeeAI(NPC npc) {
            //找女王，失效则解除标记回落原版
            int queenWho = (int)npc.ai[2];
            NPC queen = queenWho >= 0 && queenWho < Main.maxNPCs ? Main.npc[queenWho] : null;
            BrutalQueenBeeAI queenAI = null;
            if (queen != null && queen.active && queen.type == NPCID.QueenBee) {
                queen.TryGetOverride(out queenAI);
            }
            if (queenAI == null || queenAI.Swarm == null) {
                if (!VaultUtils.isClient) {
                    npc.ai[3] = 0f;
                    npc.EncourageDespawn(90);
                    npc.netUpdate = true;
                }
                return true;
            }

            InitStatsOnce(npc);
            npc.timeLeft = 600;

            //投技窗内蜂群是"茧"不是刀：收网/裹茧期接触伤归零，爆散拍恢复(每帧声明，全端确定性一致)
            npc.damage = queenAI.Machine?.CurrentState is States.QBSwarmLiftState lift && lift.BeesHarmless
                ? 0 : npc.defDamage;

            //服务端错帧周期纠偏：确定性推演的兜底通道
            if (!VaultUtils.isClient && (Main.GameUpdateCount + npc.whoAmI) % 30 == 0) {
                npc.netUpdate = true;
            }

            SwarmDirector director = queenAI.Swarm;
            int slot = director.GetEffectiveSlot(npc.whoAmI);

            //掷镖模式
            if (npc.ai[1] == 1f) {
                UpdateDart(npc, queen);
                return false;
            }

            //编队模式：查询本槽位掷镖令
            if (slot >= 0 && director.TryGetDartOrder(slot, out float dirRot, out float speed, out int steer)) {
                npc.ai[1] = 1f;
                npc.localAI[1] = steer;
                npc.localAI[2] = 0f;
                npc.velocity = dirRot.ToRotationVector2() * speed;
                //掷镖出手轻响，密集时靠MaxInstances压
                if (!VaultUtils.isServer) {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item17 with {
                        Volume = 0.32f,
                        Pitch = 0.5f,
                        MaxInstances = 3
                    }, npc.Center);
                }
                return false;
            }

            //编队巡航
            Vector2 slotTarget = slot >= 0
                ? director.GetSlotTarget(slot, director.Bees.Count)
                : director.Anchor;
            SteerToward(npc, slotTarget, director);

            //远距落队直接归位(服务端裁定)
            if (!VaultUtils.isClient && npc.Distance(slotTarget) > 2200f) {
                npc.Center = slotTarget;
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
            }

            UpdatePresentation(npc, director, slot);
            return false;
        }

        /// <summary>首帧属性覆写(全端同函数同值)</summary>
        private static void InitStatsOnce(NPC npc) {
            if (npc.localAI[0] == 1f) {
                return;
            }
            npc.localAI[0] = 1f;

            bool small = npc.type == NPCID.BeeSmall;
            int newLifeMax = small ? 28 : 45;
            //满血新蜂直接换血量上限；中途入场的残血蜂只调上限不回血
            bool wasFull = npc.life >= npc.lifeMax;
            npc.lifeMax = newLifeMax;
            npc.life = wasFull ? newLifeMax : Math.Min(npc.life, newLifeMax);
            npc.knockBackResist = 0.2f;
            npc.noTileCollide = true;
            npc.noGravity = true;
            //槽位哈希微差体型，避免克隆感
            float slotSeed = SwarmDirector.Hash01((int)npc.ai[0] * 5 + 11);
            npc.scale = (small ? 0.9f : 1f) * (0.92f + slotSeed * 0.24f);
        }

        /// <summary>编队巡航转向：弹簧靠拢+落后追赶，SnapBoost整队提速</summary>
        private static void SteerToward(NPC npc, Vector2 target, SwarmDirector director) {
            Vector2 toTarget = target - npc.Center;
            float dist = toTarget.Length();
            float boost = director.SnapBoost;
            float maxSpeed = 22f * boost + MathHelper.Clamp(dist / 26f, 0f, 18f);
            float accel = MathHelper.Clamp(0.1f * boost, 0.06f, 0.36f);

            Vector2 desired = dist > 0.01f ? toTarget.SafeNormalize(Vector2.Zero) * MathHelper.Clamp(dist * 0.11f, 1.5f, maxSpeed) : Vector2.Zero;
            npc.velocity = Vector2.Lerp(npc.velocity, desired, accel);

            //贴位后随波微振，蜂群永远不静止
            if (dist < 26f) {
                float jitterSeed = npc.whoAmI * 2.13f;
                npc.velocity += new Vector2(
                    (float)Math.Sin(director.Clock * 0.31f + jitterSeed),
                    (float)Math.Cos(director.Clock * 0.27f + jitterSeed * 1.7f)) * 0.22f;
            }

            FaceAlong(npc);
        }

        /// <summary>掷镖动力学：一帧置位出手→短暂微追踪→直线贯穿→回归编队</summary>
        private static void UpdateDart(NPC npc, NPC queen) {
            npc.localAI[2] += 1f;
            float t = npc.localAI[2];

            int steerTime = (int)npc.localAI[1];
            if (t <= steerTime && queen.target >= 0 && queen.target < 255) {
                //出手初段朝女王锁定目标微弧修正(同步目标，全端一致)，之后不再追踪
                Player target = Main.player[queen.target];
                if (target.Alives()) {
                    float current = npc.velocity.ToRotation();
                    float desired = (target.Center - npc.Center).ToRotation();
                    npc.velocity = current.AngleTowards(desired, 0.03f).ToRotationVector2() * npc.velocity.Length();
                }
            }

            //镖体拖出微光
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_BeeGlint>(npc.Center - npc.velocity * 0.4f,
                    -npc.velocity * 0.05f, QueenBeeMotion.HoneyGold, 0.8f);
            }

            FaceAlong(npc);

            if (t >= DartDuration) {
                npc.ai[1] = 0f;
                npc.localAI[2] = 0f;
                npc.velocity *= 0.4f;
            }
        }

        /// <summary>朝速度取向</summary>
        private static void FaceAlong(NPC npc) {
            if (npc.velocity.X > 0.3f) {
                npc.direction = 1;
            }
            else if (npc.velocity.X < -0.3f) {
                npc.direction = -1;
            }
            npc.spriteDirection = npc.direction;
            npc.rotation = MathHelper.Clamp(npc.velocity.X * 0.04f, -0.5f, 0.5f);
        }

        /// <summary>本地表现：琥珀微光+稀疏翅闪</summary>
        private static void UpdatePresentation(NPC npc, SwarmDirector director, int slot) {
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(npc.Center, QueenBeeMotion.HoneyGold.ToVector3() * 0.14f);

            //辉光带强时蜂身零星金闪
            if (director.RibbonIntensity > 0.2f && slot >= 0) {
                int stagger = 26 + slot * 3 % 17;
                if ((int)director.Clock % stagger == slot % stagger) {
                    PRTLoader.NewParticle<PRT_BeeGlint>(npc.Center + Main.rand.NextVector2Circular(8f, 6f),
                        Vector2.Zero, QueenBeeMotion.HoneyGold * director.RibbonIntensity, 1f);
                }
            }
        }
    }

    /// <summary>编队蜂接管(BeeSmall)，逻辑同 Bee</summary>
    internal class BrutalBeeSmallAI : BrutalBeeAI
    {
        public override int TargetID => NPCID.BeeSmall;
    }
}
