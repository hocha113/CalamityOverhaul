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
    /// 血红法杖「血弧回航」：光蝠原版扇形绕主已是强势编队，不入阵型系统；
    /// 签名 = 突击令下光蝠咬中焦点目标时撕下一弯回航血弧
    /// （<see cref="GsSanguineArcProj"/>，0.8×，飞回主人沿途割伤，冷却 50 帧）；
    /// 增强层 = 高速俯冲的猩红涂抹尾迹（速度阈值驱动）
    /// </summary>
    internal class GsSanguineStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.SanguineStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Sanguine Homing: under the assault order, each bat bite tears a crescent of clotted blood off the marked prey that wings back to you, cutting whatever stands in its path";

        private static readonly Color BloodBright = new(255, 92, 92);
        private static readonly Color BloodDeep = new(150, 22, 34);

        /// <summary>光蝠原版扇形绕主编队保留，不注册阵型 kit</summary>
        protected override GsMinionKit Kit => null;

        protected override int[] MinionProjTypes => [ProjectileID.BatOfLight];

        /// <summary>撕弧节流（owner 命中路径独占消费）</summary>
        private uint arcReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, BloodBright, BloodDeep);

        //==================== 增强层：俯冲涂抹 ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.velocity.Length() < 6.5f
                || proj.timeLeft % 3 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                -proj.velocity * 0.05f, BloodDeep, 0.12f)?.Configure(10, 0.7f);
        }

        //==================== 签名：血弧回航 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //突击令焦点才撕弧；光蝠一击返航的节奏天然限频，再加节流窗兜底
            if (!MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI
                || Main.GameUpdateCount < arcReadyTick) {
                return;
            }
            arcReadyTick = Main.GameUpdateCount + 50;
            Player owner = Main.player[proj.owner];
            //撕出初速：先甩向主人反侧上方一拍，再由弧体自寻回航
            Vector2 tearVel = (owner.Center - target.Center).SafeNormalize(Vector2.UnitX)
                .RotatedBy(0.85f) * 8.5f;
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, tearVel,
                ModContent.ProjectileType<GsSanguineArcProj>(),
                (int)(proj.damage * 0.8f), 2f, proj.owner);
        }
    }
}
