using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Destroyer
{
    /// <summary>
    /// 探针矩阵核：残酷毁灭者遗物。展开探针无人机编队环绕索敌，
    /// 激光命中同一目标累积标定，标定满格呼叫轨道贯穿死光
    /// </summary>
    internal class ProbeMatrixCore : BaseBrutalRelic
    {
        /// <summary>编队规模</summary>
        internal const int ProbeCount = 5;
        /// <summary>探针激光基伤(通用伤害加成)</summary>
        internal const int BoltDamage = 115;
        /// <summary>轨道打击基伤(贯穿柱，单目标可吃多跳)</summary>
        internal const int StrikeDamage = 1150;
        /// <summary>标定满格所需命中层数</summary>
        internal const int DesignationNeed = 8;
        /// <summary>探针被击落后的重构延迟(2.5秒，与Tooltip口径一致)</summary>
        internal const int RespawnDelay = 150;
        /// <summary>轨道打击冷却(自呼叫起算)</summary>
        internal const int StrikeCooldown = 180;
        /// <summary>编队索敌半径</summary>
        internal const float AcquireRange = 1100f;
        /// <summary>目标脱离半径(超出即放弃标定)</summary>
        internal const float DropRange = 1500f;

        public override void SetDefaults() {
            base.SetDefaults();
            //同期Boss掉落物约12~20金，取4倍档
            Item.value = Item.buyPrice(0, 60, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            ProbeMatrixPlayer mp = player.GetModPlayer<ProbeMatrixPlayer>();
            mp.MatrixActive = true;
            mp.SourceItem = Item;
        }
    }

    /// <summary>
    /// 编队管理与标定结算，全部状态在实例字段。
    /// 编队维护/标定/呼叫打击只在所有者端执行，远端经弹幕同步看到结果；
    /// 标定进度经队长探针 ai[2] 量化下发，供各端画标定光标
    /// </summary>
    internal class ProbeMatrixPlayer : ModPlayer
    {
        /// <summary>本帧饰品生效，物品钩子逐帧点亮</summary>
        public bool MatrixActive;
        /// <summary>本帧装备的物品实例，仅作生成源</summary>
        public Item SourceItem;

        /// <summary>当前标定目标 whoAmI，-1 无</summary>
        public int DesignatedTarget = -1;
        //目标类型，用于槽位复用校验
        private int designatedType = -1;
        /// <summary>已累积标定层数</summary>
        public int DesignationStacks;
        /// <summary>打击冷却剩余</summary>
        public int StrikeCooldownTimer;
        //无新命中时的标定衰减计时
        private int decayTimer;

        //逐槽位重构倒计时
        private readonly int[] respawnTimers = new int[ProbeMatrixCore.ProbeCount];
        //上帧槽位在场记录，用于捕获"被击落"沿
        private readonly bool[] slotAlive = new bool[ProbeMatrixCore.ProbeCount];
        //部署节流，防五枚同帧齐出
        private int spawnGate;

        public override void ResetEffects() {
            MatrixActive = false;
            SourceItem = null;
        }

        public override void UpdateDead() {
            //死亡清编队记账，复活立即重新部署
            ClearSquadState();
        }

        private void ClearSquadState() {
            for (int i = 0; i < respawnTimers.Length; i++) {
                respawnTimers[i] = 0;
                slotAlive[i] = false;
            }
            DesignatedTarget = -1;
            designatedType = -1;
            DesignationStacks = 0;
            decayTimer = 0;
            spawnGate = 0;
        }

        public override void PostUpdate() {
            //编队维护/标定/打击呼叫全部只在所有者端
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (!MatrixActive) {
                if (DesignatedTarget != -1 || DesignationStacks > 0) {
                    ClearSquadState();
                }
                if (StrikeCooldownTimer > 0) {
                    StrikeCooldownTimer--;
                }
                return;
            }

            if (StrikeCooldownTimer > 0) {
                StrikeCooldownTimer--;
            }

            UpdateDesignationTarget();
            UpdateDesignationDecay();
            UpdateSquad();
            TryCallStrike();
        }

        #region 编队维护

        private void UpdateSquad() {
            Span<bool> aliveNow = stackalloc bool[ProbeMatrixCore.ProbeCount];
            int droneType = ModContent.ProjectileType<ProbeDroneProj>();
            int leadSlot = -1;
            float progress = MathHelper.Clamp(DesignationStacks / (float)ProbeMatrixCore.DesignationNeed, 0f, 1f);

            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != droneType || proj.owner != Player.whoAmI) {
                    continue;
                }
                int slot = (int)proj.ai[0];
                if (slot < 0 || slot >= aliveNow.Length) {
                    continue;
                }
                aliveNow[slot] = true;
                if (leadSlot < 0 || slot < leadSlot) {
                    leadSlot = slot;
                }

                //目标写进探针 ai[1]，变更才发包
                if ((int)proj.ai[1] != DesignatedTarget) {
                    proj.ai[1] = DesignatedTarget;
                    proj.netUpdate = true;
                }
            }

            //标定进度量化写进队长 ai[2]，远端凭它画光标
            if (leadSlot >= 0) {
                float quantized = (float)Math.Round(progress * 8f) / 8f;
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type != droneType || proj.owner != Player.whoAmI) {
                        continue;
                    }
                    float want = (int)proj.ai[0] == leadSlot ? quantized : 0f;
                    if (proj.ai[2] != want) {
                        proj.ai[2] = want;
                        proj.netUpdate = true;
                    }
                }
            }

            if (spawnGate > 0) {
                spawnGate--;
            }

            for (int slot = 0; slot < aliveNow.Length; slot++) {
                if (aliveNow[slot]) {
                    slotAlive[slot] = true;
                    continue;
                }

                //在场→缺席的沿视为被击落，压重构延迟
                if (slotAlive[slot]) {
                    slotAlive[slot] = false;
                    respawnTimers[slot] = ProbeMatrixCore.RespawnDelay;
                    continue;
                }

                if (respawnTimers[slot] > 0) {
                    respawnTimers[slot]--;
                    continue;
                }

                if (spawnGate > 0) {
                    continue;
                }
                spawnGate = 9;
                SpawnProbe(slot);
            }
        }

        private void SpawnProbe(int slot) {
            IEntitySource source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_Misc("ProbeMatrixCore");
            Vector2 spawnPos = Player.Center + new Vector2(Player.direction * -14f, -22f)
                + Main.rand.NextVector2Circular(8f, 8f);
            Projectile.NewProjectile(source, spawnPos, Vector2.Zero,
                ModContent.ProjectileType<ProbeDroneProj>(), 0, 0f, Player.whoAmI,
                slot, DesignatedTarget, 0f);
        }

        #endregion

        #region 标定

        private void UpdateDesignationTarget() {
            //既有目标校验：死亡/换型/超脱离半径都作废
            if (DesignatedTarget >= 0) {
                NPC current = Main.npc[DesignatedTarget];
                bool valid = current.active && current.type == designatedType
                    && current.CanBeChasedBy()
                    && Player.Distance(current.Center) < ProbeMatrixCore.DropRange;
                if (valid) {
                    return;
                }
                DesignatedTarget = -1;
                designatedType = -1;
                DesignationStacks = 0;
            }

            //重新索敌，Boss优先
            int best = -1;
            float bestScore = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Player.Distance(npc.Center);
                if (dist > ProbeMatrixCore.AcquireRange) {
                    continue;
                }
                float score = dist - (npc.boss ? 600f : 0f);
                if (score < bestScore) {
                    bestScore = score;
                    best = npc.whoAmI;
                }
            }

            if (best >= 0) {
                DesignatedTarget = best;
                designatedType = Main.npc[best].type;
                DesignationStacks = 0;
                decayTimer = 0;
            }
        }

        private void UpdateDesignationDecay() {
            if (DesignationStacks <= 0) {
                return;
            }
            decayTimer++;
            //75帧无新命中后，每20帧掉一层
            if (decayTimer > 75 && (decayTimer - 75) % 20 == 0) {
                DesignationStacks--;
            }
        }

        /// <summary>探针激光命中入账，仅命中标定目标时累积</summary>
        public void RegisterBoltHit(NPC npc) {
            if (!MatrixActive || npc == null || !npc.active) {
                return;
            }
            if (npc.whoAmI != DesignatedTarget || npc.type != designatedType) {
                return;
            }
            if (DesignationStacks < ProbeMatrixCore.DesignationNeed) {
                DesignationStacks++;
            }
            decayTimer = 0;
        }

        private void TryCallStrike() {
            if (StrikeCooldownTimer > 0 || DesignationStacks < ProbeMatrixCore.DesignationNeed) {
                return;
            }
            if (DesignatedTarget < 0) {
                return;
            }
            NPC target = Main.npc[DesignatedTarget];
            if (!target.active || target.type != designatedType) {
                return;
            }

            IEntitySource source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_Misc("ProbeMatrixCore");
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(ProbeMatrixCore.StrikeDamage);
            Vector2 aim = target.Center + target.velocity * 14f;
            Projectile.NewProjectile(source, aim, Vector2.Zero,
                ModContent.ProjectileType<ProbeOrbitalStrike>(), damage, 10f, Player.whoAmI,
                target.whoAmI);

            //呼叫后标定重置进冷却
            DesignationStacks = 0;
            decayTimer = 0;
            StrikeCooldownTimer = ProbeMatrixCore.StrikeCooldown;
        }

        #endregion
    }
}
