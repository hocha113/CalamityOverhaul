using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaThrows;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼奴多驻同场的出力平衡：驻影席数越多单只越省力（合计仍是净增）。
    /// 在命中端统一乘 <see cref="KikasaEffigyBoard.ServantDamageScale"/>，
    /// 覆盖鬼奴本体接触伤害与它们派生的一切子弹幕
    /// 逐个改 18 条鬼奴实现的伤害公式既碎又漏，标记随生成源传染一次即可
    /// <para/>
    /// 同一处还收口两条成长口径（共性根因四，反馈二·#2/#19/#52/#120）：<br/>
    /// 鬼奴——基伤表按三机械档标定，命中端乘"当前等级表值/92"，肉前跟成长缩、后期随成长涨；<br/>
    /// 械奴——强度由沉入武器自身档位承载（KikasaArmsProfiler.TierMul，稀有度→等级表），不读伞等级；
    /// 命中端只做两件事：编队摊薄 <see cref="KikasaEffigyBoard.PackDamageScale"/>（沉 5 把 ≠ 5 倍），
    /// 以及单发按"等级表值×8"钳顶（开局沉超进度武器不再一发秒杀当期 Boss）
    /// <para/>
    /// 掷奴另有一本单掷账（<see cref="ThrowLedger"/>）：它是唯一转发原武器弹幕的械奴，
    /// 一掷可能裂成一群子弹幕反复命中同一目标，无敌帧只能限每秒次数限不了每掷次数，
    /// 所以按目标累计期望伤害，超过 <see cref="KikasaThrowServant.ThrowHitBudget"/> 份就封顶
    /// <para/>
    /// 墨印（<see cref="KikasaInkTag"/>）的结算也在这里：原版对等口径，
    /// 役从源或原版 minion 旗标族的命中对带印目标追加随等级表成长的平伤
    /// </summary>
    internal class KikasaServantBalanceGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>械奴单发钳顶系数：允许的单发上限 = 等级表原始值 × 此系数</summary>
        private const int ArmsHitCapPerLevel = 8;

        /// <summary>
        /// 一次投掷的伤害账本：掷出物与它派生的全部子弹幕共用同一份引用，按目标累计已计入的期望伤害。
        /// 键是 NPC 槽位，账本存活期内（子弹幕寿命）槽位被复用时按类型变化重置；同类复用只会让新来者少吃，不会多吃
        /// </summary>
        private sealed class ThrowLedger
        {
            /// <summary>掷出物出手时的伤害基数（含召唤加成与散花折扣），预算以它为单位</summary>
            public readonly int RootDamage;

            /// <summary>每目标预算总额，首次命中时算一次后缓存（0 = 未算）；伞等级在一掷寿命内视作不变</summary>
            public float Budget;

            private readonly Dictionary<int, (int type, float spent)> entries = new();

            public ThrowLedger(int rootDamage) => RootDamage = rootDamage;

            public float Spent(NPC npc)
                => entries.TryGetValue(npc.whoAmI, out (int type, float spent) entry) && entry.type == npc.type
                    ? entry.spent : 0f;

            public void Add(NPC npc, float amount) => entries[npc.whoAmI] = (npc.type, Spent(npc) + amount);
        }

        //鬼奴本体或它派生的子弹幕
        private bool servantSourced;
        //械奴族（沉入武器复制体）：区分成长口径
        private bool armsSourced;
        //械奴编队复制体数，随标记一起沿父链传染；子弹幕出生时父编队已定编
        private int armsUnitCount = 1;
        //掷奴单掷账本：掷出物新开，子孙沿父链共用引用；非掷奴弹幕为 null
        private ThrowLedger throwLedger;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (projectile.ModProjectile is IKikasaServant) {
                servantSourced = true;
                armsSourced = projectile.ModProjectile is IKikasaArmsServant;
                return;
            }
            //子弹幕沿父链传染标记（GetSource_FromAI 归于 EntitySource_Parent 族）
            if (source is EntitySource_Parent parentSource
                && parentSource.Entity is Projectile parent
                && parent.TryGetGlobalProjectile(out KikasaServantBalanceGlobal parentGlobal)
                && parentGlobal.servantSourced) {
                servantSourced = true;
                armsSourced = parentGlobal.armsSourced;
                armsUnitCount = parent.ModProjectile is IKikasaArmsServant arms
                    ? arms.UnitCount : parentGlobal.armsUnitCount;
                //掷手掷出的那枚是账本的根（damage 在 OnSpawn 前已由 NewProjectile 赋好），
                //它裂出的蜂/火/碎片走 GetSource_FromThis 同属父链，直接共用这本账
                throwLedger = parent.ModProjectile is KikasaThrowServant
                    ? new ThrowLedger(projectile.damage)
                    : parentGlobal.throwLedger;
            }
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target) {
            //原版 Damage() 在碰撞检测之前就对每个活跃 NPC 问一遍这个钩子，蜂群规模下每帧上万次：
            //只查账不算账，没被本掷命中过的目标直接放行；记过账的目标其预算必已在命中端算好
            if (throwLedger == null) {
                return null;
            }
            float spent = throwLedger.Spent(target);
            if (spent <= 0f) {
                return null;
            }
            //预算见底的目标直接不命中：剩余蜂群继续飞但不再刷一串 1 点伤，也不白耗穿透数
            return throwLedger.Budget - spent >= 1f ? null : false;
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target,
            ref NPC.HitModifiers modifiers) {
            Player owner = Main.player[projectile.owner];
            if (owner?.active != true) {
                return;
            }
            ApplyInkTagBonus(projectile, target, owner, ref modifiers);
            if (!servantSourced) {
                return;
            }
            modifiers.FinalDamage *= KikasaEffigyBoard.ServantDamageScale(owner);

            if (armsSourced) {
                //编队摊薄：本体命中现读编制（Summon 在生成包之后才定编，出生时记的是满编），子弹幕用传染值
                int units = projectile.ModProjectile is IKikasaArmsServant arms ? arms.UnitCount : armsUnitCount;
                float scale = KikasaEffigyBoard.PackDamageScale(units);
                //钳顶：档位只看沉入武器不看进度，开局沉超进度武器会拉满倍率；按伞成长给单发上限，
                //比对的是摊薄后的期望单发（读 projectile.damage 近似最终值的基数），已在上限下的不再折
                float expected = projectile.damage * scale;
                int cap = ArmsHitCap(owner);
                if (expected > cap && expected > 0f) {
                    scale *= cap / expected;
                    expected = cap;
                }
                //单掷预算：本次只能吃到该目标剩余的份额，账上记同口径的期望值（不含席位乘区，两边一致即可）
                if (throwLedger != null && expected > 0f) {
                    if (throwLedger.Budget <= 0f) {
                        throwLedger.Budget = ThrowBudget(cap);
                    }
                    float allowed = Math.Clamp(throwLedger.Budget - throwLedger.Spent(target), 0f, expected);
                    throwLedger.Add(target, allowed);
                    scale *= allowed / expected;
                }
                modifiers.FinalDamage *= scale;
                return;
            }
            //鬼奴锚点：基伤常量 ×（当前等级表值 / 三机械标定档 92）。
            //用等级表原始值而非面板伤——面板含召唤加成，出口的 ApplyTo 已乘过一遍
            modifiers.FinalDamage *= KikasaOverride.GetRawLevelDamage(owner)
                / KikasaOverride.ServantTuneAnchor;
        }

        private static int ArmsHitCap(Player owner)
            => KikasaOverride.GetRawLevelDamage(owner) * ArmsHitCapPerLevel;

        /// <summary>
        /// 掷奴单掷对单个目标的预算总额：掷出物自己的期望单发（摊薄并钳顶后）× ThrowHitBudget。
        /// 手里剑一掷一中恰好用满一份；蜂群/火团/穿透再多也只能补到封顶
        /// </summary>
        private float ThrowBudget(int cap) {
            float rootExpected = Math.Min(
                throwLedger.RootDamage * KikasaEffigyBoard.PackDamageScale(armsUnitCount), cap);
            return rootExpected * KikasaThrowServant.ThrowHitBudget;
        }

        /// <summary>
        /// 墨印结算：役从源或原版 minion 旗标族（minion/sentry/MinionShot/SentryShot）
        /// 命中带印目标追加平伤，速攻召唤物吃原版 SummonTagDamageMultiplier 折减。
        /// 平伤挂 FinalDamage.Flat（管线终段，乘法之后）：役从锚点/让席乘区走 FinalDamage 乘法，
        /// 若走 FlatBonusDamage 会被同一乘区二次放大（L24 锚点 ≈17 倍），各消费者口径就不齐了
        /// </summary>
        private void ApplyInkTagBonus(Projectile projectile, NPC target, Player owner,
            ref NPC.HitModifiers modifiers) {
            if (!servantSourced && !projectile.minion && !projectile.sentry
                && !ProjectileID.Sets.MinionShot[projectile.type]
                && !ProjectileID.Sets.SentryShot[projectile.type]) {
                return;
            }
            //多段敌人的印记在本体上,打哪一节都吃这份加伤
            if (!KikasaInkTag.Marked(target)) {
                return;
            }
            //随命中方玩家的鬼伞等级表成长：L0≈2 / L11≈9 / L18≈25 / L24=160
            float flat = MathF.Max(2f, KikasaOverride.GetRawLevelDamage(owner) * 0.10f)
                * ProjectileID.Sets.SummonTagDamageMultiplier[projectile.type];
            modifiers.FinalDamage.Flat += flat;
        }
    }
}
