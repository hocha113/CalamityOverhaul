using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
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
    /// 械奴——强度由沉入物 DamageCurve 承载不吃锚点，但单发按"等级表值×8"钳顶，
    /// 开局沉超进度武器不再一发秒杀当期 Boss
    /// <para/>
    /// 墨印（<see cref="KikasaInkTag"/>）的结算也在这里：原版对等口径，
    /// 役从源或原版 minion 旗标族的命中对带印目标追加随等级表成长的平伤
    /// </summary>
    internal class KikasaServantBalanceGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>械奴单发钳顶系数：允许的单发上限 = 等级表原始值 × 此系数</summary>
        private const int ArmsHitCapPerLevel = 8;

        //鬼奴本体或它派生的子弹幕
        private bool servantSourced;
        //械奴族（沉入武器复制体）：区分成长口径
        private bool armsSourced;

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
            }
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
                //械奴钳顶：DamageCurve 只看沉入物 DPS 不看进度，开局沉高 DPS 武器会拉满倍率；
                //按伞成长给单发上限，超出部分折算回去（读 projectile.damage 近似最终值的基数）
                int cap = KikasaOverride.GetRawLevelDamage(owner) * ArmsHitCapPerLevel;
                if (projectile.damage > cap && projectile.damage > 0) {
                    modifiers.FinalDamage *= cap / (float)projectile.damage;
                }
                return;
            }
            //鬼奴锚点：基伤常量 ×（当前等级表值 / 三机械标定档 92）。
            //用等级表原始值而非面板伤——面板含召唤加成，出口的 ApplyTo 已乘过一遍
            modifiers.FinalDamage *= KikasaOverride.GetRawLevelDamage(owner)
                / KikasaOverride.ServantTuneAnchor;
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
            if (!target.HasBuff(ModContent.BuffType<KikasaInkTag>())) {
                return;
            }
            //随命中方玩家的鬼伞等级表成长：L0≈2 / L11≈9 / L18≈25 / L24=160
            float flat = MathF.Max(2f, KikasaOverride.GetRawLevelDamage(owner) * 0.10f)
                * ProjectileID.Sets.SummonTagDamageMultiplier[projectile.type];
            modifiers.FinalDamage.Flat += flat;
        }
    }
}
