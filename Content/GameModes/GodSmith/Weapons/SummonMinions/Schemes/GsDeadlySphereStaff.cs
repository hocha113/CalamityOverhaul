using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 致命球法杖「绞锯协议」：致命球结成悬浮三角驻位；
    /// 签名 = 突击令下 45 帧内撞中焦点目标满 3 次，拆下一环锯齿铆进目标原地研磨
    /// （<see cref="GsDeadlySphereSawProj"/>，每段 0.45× 共约 3 段，冷却 90 帧，
    /// 同一目标同时只铆一环）
    /// </summary>
    internal class GsDeadlySphereStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.DeadlySphereStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Grinder Protocol: under the assault order, three sphere slams within a breath rivet a whirling saw ring into the marked foe, grinding it with sparking steel teeth";
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
