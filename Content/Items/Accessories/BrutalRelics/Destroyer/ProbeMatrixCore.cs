using System;
using System.Collections.Generic;
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
        internal const int BoltDamage = 55;
        /// <summary>轨道打击基伤(贯穿柱，单目标至多两跳)</summary>
        internal const int StrikeDamage = 900;
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
    /// 标定进度经队长探针 ai[2] 量化下发，供各端画标定光标。
    /// 帧级缓存(队内广播/击落粗筛)只是读法优化，ai 写入与同步路径照旧
    /// </summary>
    internal class ProbeMatrixPlayer : ModPlayer
    {
        /// <summary>击落粗筛半径(px)，以玩家为心足以罩住编队活动圈</summary>
        private const float ThreatSpan = 340f;
        /// <summary>无标定目标时的重索敌节流(帧)</summary>
        private const int RetargetPeriod = 12;

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
        //重索敌节流计时(仅无目标时生效)
        private int retargetGate;

        //逐槽位重构倒计时
        private readonly int[] respawnTimers = new int[ProbeMatrixCore.ProbeCount];
        //上帧槽位在场记录，用于捕获"被击落"沿
        private readonly bool[] slotAlive = new bool[ProbeMatrixCore.ProbeCount];
        //部署节流，防五枚同帧齐出
        private int spawnGate;

        //—— 队内广播帧缓存：owner 由 UpdateSquad 顺手填，远端由首个探针填 ——
        /// <summary>广播缓存填充帧(GameUpdateCount)</summary>
        internal uint SquadCacheFrame;
        /// <summary>全队最高量化标定进度(缓存)</summary>
        internal float SquadProgressCache;
        /// <summary>队长槽位(缓存)，int.MaxValue=无</summary>
        internal int SquadLeadSlotCache = int.MaxValue;

        //—— 击落粗筛帧缓存：owner 每帧单趟收集玩家周边敌对实体，探针只查短清单 ——
        /// <summary>粗筛缓存填充帧</summary>
        internal uint ThreatCacheFrame;
        internal readonly List<int> ThreatNpcs = new(8);
        internal readonly List<int> ThreatProjs = new(8);

        public override void ResetEffects() {
            MatrixActive = false;
            SourceItem = null;
        }

        /// <summary>
        /// 队内广播缓存兜底：本帧尚未有人填(非 owner 端)则单趟填充。
        /// 语义与逐探针自扫等价：队长=最小槽位，进度=全队 ai[2] 最大值
        /// </summary>
        internal void EnsureSquadCache() {
            if (SquadCacheFrame == Main.GameUpdateCount) {
                return;
            }
            SquadCacheFrame = Main.GameUpdateCount;
            int droneType = ModContent.ProjectileType<ProbeDroneProj>();
            float prog = 0f;
            int lead = int.MaxValue;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != droneType || proj.owner != Player.whoAmI) {
                    continue;
                }
                int slot = (int)proj.ai[0];
                if (slot < lead) {
                    lead = slot;
                }
                if (proj.ai[2] > prog) {
                    prog = proj.ai[2];
                }
            }
            SquadProgressCache = prog;
            SquadLeadSlotCache = lead;
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
            retargetGate = 0;
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

            ValidateDesignation();
            ScanNpcTable();
            UpdateDesignationDecay();
            UpdateSquad();
            TryCallStrike();
        }

        #region 编队维护

        /// <summary>
        /// 编队维护(owner 每帧唯一一趟弹幕全表)：认领在场槽位、收集探针引用，
        /// 顺手收集敌对弹幕威胁清单；ai 写入走收集到的 ≤5 个引用，不再二次全表
        /// </summary>
        private void UpdateSquad() {
            Span<bool> aliveNow = stackalloc bool[ProbeMatrixCore.ProbeCount];
            Span<int> slotProj = stackalloc int[ProbeMatrixCore.ProbeCount];
            slotProj.Fill(-1);
            int droneType = ModContent.ProjectileType<ProbeDroneProj>();
            int leadSlot = -1;
            float progress = MathHelper.Clamp(DesignationStacks / (float)ProbeMatrixCore.DesignationNeed, 0f, 1f);
            float quantized = (float)Math.Round(progress * 8f) / 8f;

            ThreatCacheFrame = Main.GameUpdateCount;
            ThreatProjs.Clear();
            Rectangle vicinity = Utils.CenteredRectangle(Player.Center, new Vector2(ThreatSpan * 2f));

            foreach (Projectile proj in Main.ActiveProjectiles) {
                //击落粗筛：敌对弹幕短清单(盒判包容大判定箱)
                if (proj.hostile && proj.damage > 0 && proj.Hitbox.Intersects(vicinity)) {
                    ThreatProjs.Add(proj.whoAmI);
                }
                if (proj.type != droneType || proj.owner != Player.whoAmI) {
                    continue;
                }
                int slot = (int)proj.ai[0];
                if (slot < 0 || slot >= aliveNow.Length) {
                    continue;
                }
                aliveNow[slot] = true;
                slotProj[slot] = proj.whoAmI;
                if (leadSlot < 0 || slot < leadSlot) {
                    leadSlot = slot;
                }
            }

            //目标与量化进度写进探针 ai，变更才发包(同步语义不变：队长 ai[2] 仍是跨端进度真相)
            for (int slot = 0; slot < slotProj.Length; slot++) {
                if (slotProj[slot] < 0) {
                    continue;
                }
                Projectile proj = Main.projectile[slotProj[slot]];
                if ((int)proj.ai[1] != DesignatedTarget) {
                    proj.ai[1] = DesignatedTarget;
                    proj.netUpdate = true;
                }
                float want = slot == leadSlot ? quantized : 0f;
                if (proj.ai[2] != want) {
                    proj.ai[2] = want;
                    proj.netUpdate = true;
                }
            }

            //队内广播缓存顺手填好，owner 端探针本帧直读免自扫
            SquadCacheFrame = Main.GameUpdateCount;
            SquadLeadSlotCache = leadSlot >= 0 ? leadSlot : int.MaxValue;
            SquadProgressCache = leadSlot >= 0 ? quantized : 0f;

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

        /// <summary>既有目标 O(1) 校验：死亡/换型/超脱离半径都作废，作废帧放行一次立即重索</summary>
        private void ValidateDesignation() {
            if (DesignatedTarget < 0) {
                return;
            }
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
            retargetGate = 0;
        }

        /// <summary>
        /// owner 每帧唯一一趟 NPC 全表：击落粗筛威胁清单每帧收集；
        /// 重索敌(Boss优先)只在节流拍顺手做，空场不再逐帧全扫
        /// </summary>
        private void ScanNpcTable() {
            ThreatNpcs.Clear();
            Rectangle vicinity = Utils.CenteredRectangle(Player.Center, new Vector2(ThreatSpan * 2f));

            bool retarget = DesignatedTarget < 0;
            if (retarget && --retargetGate > 0) {
                retarget = false;
            }
            int best = -1;
            float bestScore = float.MaxValue;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.friendly && npc.damage > 0 && npc.Hitbox.Intersects(vicinity)) {
                    ThreatNpcs.Add(npc.whoAmI);
                }
                if (!retarget || !npc.CanBeChasedBy()) {
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

            if (!retarget) {
                return;
            }
            retargetGate = RetargetPeriod;
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
