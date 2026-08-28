using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 致命球法杖「绞锯协议」：致命球结成悬浮三角驻位；
    /// 签名 = 突击令下 45 帧内撞中焦点目标满 3 次，拆下一环锯齿铆进目标原地研磨
    /// （<see cref="GsDeadlySphereSawProj"/>，每段 0.45× 共约 3 段，冷却 90 帧，
    /// 同一目标同时只铆一环）；增强层 = 高速突进的钢灰残影
    /// </summary>
    internal class GsDeadlySphereStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.DeadlySphereStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Grinder Protocol: under the assault order, three sphere slams within a breath rivet a whirling saw ring into the marked foe, grinding it with sparking steel teeth";

        private static readonly Color SteelGray = new(168, 172, 186);
        private static readonly Color FrictionOrange = new(255, 148, 54);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Triangle,
            Radius = 66f,
            SectorAnchor = -MathHelper.PiOver2,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.DeadlySphere];

        /// <summary>绞锯计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint sawReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, SteelGray, FrictionOrange);

        //==================== 增强层：突进残影 ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.type != ProjectileID.DeadlySphere
                || proj.velocity.Length() < 10f || proj.timeLeft % 3 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.6f,
                -proj.velocity * 0.05f, SteelGray, 0.12f)?.Configure(9, 0.6f);
        }

        //==================== 签名：铆锯 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //锯环自身的研磨不回喂计数
            if (proj.type != ProjectileID.DeadlySphere
                || !MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            int count = tally.Bump(target, proj, 45, out _);
            //同一目标身上只铆一环
            if (count < 3 || Main.GameUpdateCount < sawReadyTick
                || MinionDoctrine.FindOwnedProj(proj.owner,
                    ModContent.ProjectileType<GsDeadlySphereSawProj>(), target.Center, 60f) != null) {
                return;
            }
            sawReadyTick = Main.GameUpdateCount + 90;
            tally.Reset(target);
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsDeadlySphereSawProj>(),
                (int)(proj.damage * 0.45f), 2f, proj.owner,
                target.whoAmI, target.type);
        }
    }
}
