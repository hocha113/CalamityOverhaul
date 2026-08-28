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
    /// 光学法杖「双瞳协议」（A 档）：机械双瞳成对翼列护卫；
    /// 签名 = 突击令下红瞳激光与绿瞳冲撞在 40 帧内先后咬中焦点目标，
    /// 视线于其身上交汇成 X 形爆闪（<see cref="GsOpticCrossrayProj"/>，1.25×，
    /// 咒焰引燃，冷却 120 帧）；增强层 = 激光红色曳光、冲撞咒绿涂抹（速度驱动，非匀速裸弹）
    /// </summary>
    internal class GsOpticStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.OpticStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Twin Pact: the mini twins fly wing formation; under the assault order, a laser and a ram biting one foe in quick succession cross their sights into an X-flash that ignites cursed flames";

        private static readonly Color RetRed = new(255, 84, 74);
        private static readonly Color SpazGreen = new(128, 255, 96);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Wings,
            Radius = 58f,
            Spacing = 34f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.Retanimini, ProjectileID.Spazmamini, ProjectileID.MiniRetinaLaser];

        //==================== 交叉视线记账（owner 命中路径独占消费） ====================

        /// <summary>最近一次激光咬中的焦点（npc.whoAmI）与时刻</summary>
        private int laserNpc = -1;
        private uint laserTick;
        /// <summary>最近一次冲撞咬中的焦点与时刻</summary>
        private int ramNpc = -1;
        private uint ramTick;
        private uint crossReadyTick;

        /// <summary>双源交汇窗口</summary>
        private const int CrossWindow = 40;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, RetRed, SpazGreen);

        //==================== 增强层：飞行视觉（各端，粒子守服务端） ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            //红瞳激光曳光：拉出与速度同向的红色光粒
            if (proj.type == ProjectileID.MiniRetinaLaser && proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.03f, RetRed, 0.1f)?.Configure(8, 0.75f);
            }
            //绿瞳高速冲撞：咒绿涂抹尾迹
            if (proj.type == ProjectileID.Spazmamini && proj.velocity.Length() > 8f
                && proj.timeLeft % 2 == 0) {
                PRTLoader.NewParticle<PRT_Light>(
                    proj.Center - proj.velocity * 0.6f, -proj.velocity * 0.06f,
                    SpazGreen, 0.13f)?.Configure(10, 0.7f);
            }
        }

        //==================== 签名：交叉视线 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只认突击令焦点，双源分别记账
            if (!MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            uint now = Main.GameUpdateCount;
            if (proj.type == ProjectileID.MiniRetinaLaser) {
                laserNpc = target.whoAmI;
                laserTick = now;
            }
            else if (proj.type == ProjectileID.Spazmamini) {
                ramNpc = target.whoAmI;
                ramTick = now;
            }
            else {
                return;
            }
            //红绿双源在窗口内咬中同一焦点 = 视线交汇
            if (laserNpc != target.whoAmI || ramNpc != target.whoAmI
                || now > laserTick + CrossWindow || now > ramTick + CrossWindow
                || now < crossReadyTick) {
                return;
            }
            crossReadyTick = now + 120;
            laserNpc = ramNpc = -1;
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsOpticCrossrayProj>(),
                (int)(proj.damage * 1.25f), 5f, proj.owner);
        }
    }
}
