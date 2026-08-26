using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 史莱姆法杖「凝胶军团」：脚边环绕滚动队列；
    /// 协同「融胶爆」= 同一目标 45 帧内被两只不同史莱姆命中，owner 生成凝胶震波（0.8×，冷却 90 帧）；
    /// 集结 = 旗点堆起弹性胶垛（0.5× 粘击 + 原版黏滑）。公认最弱武器给 135% 上限档
    /// </summary>
    internal class GsSlimeStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.SlimeStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Gel Legion: slimes roll in escort formation; two different slimes striking one foe within a beat triggers a gel burst, and the rally order piles them into a sticky mound";

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Ring,
            Radius = 60f,
            Spacing = 26f,
            Grounded = true,
            VerticalSquash = 0.35f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.BabySlime];

        /// <summary>融胶爆命中窗计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        /// <summary>融胶爆冷却</summary>
        private uint burstReadyTick;
        /// <summary>胶垛生成防抖</summary>
        private uint fieldReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router)
            => TryKeepRallyField(proj, GsRallyFieldProj.StanceGelMound, 0.5f, 2f, ref fieldReadyTick);

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            int count = tally.Bump(target, proj, 45, out int distinct);
            if (count >= 2 && distinct >= 2 && Main.GameUpdateCount >= burstReadyTick) {
                burstReadyTick = Main.GameUpdateCount + 90;
                tally.Reset(target);
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsGelBurstProj>(),
                    (int)(proj.damage * 0.8f), 3f, proj.owner);
            }
        }
    }
}
