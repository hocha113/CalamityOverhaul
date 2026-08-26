using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows.Projectiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 蜂膝弓：原版「木箭化蜂箭、命中放蜂」保留（换型在 TransformShootType 复刻）。
    /// T2 命中补放 2 只蜂（吃蜂巢背包换大蜂）；T3 命中点悬置蜂涡 1.5 秒（每 20 帧放 1 蜂共 4 只）。
    /// 活跃蜂 12 只每玩家封顶，超限不再生成
    /// </summary>
    internal class GsBeesKnees : GsChargeBowScheme
    {
        public override int TargetItemID => ItemID.BeesKnees;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Full-drawn bee arrows release extra bees on impact; an overdrawn hit leaves a swirling bee vortex. At most 12 bees may serve you at once";
        internal override float DpsTarget => 1.0f;
        internal override Color TrailMain => new(255, 200, 70);
        internal override Color TrailHot => new(255, 236, 150);
        internal override Color TrailDeep => new(130, 96, 30);

        internal override int TransformShootType(int pickedType, int tier)
            => pickedType == ProjectileID.WoodenArrowFriendly ? ProjectileID.BeeArrow : pickedType;

        /// <summary>活跃蜂计数（普通蜂 + 巨蜂），家族共享上限 12 只每玩家</summary>
        internal static bool BeePoolFull(Player player)
            => player.ownedProjectileCounts[ProjectileID.Bee]
               + player.ownedProjectileCounts[ProjectileID.GiantBee] >= 12;

        /// <summary>owner 端补放蜂，respect 蜂巢背包与蜂池上限</summary>
        internal static void SpawnBee(Player player, IEntitySource source, Vector2 pos, int baseDamage) {
            if (BeePoolFull(player)) {
                return;
            }
            Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
            Projectile.NewProjectile(source, pos, vel, player.beeType(),
                player.beeDamage(baseDamage), player.beeKB(0f), player.whoAmI);
        }

        internal override void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            Player owner = Main.player[proj.owner];
            //T2 起：命中补 2 只蜂（原版蜂箭自带的放蜂由原版逻辑继续跑）
            for (int i = 0; i < 2; i++) {
                SpawnBee(owner, proj.GetSource_FromThis(), target.Center + Main.rand.NextVector2Circular(12f, 12f), 11);
            }
            if (tier < 3 || !ValidRiderTarget(target)) {
                return;
            }
            //蜂后之怒：命中点悬置蜂涡（自定义弹幕走 Misc 源，不承签、逻辑全在自身）
            Projectile.NewProjectile(owner.GetSource_Misc("GsBeeVortex"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsBeeVortexProj>(), 0, 0f, proj.owner);
        }
    }
}
