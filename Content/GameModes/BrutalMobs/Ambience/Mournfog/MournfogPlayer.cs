using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mournfog.Projectiles;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mournfog
{
    /// <summary>
    /// 怨聚累积（逐玩家状态，禁 static）：玩家在墓地里以锚点为圆心久待不动，
    /// 内部计时涨满后由权威端在其脚下起一圈怨聚鬼火环（表现全程是鬼火物理围拢，
    /// 见 <see cref="MournfogGrudgeRingProj"/>）。移动超过锚距或离开墓地立即清零；
    /// Boss 在场 / 城镇安宁（60 格内有城镇 NPC）时累积回退不触发。
    /// 各端同规则镜像累积：客户端镜像只喂本机漫飘鬼火的转红演出，生成决策只在权威端
    /// </summary>
    internal class MournfogPlayer : ModPlayer
    {
        /// <summary>累积需求（帧），档位只调累积速度：残酷 17.5s / 修罗 14.5s / 毁灭 12s</summary>
        private static readonly int[] StillNeedByTier = [1050, 870, 720];
        /// <summary>锚距：超过即视为"移动足够距离"，累积清零重锚</summary>
        private const float MoveResetDist = 230f;
        /// <summary>一轮怨聚后的冷却（约 22.5s）</summary>
        private const int RingCooldownFrames = 1350;
        /// <summary>怨聚环全局并发上限</summary>
        private const int RingCap = 3;
        /// <summary>城镇安宁半径（60 格）</summary>
        private const float TownCalmRangePx = 960f;
        /// <summary>环伤害目标实收 = 墓地原版乌鸦经典接触伤 × 此值（微量，档位不加伤）</summary>
        private const float RingDamageFrac = 0.4f;

        /// <summary>静止累积帧数</summary>
        private int stillTicks;
        /// <summary>久待锚点</summary>
        private Vector2 anchor;
        /// <summary>触发后的冷却</summary>
        private int cooldown;
        /// <summary>城镇安宁缓存（30 帧刷新一次，避免逐帧扫 NPC 表）</summary>
        private bool townCalm;
        private int townCheckIn;

        /// <summary>本机漫飘鬼火的转红度 0~1（纯演出读数）</summary>
        internal float RedShift {
            get {
                int tier = GameModeSystem.EffectiveTier;
                if (tier <= 0 || cooldown > 0) {
                    return 0f;
                }
                return MathHelper.Clamp(stillTicks / (float)StillNeedByTier[tier - 1], 0f, 1f);
            }
        }

        private void ResetGather() {
            stillTicks = 0;
            anchor = Player.Center;
        }

        public override void OnEnterWorld() {
            ResetGather();
            cooldown = 0;
        }

        public override void UpdateDead() {
            ResetGather();
        }

        public override void PostUpdate() {
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0 || !Player.ZoneGraveyard) {
                ResetGather();
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                anchor = Player.Center;
                return;
            }
            //移动足够距离：清零并重锚（锚点语义=在这一带扎营，小碎步不重置）
            if (Player.Center.Distance(anchor) > MoveResetDist) {
                ResetGather();
                return;
            }

            //Boss 在场 / 城镇安宁：伤害性机制不触发，累积缓慢回退
            if (--townCheckIn <= 0) {
                townCheckIn = 30;
                townCalm = TownNpcNearby();
            }
            if (CWRWorld.HasBoss || townCalm) {
                stillTicks = Math.Max(0, stillTicks - 2);
                return;
            }

            if (++stillTicks < StillNeedByTier[tier - 1]) {
                return;
            }

            //涨满：各端同步清零进冷却（客户端镜像的转红演出随之熄灭），生成只在权威端
            stillTicks = 0;
            cooldown = RingCooldownFrames;
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (CountActiveRings() >= RingCap) {
                return;
            }
            Projectile.NewProjectile(Player.GetSource_Misc("MournfogGrudge"), Player.Center,
                Vector2.Zero, ModContent.ProjectileType<MournfogGrudgeRingProj>(),
                RingDamage(), 1f, Main.myPlayer, Player.whoAmI, tier);
        }

        private bool TownNpcNearby() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(Player.Center) < TownCalmRangePx) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计活动怨聚环数（只在触发帧调用，非每帧）</summary>
        private static int CountActiveRings() {
            int type = ModContent.ProjectileType<MournfogGrudgeRingProj>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && ++count >= RingCap) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 环伤害锚定：弹幕 damage = 乌鸦经典接触伤 × RingDamageFrac ÷ 2。
        /// 已预除原版敌对弹幕 ×2 结算系数，引擎自做难度缩放（经典 ×2 / 专家 ×4 / 大师 ×6），
        /// 各难度实收恒等于同难度接触伤 × RingDamageFrac，禁再叠手动难度乘数
        /// </summary>
        private static int RingDamage() {
            int baseDamage = 45;
            if (ContentSamples.NpcsByNetId.TryGetValue(NPCID.Raven, out NPC sample) && sample.damage > 0) {
                baseDamage = sample.damage;
            }
            return Math.Max(1, (int)(baseDamage * RingDamageFrac * 0.5f));
        }
    }
}
