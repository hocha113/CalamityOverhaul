using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Plantera
{
    /// <summary>
    /// 绽放新星球茎：世纪之花残酷遗物。
    /// 受击自动甩出荆棘藤网反缠攻击者(束缚+撕裂+反伤)；
    /// 生命低于阈值自动引爆绽放新星(花瓣冲击波+大额回血+再生强化+孢子雾遮蔽)；
    /// 战斗时长积累生长值，反缠藤更多更痛
    /// </summary>
    internal class BloomNovaBulb : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            Item.value = Item.buyPrice(1, 0, 0, 0);//同期Boss掉落物约4倍
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            BloomNovaBulbPlayer mp = player.GetModPlayer<BloomNovaBulbPlayer>();
            mp.Equipped = true;
            mp.HideVisual = hideVisual;
            mp.SourceItem = Item;
            player.statLifeMax2 += 30;
            player.lifeRegen += 4;
        }
    }

    /// <summary>
    /// 球茎状态机。状态全在实例字段：生长值/冷却只在 owner 端消费(生成参数与判定)，
    /// 远端实例只按同步血量喂待机视觉；装备态经原版装备同步各端一致
    /// </summary>
    internal class BloomNovaBulbPlayer : ModPlayer
    {
        #region 数值面板
        /// <summary>新星触发生命比例阈值</summary>
        public const float NovaLifeRatio = 0.35f;
        /// <summary>新星冷却(帧)</summary>
        public const int NovaCooldownTime = 3600;
        /// <summary>新星瞬间回血比例(生命上限)</summary>
        public const float NovaHealRatio = 0.40f;
        /// <summary>再生强化时长(帧)</summary>
        public const int RegenBoostTime = 360;
        /// <summary>孢子雾遮蔽时长(帧)，覆盖鼓胀+爆发+雾滞留全程</summary>
        public const int FogTime = BloomNovaBurst.SwellTime + BloomNovaBurst.BurstTime + BloomNovaBurst.FogHoldTime;
        /// <summary>雾中敌人攻击落空概率</summary>
        public const float FogMissChance = 0.25f;
        /// <summary>反缠出手冷却(帧)</summary>
        public const int LashCooldownTime = 45;
        /// <summary>藤网撞击基础伤害</summary>
        public const int LashBaseDamage = 950;
        /// <summary>生长值积满所需战斗时长(帧)</summary>
        public const int GrowthFullTime = 5400;
        /// <summary>脱战后生长值排空时长(帧)</summary>
        private const int GrowthDrainTime = 2700;
        /// <summary>攻击或受击后视为战斗中的窗口(帧)</summary>
        private const int CombatWindow = 300;
        /// <summary>荆棘反伤：受伤倍率部分</summary>
        private const float ThornReflectMult = 3f;
        /// <summary>荆棘反伤：固定部分(随生长值放大)</summary>
        private const int ThornReflectFlat = 600;
        /// <summary>同时在场反缠藤上限</summary>
        private const int MaxActiveVines = 10;
        #endregion

        /// <summary>本帧是否装备生效，物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>可见性开关(功能不受影响，只关待机演出)</summary>
        public bool HideVisual;
        /// <summary>本帧生效的物品实例，仅作生成源</summary>
        public Item SourceItem;
        /// <summary>生长值0~1，战斗时长奖励</summary>
        public float Growth;
        /// <summary>反缠冷却剩余</summary>
        public int LashCooldown;
        /// <summary>新星冷却剩余</summary>
        public int NovaCooldown;
        /// <summary>孢子雾遮蔽剩余(owner端闪避判定)</summary>
        public int FogTimer;
        /// <summary>再生强化剩余</summary>
        public int RegenTimer;
        private int combatTimer;

        public override void ResetEffects() {
            Equipped = false;
            HideVisual = false;
            SourceItem = null;
        }

        public override void PreUpdateMovement() {
            TickTimers();

            if (!Equipped) {
                //未装备快速排空生长值
                Growth = Math.Max(0f, Growth - 4f / GrowthFullTime);
                return;
            }

            //战斗中积累(90秒满)，脱战缓慢流失
            Growth = combatTimer > 0
                ? Math.Min(1f, Growth + 1f / GrowthFullTime)
                : Math.Max(0f, Growth - 1f / GrowthDrainTime);

            TryTriggerNova();
            UpdateStandbyVisual();
        }

        private void TickTimers() {
            if (LashCooldown > 0) {
                LashCooldown--;
            }
            if (NovaCooldown > 0) {
                NovaCooldown--;
            }
            if (FogTimer > 0) {
                FogTimer--;
            }
            if (RegenTimer > 0) {
                RegenTimer--;
            }
            if (combatTimer > 0) {
                combatTimer--;
            }
        }

        //死亡期间 PreUpdateMovement 不跑：冷却照常流逝，救急态清空
        public override void UpdateDead() {
            if (NovaCooldown > 0) {
                NovaCooldown--;
            }
            FogTimer = 0;
            RegenTimer = 0;
            combatTimer = 0;
            Growth = 0f;
        }

        public override void UpdateLifeRegen() {
            if (RegenTimer > 0) {
                //绽放余韵：20点/秒的再生强化
                Player.lifeRegen += 40;
            }
        }

        #region 战斗事件
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            combatTimer = CombatWindow;
        }

        public override void OnHurt(Player.HurtInfo info) {
            combatTimer = CombatWindow;
            //受击反制全在受害端(=命中判定端)结算
            if (!Equipped || Player.whoAmI != Main.myPlayer || Player.dead) {
                return;
            }

            NPC attackerNpc = null;
            Projectile sourceProj = null;
            if (info.DamageSource.TryGetCausingEntity(out Entity entity)) {
                if (entity is NPC npc) {
                    attackerNpc = npc;
                }
                else if (entity is Projectile proj) {
                    sourceProj = proj;
                }
            }

            //荆棘反伤：被缠住的敌人打你要付出代价
            if (attackerNpc != null && attackerNpc.active
                && attackerNpc.HasBuff(ModContent.BuffType<BloomSnaredDebuff>())) {
                int reflect = (int)(info.Damage * ThornReflectMult) + (int)(ThornReflectFlat * (1f + Growth));
                int dir = attackerNpc.Center.X >= Player.Center.X ? 1 : -1;
                Player.ApplyDamageToNPC(attackerNpc, reflect, 2f, dir, false);
            }

            TryLash(attackerNpc, sourceProj, info);
            //这一击可能直接砸穿阈值，当帧检查救急
            TryTriggerNova();
        }

        /// <summary>雾中遮蔽：孢子雾期间敌人的攻击有概率落空。仅在 owner 端掷骰(命中在受害端结算)</summary>
        public override bool FreeDodge(Player.HurtInfo info) {
            if (!Equipped || FogTimer <= 0 || Player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (Main.rand.NextFloat() >= FogMissChance) {
                return false;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.45f, Volume = 0.7f }, Player.Center);
            PlanteraRenderHelper.SpawnPetalBurst(Player.Center, 8, 4.5f, false);
            PlanteraRenderHelper.SpawnSporePuff(Player.Center, 1f);
            return true;
        }
        #endregion

        #region 藤网反缠
        /// <summary>受击甩藤：近战源缠攻击者本体，弹幕源朝来向锁附近敌人，兜底朝击退反方向甩空网</summary>
        private void TryLash(NPC attacker, Projectile sourceProj, Player.HurtInfo info) {
            if (LashCooldown > 0 || SourceItem == null || Player.dead) {
                return;
            }
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<BloomVineSnare>()] >= MaxActiveVines) {
                return;
            }
            LashCooldown = LashCooldownTime;

            //伤害来向
            Vector2 fallback = new Vector2(-info.HitDirection, -0.25f).SafeNormalize(Vector2.UnitX);
            Vector2 threatDir = fallback;
            if (attacker != null) {
                threatDir = Player.Center.To(attacker.Center).SafeNormalize(fallback);
            }
            else if (sourceProj != null) {
                threatDir = Player.Center.To(sourceProj.Center).SafeNormalize(fallback);
            }

            //目标集合：攻击者优先，其余按距离补位
            int vineCount = 1 + (int)(Growth * 3f);
            Span<int> targets = stackalloc int[4];
            int found = 0;
            if (attacker != null && attacker.CanBeChasedBy()) {
                targets[found++] = attacker.whoAmI;
            }
            for (int pass = found; pass < vineCount; pass++) {
                int best = -1;
                float bestDist = 1000f;
                foreach (var npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy()) {
                        continue;
                    }
                    bool taken = false;
                    for (int j = 0; j < found; j++) {
                        if (targets[j] == npc.whoAmI) {
                            taken = true;
                            break;
                        }
                    }
                    if (taken) {
                        continue;
                    }
                    float dist = Player.Distance(npc.Center);
                    if (dist < bestDist) {
                        bestDist = dist;
                        best = npc.whoAmI;
                    }
                }
                if (best < 0) {
                    break;
                }
                targets[found++] = best;
            }

            int damage = (int)(LashBaseDamage * (1f + 2f * Growth));
            IEntitySource source = Player.GetSource_Accessory(SourceItem);
            for (int i = 0; i < vineCount; i++) {
                Vector2 vel;
                float targetAi = 0f;
                if (i < found) {
                    NPC npc = Main.npc[targets[i]];
                    vel = Player.Center.To(npc.Center).SafeNormalize(threatDir) * 30f;
                    targetAi = targets[i] + 1;
                }
                else {
                    //无目标空网：沿来向扇形铺开，仍可缠住撞上来的敌人
                    vel = threatDir.RotatedBy((i - (vineCount - 1) * 0.5f) * 0.34f) * 30f;
                }
                Projectile.NewProjectile(source, Player.Center, vel,
                    ModContent.ProjectileType<BloomVineSnare>(), damage, 7f, Player.whoAmI, targetAi, Growth);
            }

            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = 0.15f, Volume = 0.85f, MaxInstances = 3 }, Player.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.3f, Volume = 0.9f, MaxInstances = 3 }, Player.Center);
        }
        #endregion

        #region 绽放新星
        private void TryTriggerNova() {
            if (Player.whoAmI != Main.myPlayer || !Equipped || Player.dead || Player.statLife <= 0) {
                return;
            }
            if (NovaCooldown > 0 || Player.statLife > Player.statLifeMax2 * NovaLifeRatio) {
                return;
            }
            TriggerNova();
        }

        /// <summary>低血救急：owner端结算。回血写自身生命(非SSC下服务器不可代写客户端血量)</summary>
        private void TriggerNova() {
            NovaCooldown = NovaCooldownTime;
            FogTimer = FogTime;
            RegenTimer = BloomNovaBurst.SwellTime + RegenBoostTime;

            int heal = (int)(Player.statLifeMax2 * NovaHealRatio);
            Player.statLife = Math.Min(Player.statLife + heal, Player.statLifeMax2);
            Player.HealEffect(heal, true);
            //可见的再生指示走原版快速治愈图标，实际数值在 UpdateLifeRegen
            Player.AddBuff(BuffID.RapidHealing, BloomNovaBurst.SwellTime + RegenBoostTime);

            IEntitySource source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_FromThis();
            Projectile.NewProjectile(source, Player.Center, Vector2.Zero,
                ModContent.ProjectileType<BloomNovaBurst>(), BloomNovaBurst.WaveDamage, 11f, Player.whoAmI, Growth);
        }
        #endregion

        #region 待机视觉
        /// <summary>低血待机强度0~1：血越低越亮。远端不知道冷却，只按同步血量亮；owner冷却中压暗</summary>
        public float StandbyIntensity {
            get {
                if (!Equipped || HideVisual || Player.dead) {
                    return 0f;
                }
                float lifeFrac = Player.statLife / (float)Math.Max(1, Player.statLifeMax2);
                if (lifeFrac >= 0.5f) {
                    return 0f;
                }
                float t = 1f - lifeFrac * 2f;
                if (Player.whoAmI == Main.myPlayer && NovaCooldown > 0) {
                    t *= 0.35f;
                }
                return t;
            }
        }

        private void UpdateStandbyVisual() {
            if (VaultUtils.isServer) {
                return;
            }
            float intensity = StandbyIntensity;
            if (intensity <= 0.05f) {
                return;
            }
            float breath = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f);
            Lighting.AddLight(Player.Bottom, PlanteraRenderHelper.GlowGreen.ToVector3() * (0.45f * intensity * breath));
            if (Main.rand.NextBool(16)) {
                PlanteraRenderHelper.SpawnAmbientMote(
                    Player.Bottom + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-4f, 2f)), false);
            }
        }
        #endregion
    }
}
