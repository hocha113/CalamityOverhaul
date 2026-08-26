using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 魔法·终局族共享方案：普攻命中积攒灾变计量，满层右键引爆一场三段式灾变
    /// （蓄势/爆发/余韵，由 director 弹幕全端承载演出与判定）。<br/>
    /// 计量与触发全部走攻击方本地路径；director 用 Misc 出生源生成，
    /// 演出弹幕不打标、不回充计量，防灾变自喂
    /// </summary>
    internal abstract class GsCataclysmScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "MagicCataclysm";

        //==================== 灾变参数面 ====================

        /// <summary>每次普攻命中积攒的计量</summary>
        public virtual int ChargePerHit => 3;

        /// <summary>计量上限，满后右键可触发</summary>
        public virtual int ChargeMax => 100;

        /// <summary>灾变冷却（全族共享一条冷却）</summary>
        public virtual int CooldownTicks => 1080;

        /// <summary>触发一次性蓝耗（经 CheckMana 结算，吃减耗饰品）</summary>
        public virtual int CataclysmManaCost => 50;

        /// <summary>数值行：普攻伤害加成</summary>
        protected abstract float PassiveDamageBonus { get; }

        /// <summary>灾变 director 弹幕类型</summary>
        protected abstract int DirectorType { get; }

        /// <summary>计量读数金辉主题色</summary>
        protected virtual Color AccentColor => new(232, 146, 38);

        /// <summary>锚点取光标（false 取自机）</summary>
        protected virtual bool AnchorAtCursor => true;

        /// <summary>触发音</summary>
        protected virtual SoundStyle TriggerSound => SoundID.Item29;

        //==================== 数值行 ====================

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1f + PassiveDamageBonus;

        //==================== 计量积攒（命中钩子只在攻击方端执行，计量即攻击方本地量） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router)
            => GainCharge(Main.player[proj.owner], target);

        public sealed override void GsOnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
            => GainCharge(player, target);

        private void GainCharge(Player player, NPC target) {
            //假人与友方不喂计量
            if (target.friendly || target.immortal || target.type == NPCID.TargetDummy) {
                return;
            }
            player.GetModPlayer<GsCataclysmPlayer>().AddCharge(ChargePerHit, ChargeMax, TargetItemID);
        }

        //==================== 右键触发 ====================

        public sealed override bool? GsAltFunctionUse(Item item, Player player) => true;

        public sealed override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse != 2) {
                //左键完全原版
                return null;
            }
            //右键是灾变指令，永不走原版使用链；生成判定只在本地玩家端，各端统一返回 false
            if (player.whoAmI == Main.myPlayer) {
                TryTrigger(item, player);
            }
            return false;
        }

        private void TryTrigger(Item item, Player player) {
            GsCataclysmPlayer state = player.GetModPlayer<GsCataclysmPlayer>();
            if (state.BoundItemType != TargetItemID || state.Charge < ChargeMax || state.OnCooldown) {
                Deny(state, player);
                return;
            }
            if (!player.CheckMana(item, CataclysmManaCost, true)) {
                Deny(state, player);
                return;
            }
            player.manaRegenDelay = Math.Max(player.manaRegenDelay, 40);

            Vector2 anchor = AnchorAtCursor ? Main.MouseWorld : player.Center;
            Vector2 off = anchor - player.Center;
            if (off.Length() > 1100f) {
                anchor = player.Center + off.SafeNormalize(Vector2.UnitX) * 1100f;
            }
            float ai1 = 0f, ai2 = 0f;
            ModifyTriggerParams(item, player, ref anchor, ref ai1, ref ai2);

            Projectile.NewProjectile(player.GetSource_Misc("CWRGsCataclysm"), anchor, Vector2.Zero,
                DirectorType, player.GetWeaponDamage(item), item.knockBack, player.whoAmI, 0f, ai1, ai2);
            state.ConsumeAndCooldown(CooldownTicks);
            SoundEngine.PlaySound(TriggerSound with { Volume = 0.85f }, anchor);
        }

        /// <summary>触发瞬间修正锚点与 director 的 ai1/ai2（选目标等，只在 owner 端调用）</summary>
        protected virtual void ModifyTriggerParams(Item item, Player player, ref Vector2 anchor, ref float ai1, ref float ai2) { }

        private static void Deny(GsCataclysmPlayer state, Player player) {
            if (Main.GameUpdateCount - state.LastDenyTick < 45) {
                return;
            }
            state.LastDenyTick = Main.GameUpdateCount;
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.4f }, player.Center);
        }

        //==================== 就绪读数（计量是攻击方本地量，个人读数合法） ====================

        public sealed override void GsHoldItem(Item item, Player player) {
            GsCataclysmHoldItem(item, player);
            if (player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            GsCataclysmPlayer state = player.GetModPlayer<GsCataclysmPlayer>();
            if (state.BoundItemType != TargetItemID || state.Charge < ChargeMax) {
                return;
            }
            if (!state.ReadyChimed) {
                state.ReadyChimed = true;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = 0.2f }, player.Center);
            }
            if (state.OnCooldown || Main.GameUpdateCount % 3 != 0) {
                return;
            }
            //满层且冷却就绪：杖尖金辉，1 粒/3 帧
            Vector2 pos = player.itemLocation + new Vector2(player.direction * 10f, -6f) + Main.rand.NextVector2Circular(7f, 7f);
            PRTLoader.NewParticle<PRT_Light>(pos, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                AccentColor, Main.rand.NextFloat(0.09f, 0.14f))?.Configure(12, 0.85f);
        }

        /// <summary>子类的手持每帧扩展位（基类已占用 GsHoldItem）</summary>
        protected virtual void GsCataclysmHoldItem(Item item, Player player) { }
    }
}
