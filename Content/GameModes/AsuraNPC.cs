using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 修罗模式：敌怪对同种伤害来源的自适应免疫
    /// 来源键 = 弹幕类型（正数）/物品类型（负数）。适应量就是减伤比例本身：
    /// 每次同源命中吞掉一份剩余脆弱度、渐近上限，脱手过宽限后线性回落。数值见 <see cref="GameModeTuning"/>
    /// 共享血池的部件（蠕虫体节、附着头等，realLife 指向池主）统一记到池主一本账上：
    /// 整条蠕虫是一个生物，对同一件武器只有一份记忆；同一发弹幕扫过多节、同一次挥击划过多节
    /// 只算一次接触（去重见 <see cref="AsuraProj"/> 与 <see cref="AccumulateSwing"/>），不因体节多而加速适应。
    /// 近战是适应的裂隙：刀刃本体只承受小部分适应减伤，近战弹幕次之；
    /// 近战命中还按出手距离获得贴身增幅，越近越痛。
    /// tML 的打击判定在攻击方本机进行（伤害随打击包下发，服务端不重算），
    /// 因此适应状态无需网络同步；联机下每个攻击者面对的是敌怪对"自己"的适应
    /// </summary>
    internal class AsuraNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private struct AdaptEntry
        {
            /// <summary>最近一次接触时刻的减伤比例（未计此后回落）</summary>
            public float Resist;
            public uint LastHitTick;
            /// <summary>物品键专用：最近一次上账的挥击起始帧，同一挥击内的多节命中只记一次</summary>
            public uint LastSwingStart;
        }

        /// <summary>来源键 → 适应条目；懒初始化，条目回落尽后懒清除</summary>
        private Dictionary<int, AdaptEntry> adapt;

        private static int ProjKey(int type) => type;
        private static int ItemKey(int type) => -type;

        private static bool Eligible(NPC npc) => GameModeSystem.AsuraActive && !npc.friendly;

        /// <summary>记账主体：共享血池的部件记到池主，独立个体（含池主自身）记自己</summary>
        private AsuraNPC Ledger(NPC npc) {
            int root = npc.realLife;
            if (root < 0 || root == npc.whoAmI || root >= Main.maxNPCs) {
                return this;
            }
            NPC rootNpc = Main.npc[root];
            if (!rootNpc.active || !rootNpc.TryGetGlobalNPC(out AsuraNPC ledger)) {
                return this;
            }
            return ledger;
        }

        /// <summary>按回落折算当前减伤比例（可为负，调用方自行钳零）</summary>
        private static float EffectiveResist(in AdaptEntry entry, uint now) {
            uint elapsed = now - entry.LastHitTick;
            uint grace = (uint)GameModeTuning.AsuraAdaptGraceTicks;
            if (elapsed <= grace) {
                return entry.Resist;
            }
            return entry.Resist - (elapsed - grace) * GameModeTuning.AsuraAdaptDecayPerTick;
        }

        /// <summary>该来源当前的伤害保留系数（1 = 无适应）；adaptTaken 为该攻击实际承受的适应减伤比例</summary>
        private float ResistFactor(int key, float adaptTaken) {
            if (adapt == null || !adapt.TryGetValue(key, out AdaptEntry entry)) {
                return 1f;
            }
            float resist = EffectiveResist(in entry, Main.GameUpdateCount);
            if (resist <= 0f) {
                adapt.Remove(key);
                return 1f;
            }
            return 1f - resist * adaptTaken;
        }

        /// <summary>贴身增幅倍率：按玩家中心到目标碰撞箱最近点的距离线性增伤，贴脸满额、出增幅圈归 1</summary>
        private static float CloseRangeMult(Player player, NPC npc) {
            Rectangle box = npc.Hitbox;
            Vector2 nearest = new(
                MathHelper.Clamp(player.Center.X, box.Left, box.Right),
                MathHelper.Clamp(player.Center.Y, box.Top, box.Bottom));
            float dist = player.Center.Distance(nearest);
            float t = MathHelper.Clamp(
                (GameModeTuning.AsuraCloseRangeZeroDist - dist)
                / (GameModeTuning.AsuraCloseRangeZeroDist - GameModeTuning.AsuraCloseRangeFullDist), 0f, 1f);
            return 1f + GameModeTuning.AsuraCloseRangeMaxBonus * t;
        }

        /// <summary>
        /// 记一次同源接触：先折算回落，再吞掉一份剩余脆弱度（毁灭下按多次命中计），刷新计时。
        /// R ← R + (Cap - R) × Bite，越接近上限每击涨得越少，天然渐近不撞顶
        /// </summary>
        private void Accumulate(int key, uint swingStart = 0) {
            adapt ??= [];
            uint now = Main.GameUpdateCount;
            float resist = 0f;
            if (adapt.TryGetValue(key, out AdaptEntry entry)) {
                resist = Math.Max(0f, EffectiveResist(in entry, now));
            }
            float hits = GameModeSystem.AnnihilationActive ? GameModeTuning.AnnihilationAdaptHitsPerHit : 1f;
            float bite = 1f - MathF.Pow(1f - GameModeTuning.AsuraAdaptBite, hits);
            adapt[key] = new AdaptEntry {
                Resist = resist + (GameModeTuning.AsuraResistCap - resist) * bite,
                LastHitTick = now,
                LastSwingStart = swingStart,
            };
        }

        /// <summary>
        /// 物品挥击上账：同一次挥击划过多节只算一次接触。
        /// 原版对同一 NPC 一次挥击只命中一次，但相邻体节可在同一挥击的不同帧各挨一刀（attackCD 间隔），
        /// 故以挥击起始帧为身份：当前帧倒推动画已走的帧数，同一挥击内不论哪一帧命中都得到同一个值
        /// </summary>
        private void AccumulateSwing(int key, Player player) {
            uint swingStart = Main.GameUpdateCount - (uint)Math.Max(0, player.itemAnimationMax - player.itemAnimation);
            if (adapt != null && adapt.TryGetValue(key, out AdaptEntry entry) && entry.LastSwingStart == swingStart) {
                return;
            }
            Accumulate(key, swingStart);
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (!Eligible(npc)) {
                return;
            }
            //物品挥击就是刀刃本体：适应减伤按真近战折扣，并吃贴身增幅
            bool melee = item.DamageType.CountsAsClass(DamageClass.Melee);
            float adaptTaken = melee ? GameModeTuning.AsuraTrueMeleeAdaptTaken : 1f;
            modifiers.FinalDamage *= Ledger(npc).ResistFactor(ItemKey(item.type), adaptTaken);
            if (melee) {
                modifiers.FinalDamage *= CloseRangeMult(player, npc);
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (!Eligible(npc)) {
                return;
            }
            //ownerHitCheck 是手持刀刃弹幕的通行标记，灾厄真近战伤害类是另一路信号
            bool melee = projectile.DamageType.CountsAsClass(DamageClass.Melee);
            bool blade = melee && (projectile.ownerHitCheck || CWRRef.IsTrueMeleeClass(projectile.DamageType));
            float adaptTaken = blade ? GameModeTuning.AsuraTrueMeleeAdaptTaken
                : melee ? GameModeTuning.AsuraMeleeProjAdaptTaken : 1f;
            modifiers.FinalDamage *= Ledger(npc).ResistFactor(ProjKey(projectile.type), adaptTaken);
            Player owner = Main.player[projectile.owner];
            if (melee && owner.active) {
                modifiers.FinalDamage *= CloseRangeMult(owner, npc);
            }
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (!Eligible(npc)) {
                return;
            }
            Ledger(npc).AccumulateSwing(ItemKey(item.type), player);
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (!Eligible(npc)) {
                return;
            }
            AsuraNPC ledger = Ledger(npc);
            //同一发弹幕在自己的再命中间隔内扫过同一本账的多节只算一次接触；
            //独立个体本就受自身免疫帧限制，这里的判定对它恒放行
            if (!projectile.GetGlobalProjectile<AsuraProj>().TryTeach(ledger, projectile)) {
                return;
            }
            ledger.Accumulate(ProjKey(projectile.type));
        }
    }

    /// <summary>
    /// 修罗适应的弹幕侧去重：记住这发弹幕最近给哪本账上过、何时上的。
    /// 蠕虫每节各有自己的免疫帧，一道激光可在同一窗口内接连命中二十节，
    /// 按弹幕实例限流后，池主每个再命中间隔只多一次接触，与打单体的速率一致
    /// </summary>
    internal class AsuraProj : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private AsuraNPC taughtLedger;
        private uint taughtTick;

        /// <summary>该弹幕对同一 NPC 的再命中间隔（帧），镜像原版 Projectile.Damage 的免疫写法</summary>
        private static uint RehitInterval(Projectile projectile) {
            if (projectile.usesLocalNPCImmunity) {
                //-1 = 每个目标只命中一次；-2 = 不走局部免疫，落回默认 10 帧
                return projectile.localNPCHitCooldown switch {
                    -1 => uint.MaxValue,
                    < 0 => 10u,
                    var cooldown => (uint)cooldown,
                };
            }
            if (projectile.usesIDStaticNPCImmunity) {
                return (uint)Math.Max(0, projectile.idStaticNPCHitCooldown);
            }
            //默认 immune[owner] = 10；非穿透弹幕一发只命中一个目标，窗口取值无关紧要
            return 10u;
        }

        /// <summary>尝试给这本账上一次接触：同一账本在再命中间隔内重复到达则拒绝</summary>
        internal bool TryTeach(AsuraNPC ledger, Projectile projectile) {
            uint now = Main.GameUpdateCount;
            uint interval = Math.Max(1u, RehitInterval(projectile));
            if (ReferenceEquals(taughtLedger, ledger) && now - taughtTick < interval) {
                return false;
            }
            taughtLedger = ledger;
            taughtTick = now;
            return true;
        }
    }
}
