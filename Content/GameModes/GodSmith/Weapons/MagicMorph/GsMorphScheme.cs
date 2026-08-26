using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 魔法·困难弹体族（MagicMorph）共享引擎：法术二形态。<br/>
    /// 交互契约：右键触发进入蓄力（<see cref="GsAltFunctionUse"/> 放行 +
    /// <see cref="GsCanUseItem"/> 捕获 altFunctionUse==2，闩锁防重触发），
    /// 蓄力推进在 <see cref="GsHoldItem"/> 的本地玩家路径逐帧读右键；
    /// 松手时蓄满则结算蓝耗（item.mana × <see cref="ChargeManaMult"/>）并调 <see cref="FireMorphB"/>。<br/>
    /// 蓄力状态挂 <see cref="GsMorphPlayer"/>（每玩家实例字段），换武器/关模式/死亡由其兜底清理；
    /// 蓄力读数（杖尖聚拢粒子/蓄满定音与轻屏震）是施法者本地反馈，
    /// 跨端可见的部分一律由释放出的真弹幕承载。<br/>
    /// B 形态生成走 <see cref="SpawnMorph"/>：pendingKind 在 NewProjectile 同步调用链内被
    /// <see cref="GsProjOnSpawnMarked"/> 消费写进 MarkData，先于生成包发出，各端一致
    /// </summary>
    internal abstract class GsMorphScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "MagicMorph";

        //==================== 二形态参数（子类按计划行覆写） ====================

        /// <summary>B 形态蓄力阈值（帧）</summary>
        protected virtual int ChargeTicksB => 45;

        /// <summary>B 形态蓝耗倍率（对 item.mana）</summary>
        protected virtual float ChargeManaMult => 1.8f;

        /// <summary>蓄力中移速乘区（重咏唱体感），由 GsMorphPlayer 消费</summary>
        internal virtual float ChargeSlowdown => 0.85f;

        /// <summary>蓄力读数粒子色</summary>
        protected virtual Color ChargeColor => new(196, 142, 255);

        /// <summary>基础伤害乘区（数值行，残酷口径下的账面加成）</summary>
        protected virtual float BaseDamageMult => 1.08f;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= BaseDamageMult;

        //==================== MarkData 形态约定 ====================

        /// <summary>MarkData 形态值：0=A 常规；1=B 主体；≥10 供子类自定义（碎屑/子产物等）</summary>
        protected const int KindA = 0;
        /// <summary>B 形态主体</summary>
        protected const int KindB = 1;

        //==================== 右键蓄力流 ====================

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public sealed override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                //右键从不走原版使用链；触发信号只在本地玩家端消费，闩锁保证一次按住只触发一次
                if (player.whoAmI == Main.myPlayer) {
                    GsMorphPlayer morph = player.GetModPlayer<GsMorphPlayer>();
                    if (!morph.AltLatch) {
                        morph.AltLatch = true;
                        OnAltTrigger(item, player);
                    }
                }
                return false;
            }
            //本地玩家蓄力期间锁左键，防止边蓄边施
            if (player.whoAmI == Main.myPlayer
                && player.GetModPlayer<GsMorphPlayer>().ChargingItem == item.type) {
                return false;
            }
            return GsMorphCanUseItem(item, player);
        }

        /// <summary>子类的 CanUseItem 扩展点（左键路径）</summary>
        protected virtual bool? GsMorphCanUseItem(Item item, Player player) => null;

        /// <summary>
        /// 右键按下瞬间（仅本地玩家，单次按住只回调一次）。
        /// 默认开始蓄力；瞬发型武器（领域迁移/模式切换/收回）覆写本方法
        /// </summary>
        protected virtual void OnAltTrigger(Item item, Player player)
            => player.GetModPlayer<GsMorphPlayer>().BeginCharge(item.type);

        public sealed override void GsHoldItem(Item item, Player player) {
            GsMorphHoldItem(item, player);
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsMorphPlayer morph = player.GetModPlayer<GsMorphPlayer>();
            if (morph.ChargingItem != item.type) {
                return;
            }
            if (PlayerInput.Triggers.Current.MouseRight) {
                if (morph.ChargeTicks < ChargeTicksB * 3) {
                    morph.ChargeTicks++;
                }
                EmitChargeReadout(item, player, morph.ChargeTicks);
                if (morph.ChargeTicks == ChargeTicksB) {
                    //蓄满一次性提示：定音 + 轻屏震（施法者本地）
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.4f }, player.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        player.Center, Main.rand.NextVector2Unit(), 0.5f, 8f, 10));
                }
                return;
            }
            //松手：蓄满则释放，未满则静默取消（蓄力全程未预扣蓝，无需返还）
            int ticks = morph.ChargeTicks;
            morph.EndCharge();
            if (ticks >= ChargeTicksB) {
                lastReleaseTicks = ticks;
                TryReleaseB(item, player);
            }
        }

        /// <summary>子类的 HoldItem 扩展点（所有端都会进入；写玩家态先守 myPlayer）</summary>
        protected virtual void GsMorphHoldItem(Item item, Player player) { }

        /// <summary>本次释放时的实际蓄力帧数（仅 FireMorphB 调用栈内有效，myPlayer 消费）</summary>
        protected int lastReleaseTicks;

        /// <summary>蓄力读数：杖尖聚拢粒子，30%/70%/满三段加密（施法者本地反馈）</summary>
        protected virtual void EmitChargeReadout(Item item, Player player, int ticks) {
            float pct = MathHelper.Clamp(ticks / (float)ChargeTicksB, 0f, 1f);
            Vector2 tip = player.Center + GsAimUnit(player) * 30f;
            int interval = pct >= 1f ? 2 : pct >= 0.7f ? 3 : pct >= 0.3f ? 4 : 6;
            if (ticks % interval == 0) {
                Vector2 off = Main.rand.NextVector2CircularEdge(22f, 22f);
                PRTLoader.NewParticle<PRT_Light>(tip + off, -off * 0.07f, ChargeColor,
                    0.12f + 0.1f * pct)?.Configure(12, 0.8f);
            }
            if (pct >= 1f && ticks % 6 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(tip, Vector2.Zero, ChargeColor, 0.28f)
                    ?.Configure(ChargeColor, 10, 0.15f, 1.1f);
            }
            Lighting.AddLight(tip, ChargeColor.ToVector3() * (0.2f + 0.3f * pct));
        }

        /// <summary>结算 B 形态蓝耗并释放；蓝不足播失败音（仅本地玩家路径调用）</summary>
        protected void TryReleaseB(Item item, Player player) {
            int cost = (int)MathF.Ceiling(item.mana * ChargeManaMult * player.manaCost);
            if (cost > 0 && !player.CheckMana(item, cost, true, false)) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.7f, Volume = 0.6f }, player.Center);
                return;
            }
            player.manaRegenDelay = Math.Max(player.manaRegenDelay, 60);
            FireMorphB(item, player);
            //释放硬直兼一次举杖动作，防止松手瞬间无缝接左键
            player.itemAnimation = player.itemAnimationMax = item.useAnimation;
            player.itemTime = player.itemTimeMax = item.useTime;
        }

        /// <summary>
        /// 释放 B 形态（仅本地玩家路径；蓝耗已结算）。
        /// 生成弹幕用 <see cref="SpawnMorph"/> 走打标通道；模式切换型武器在此开模式窗
        /// </summary>
        protected abstract void FireMorphB(Item item, Player player);

        //==================== 打标生成管线 ====================

        private int pendingKind;
        private float pendingData2;

        /// <summary>
        /// 以指定形态生成弹幕（仅本地玩家路径）。出生源用 GetSource_ItemUse 走路由打标通道，
        /// kind/data2 经 OnSpawnMarked 写进 MarkData/MarkData2，与 ai0/ai1 一起先于生成包定型
        /// </summary>
        protected Projectile SpawnMorph(Player player, Item item, Vector2 pos, Vector2 vel,
            int type, int damage, float knockback, int kind, float data2 = 0f,
            float ai0 = 0f, float ai1 = 0f) {
            pendingKind = kind;
            pendingData2 = data2;
            int idx = Projectile.NewProjectile(player.GetSource_ItemUse(item), pos, vel,
                type, damage, knockback, player.whoAmI, ai0, ai1);
            pendingKind = 0;
            pendingData2 = 0f;
            return idx >= 0 && idx < Main.maxProjectiles ? Main.projectile[idx] : null;
        }

        public sealed override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (pendingKind != 0) {
                router.MarkData = pendingKind;
                router.MarkData2 = pendingData2;
            }
            GsMorphOnSpawnMarked(proj, router);
        }

        /// <summary>子类的打标出生扩展点（owner 端，先于生成包发出；A 形态增强放这）</summary>
        protected virtual void GsMorphOnSpawnMarked(Projectile proj, GodSmithProjRouter router) { }

        //==================== 通用小工具 ====================

        /// <summary>取该弹幕的形态值（MarkData 取整）</summary>
        protected static int KindOf(GodSmithProjRouter router) => (int)router.MarkData;

        /// <summary>光标附近的可追击目标；无则返回 null（仅本地玩家路径调用）</summary>
        protected static NPC FindCursorTarget(float range = 600f) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.npc) {
                if (!npc.active || !npc.CanBeChasedBy()) {
                    continue;
                }
                float d = npc.Distance(Main.MouseWorld);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>某点附近最近的可追击目标</summary>
        protected static NPC FindNearestTarget(Vector2 pos, float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.npc) {
                if (!npc.active || !npc.CanBeChasedBy()) {
                    continue;
                }
                float d = npc.Distance(pos);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }
    }
}
