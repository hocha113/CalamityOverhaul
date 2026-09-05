using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// P10b 族公共骨架（族内基类，不属框架）：「积攒→满层→释放」公共管线，
    /// 积攒来源（命中/受击）与崩层规则参数化。
    /// 硬模式矿套六件与多数进度套共用此骨架，但释放机制各自实现、主题互不相同。<br/>
    /// 层数只存在于攻击方端（GodSmithArmorPlayer 暂存寄存器），跨端可见的是释放出的弹幕实体
    /// </summary>
    internal abstract class GsArmorsBChargeScheme : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsB";

        /// <summary>满层所需层数</summary>
        protected abstract int FullCharge { get; }

        /// <summary>主题主色（色板签名，子类仍在覆写，基类已不消费）</summary>
        protected abstract Color ThemeMain { get; }

        /// <summary>主题亮色（色板签名，子类仍在覆写，基类已不消费）</summary>
        protected abstract Color ThemeBright { get; }

        /// <summary>命中是否积攒</summary>
        protected virtual bool ChargeOnHit => true;

        /// <summary>受击是否积攒（钛金式反向）；true 时受击不再崩层</summary>
        protected virtual bool ChargeOnHurt => false;

        /// <summary>每次受击积攒的层数</summary>
        protected virtual int ChargePerHurt => 1;

        /// <summary>受击崩落层数（仅 ChargeOnHurt 为 false 时生效）</summary>
        protected virtual int HurtLoss => 2;

        /// <summary>自家 proc 弹幕过滤，防自喂循环</summary>
        protected virtual bool IsOwnProc(Projectile proj) => false;

        /// <summary>满层释放；target 为触发命中的目标（受击积攒型也走命中释放）</summary>
        protected abstract void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target, in NPC.HitInfo hit, int damageDone);

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj != null && IsOwnProc(sourceProj)) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }
            if (ChargeOnHit && state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }
            if (state.EndowCharge >= FullCharge) {
                state.EndowCharge = 0;
                ReleaseEndow(player, state, target, hit, damageDone);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (ChargeOnHurt) {
                if (state.EndowCharge >= FullCharge) {
                    return;
                }
                state.EndowCharge = Math.Min(FullCharge, state.EndowCharge + ChargePerHurt);
                return;
            }
            if (HurtLoss <= 0 || state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - HurtLoss);
        }
    }
}
