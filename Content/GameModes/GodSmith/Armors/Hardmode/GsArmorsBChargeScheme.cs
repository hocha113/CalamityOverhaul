using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// P10b 族公共骨架（族内基类，不属框架）：「积攒→满层→释放」公共管线，
    /// 积攒来源（命中/受击）与崩层规则参数化，就绪光环与反馈粒子由主题色板驱动。
    /// 硬模式矿套六件与多数进度套共用此骨架，但释放机制各自实现、主题互不相同。<br/>
    /// 层数只存在于攻击方端（GodSmithArmorPlayer 暂存寄存器），跨端可见的是释放出的弹幕实体
    /// </summary>
    internal abstract class GsArmorsBChargeScheme : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsB";

        /// <summary>满层所需层数</summary>
        protected abstract int FullCharge { get; }

        /// <summary>主题主色（就绪光环/反馈）</summary>
        protected abstract Color ThemeMain { get; }

        /// <summary>主题亮色</summary>
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

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            ReadyAura(player);
        }

        /// <summary>就绪光环（个人读数：层数只在攻击方端存在，远端不可见）</summary>
        protected virtual void ReadyAura(Player player) {
            Lighting.AddLight(player.Center, ThemeMain.ToVector3() * 0.22f);
            if (Main.rand.NextBool(9)) {
                Vector2 at = player.Center + Main.rand.NextVector2CircularEdge(20f, 26f);
                PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    ThemeBright, Main.rand.NextFloat(0.08f, 0.14f))?.Configure(14, 0.7f);
            }
        }

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
                OnChargeGained(player, state, target);
                return;
            }
            if (state.EndowCharge >= FullCharge) {
                state.EndowCharge = 0;
                ReleaseEndow(player, state, target, hit, damageDone);
            }
        }

        /// <summary>积攒反馈（攻击方端本地）</summary>
        protected virtual void OnChargeGained(Player player, GodSmithArmorPlayer state, NPC target) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f),
                ThemeMain, 0.3f)?.Configure(false, 12);
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (ChargeOnHurt) {
                if (state.EndowCharge >= FullCharge) {
                    return;
                }
                state.EndowCharge = Math.Min(FullCharge, state.EndowCharge + ChargePerHurt);
                if (!VaultUtils.isServer) {
                    //吃击成层：主题色火花自伤处收拢上身
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(14f, 20f),
                            -Vector2.UnitY * Main.rand.NextFloat(1f, 2f),
                            ThemeBright, 0.35f)?.Configure(false, 14);
                    }
                }
                return;
            }
            if (HurtLoss <= 0 || state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - HurtLoss);
            if (!VaultUtils.isServer) {
                //崩层：主题色碎屑洒落
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Smoke, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)),
                        120, ThemeMain, 1.1f);
                    d.noGravity = false;
                }
            }
        }
    }
}
