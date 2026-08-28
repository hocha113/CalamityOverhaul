using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 蜜蜂枪重铸：蜂群共鸣。正拍本波蜜蜂收敛成 V 字编队（散射收拢），
    /// 共鸣每层 +8% 大蜂概率；满层强化「蜂后令」：放出一只蜂后单位（3 倍，
    /// 存续 3s，沿途散出至多六只小蜂）。材质身份：虫群（金尘）。<br/>
    /// 蜂波全接管生成：数量与大小蜂沿用原版口径（player.beeType 系），
    /// 共鸣只在原版掷出小蜂后追掷一次升格
    /// </summary>
    internal class GsBeeGun : GsChantScheme
    {
        public override int TargetItemID => ItemID.BeeGun;

        protected override string GsDescFallback =>
            "Reforged: on-beat volleys fly in a V formation, resonance breeds bigger bees;" +
            "\nat full resonance the next cast releases a queen bee that trails a living swarm";

        protected override float BaseDamageMult => 1.06f;

        protected override Color ChantColor => new(255, 200, 70);

        /// <summary>形态：蜂后单位</summary>
        private const float FormQueen = 10f;

        private static readonly Color HoneyGold = new(232, 164, 44);

        /// <summary>蜂后产蜂状态（端本地计数，生成守 owner）</summary>
        private class QueenState
        {
            public int Spawned;
            public int Timer;
        }

        protected override bool? ChantShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //蜂波全接管：原版口径 2~4 只（强化蜂蜜再 +1），正拍收敛 V 编队
            int count = Main.rand.Next(2, 5);
            if (player.strongBees && Main.rand.NextBool(2)) {
                count++;
            }
            bool onBeat = chant.CurrentBeat == ChantBeat.OnBeat;
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            float speed = velocity.Length();
            for (int i = 0; i < count; i++) {
                int beeType = player.beeType();
                //共鸣育蜂：小蜂按层数 8%/层 追掷升格为大蜂
                if (beeType == ProjectileID.Bee && Main.rand.NextFloat() < 0.08f * chant.ResonanceAtCast) {
                    beeType = ProjectileID.GiantBee;
                }
                Vector2 vel;
                if (onBeat) {
                    //V 编队：中锋领飞，两翼按位阶排开
                    float rank = i - (count - 1) * 0.5f;
                    vel = aim.RotatedBy(MathHelper.ToRadians(4f) * rank) * speed * (1f - Math.Abs(rank) * 0.05f);
                }
                else {
                    vel = aim.RotatedByRandom(0.35) * speed * Main.rand.NextFloat(0.85f, 1.1f);
                }
                Projectile.NewProjectile(source, position, vel, beeType,
                    player.beeDamage(damage), player.beeKB(knockback), player.whoAmI);
            }
            return false;
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //蜂后令：一只 3 倍蜂后单位，沿途产蜂
            QueueForm(player, FormQueen);
            int idx = Projectile.NewProjectile(source, position, velocity * 0.8f,
                ProjectileID.GiantBee, player.beeDamage(damage) * 3, player.beeKB(knockback) * 1.5f,
                player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile queen = Main.projectile[idx];
                queen.scale *= 1.9f;
                queen.timeLeft = 180;
                queen.netUpdate = true;
            }
            return false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //蜂后产蜂：每 30 帧一只、至多六只，扑向最近敌
            if (router.MarkData == FormQueen && proj.IsOwnedByLocalPlayer()) {
                QueenState state = router.GetOrCreateState<QueenState>();
                state.Timer++;
                if (state.Timer >= 30 && state.Spawned < 6) {
                    state.Timer = 0;
                    state.Spawned++;
                    NPC target = FindNearestEnemy(proj.Center, 500f);
                    Vector2 vel = target != null
                        ? (target.Center - proj.Center).SafeNormalize(Vector2.UnitX) * 7f
                        : proj.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.6) * 6f;
                    Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, vel,
                        ProjectileID.Bee, Math.Max(1, (int)(proj.damage * 0.33f)),
                        proj.knockBack * 0.5f, proj.owner);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //飞行相：金尘（蜂本体动画由原版负责，尘是群息）
            bool queen = router.MarkData == FormQueen;
            if (queen) {
                Lighting.AddLight(proj.Center, HoneyGold.ToVector3() * 0.35f);
            }
            int interval = queen ? 4 : 8;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_FarmSpore>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    -proj.velocity * 0.08f, HoneyGold * 0.7f,
                    Main.rand.NextFloat(0.3f, 0.5f) * (queen ? 1.4f : 1f))
                    ?.Configure(Main.rand.Next(12, 20), queen);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //命中相：蜂毒金闪
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(2f, 2f), HoneyGold,
                    Main.rand.NextFloat(0.2f, 0.35f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：金粉飘落，蜂后散场更盛大
            if (VaultUtils.isServer) {
                return;
            }
            int count = router.MarkData == FormQueen ? 6 : 2;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_FarmSpore>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.2f, 0.8f)),
                    HoneyGold * 0.6f, Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(Main.rand.Next(16, 26), false);
            }
        }
    }
}
