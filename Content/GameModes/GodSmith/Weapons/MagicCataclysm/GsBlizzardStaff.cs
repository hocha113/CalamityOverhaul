using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 暴雪法杖重铸（P13 左键 rider）。材质身份：极寒冰晶（白灾前奏的霜种）。<br/>
    /// ①左键 rider：每第 5 支冰矢淬成「霜种矢」，消亡时炸开小型霜爆
    /// ②施法有举杖响应。霜种爆 0.5×/5 折算命中率 ≈ +5%，计入包络
    /// </summary>
    internal class GsBlizzardStaff : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.BlizzardStaff;

        protected override string GsDescFallback =>
            "Reforged: every 5th icicle is a rime seed that bursts into a small frost blast where it dies";
        public override int ChargePerHit => 3;

        protected override float PassiveDamageBonus => 0.08f;

        /// <summary>原版冰矢弹类型</summary>
        private static int IcicleType => ContentSamples.ItemsByType[ItemID.BlizzardStaff].shoot;

        /// <summary>冰矢出生计数（打标窗口只在生成端执行，本机契约；原版每次施放降 2 支，逐支计）</summary>
        private int icicleCounter;

        //==================== 动画法：举杖唤雪 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举杖唤雪：杖头抬升 4px 再缓落（绝对剖面 0.1·p，差分施加防累积漂移；本杖动画双发，中途 snap 由差分清账）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -4f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.1f * progress, 0.1f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：霜种矢 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != IcicleType) {
                return;
            }
            //每第 5 支冰矢淬霜种：MarkData=1 随生成包过线，远端同样看到霜种形态
            icicleCounter++;
            if (icicleCounter % 5 == 0) {
                router.MarkData = 1f;
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != IcicleType || router.MarkData < 1f) {
                return;
            }
            //霜种落点：小型霜爆（真弹幕跨端可见；Misc 源不承签，防爆体再袭）
            if (proj.IsOwnedByLocalPlayer()) {
                Player owner = Main.player[proj.owner];
                Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), proj.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                    Math.Max(1, (int)(proj.damage * 0.5f)), 2f, proj.owner,
                    70f, GsCataclysmRiderBurstProj.ThemeFrost);
            }
        }
    }
}
