using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 神匠中枢 ModPlayer：维护本帧生效的重铸饰品与神赋饰品清单，
    /// 把命中/受击/护死等事件 fan-out 给生效效果；并提供通用每玩家冷却表。<br/>
    /// 此类禁止内容 agent 增改字段：轻量状态用冷却表，
    /// 复杂状态在内容文件里自建私有 ModPlayer。<br/>
    /// 清单每帧由 ResetEffects 清空、装备结算期重新登记，模式关闭时天然为空
    /// </summary>
    internal class GodSmithPlayer : ModPlayer
    {
        //本帧生效的重铸饰品（GodSmithAccItem.UpdateAccessory 登记）
        private readonly List<(Item item, GodSmithAccEffect effect)> activeAccs = [];
        //本帧生效的神赋饰品（GodSmithItem.UpdateAccessory 登记）
        private readonly List<(Item item, GodSmithEndow endow)> activeEndowAccs = [];
        //冷却表：键按约定 = 物品 type，值 = 到期帧（对比 Main.GameUpdateCount）
        private readonly Dictionary<int, uint> cooldownExpiry = [];

        internal void RegisterActiveAcc(Item item, GodSmithAccEffect effect) => activeAccs.Add((item, effect));

        internal void RegisterActiveEndowAcc(Item item, GodSmithEndow endow) => activeEndowAccs.Add((item, endow));

        public override void ResetEffects() {
            activeAccs.Clear();
            activeEndowAccs.Clear();
        }

        //==================== 冷却 helper（每玩家本地，键约定 = 物品 type） ====================

        /// <summary>冷却就绪则立刻进入冷却并返回 true，否则返回 false（一步完成判定加占用）</summary>
        public bool TryUseCooldown(int key, int cooldownFrames) {
            if (IsOnCooldown(key)) {
                return false;
            }
            SetCooldown(key, cooldownFrames);
            return true;
        }

        /// <summary>直接写入冷却（护死类饰品触发后压长冷却用）</summary>
        public void SetCooldown(int key, int frames) => cooldownExpiry[key] = Main.GameUpdateCount + (uint)Math.Max(0, frames);

        /// <summary>是否仍在冷却</summary>
        public bool IsOnCooldown(int key) => cooldownExpiry.TryGetValue(key, out uint expiry) && Main.GameUpdateCount < expiry;

        /// <summary>冷却剩余帧数，就绪时为 0</summary>
        public int CooldownRemaining(int key)
            => cooldownExpiry.TryGetValue(key, out uint expiry) && Main.GameUpdateCount < expiry
                ? (int)(expiry - Main.GameUpdateCount) : 0;

        //==================== 事件 fan-out ====================

        public override void PostUpdateEquips() {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            foreach ((Item item, GodSmithAccEffect effect) in activeAccs) {
                effect.PostUpdateEquips(item, Player, this);
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //武器神赋：直击路径，武器就在手上
            if (item.TryGetGlobalItem(out GodSmithItem data) && data.Endow is GodSmithEndow endow) {
                endow.OnHitNPC(Player, item, null, target, hit, damageDone, endow.TierScaleFor(item.prefix));
            }
            DispatchWearerHit(target, hit, damageDone, fromProjectile: false);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //武器神赋：弹幕路径，出生源打标回溯（GodSmithEndowSource，owner 端本地）
            if (proj.TryGetGlobalProjectile(out GodSmithEndowSource source) && source.Endow is GodSmithEndow endow) {
                endow.OnHitNPC(Player, null, proj, target, hit, damageDone, source.TierScale);
            }
            DispatchWearerHit(target, hit, damageDone, fromProjectile: true);
        }

        private void DispatchWearerHit(NPC target, in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            foreach ((Item item, GodSmithEndow endow) in activeEndowAccs) {
                endow.OnWearerHitNPC(item, Player, target, hit, damageDone, fromProjectile, endow.TierScaleFor(item.prefix));
            }
            foreach ((Item item, GodSmithAccEffect effect) in activeAccs) {
                effect.OnHitNPC(item, Player, this, target, hit, damageDone, fromProjectile);
            }
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            foreach ((Item item, GodSmithEndow endow) in activeEndowAccs) {
                endow.ModifyHurt(item, Player, ref modifiers, endow.TierScaleFor(item.prefix));
            }
            foreach ((Item item, GodSmithAccEffect effect) in activeAccs) {
                effect.ModifyHurt(item, Player, this, ref modifiers);
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            foreach ((Item item, GodSmithEndow endow) in activeEndowAccs) {
                endow.OnHurt(item, Player, info, endow.TierScaleFor(item.prefix));
            }
            foreach ((Item item, GodSmithAccEffect effect) in activeAccs) {
                effect.OnHurt(item, Player, this, info);
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (!GameModeSystem.GodSmithActive) {
                return true;
            }
            //首个取消者生效，避免多件护死饰品同帧连环结算
            foreach ((Item item, GodSmithAccEffect effect) in activeAccs) {
                if (!effect.PreKill(item, Player, this, damage, hitDirection, pvp,
                    ref playSound, ref genGore, ref damageSource)) {
                    return false;
                }
            }
            return true;
        }
    }
}
