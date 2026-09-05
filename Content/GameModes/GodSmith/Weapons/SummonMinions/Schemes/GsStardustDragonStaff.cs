using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 星尘之龙法杖「星穹撕裂」（A 档）：龙是链体蠕虫，不入阵型系统，
    /// 绝不碰任何节体速度（链体交给原版跟随 AI）；
    /// 签名 = 突击令下龙首 60 帧内两次凿中焦点目标，在凿点撕开星穹裂隙
    /// （<see cref="GsStardustDragonRiftProj"/>，每段 0.6× 共约 3 段星涌，冷却 150 帧）；
    /// 判定只挂头节（625），节体命中不计数
    /// </summary>
    internal class GsStardustDragonStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.StardustDragonStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Skyrift Piercer: under the assault order, two drills of the dragon's head into the marked foe tear open a star rift at the wound that gushes falling stardust";
        /// <summary>蠕虫链体不入阵型：任何速度牵引都会扯散节间跟随</summary>
        protected override GsMinionKit Kit => null;

        protected override int[] MinionProjTypes
            => [ProjectileID.StardustDragon1, ProjectileID.StardustDragon2,
                ProjectileID.StardustDragon3, ProjectileID.StardustDragon4];

        /// <summary>凿击计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint riftReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        //==================== 签名：星穹撕裂（只认头节） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.StardustDragon1
                || !MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            int count = tally.Bump(target, proj, 60, out _);
            if (count >= 2 && Main.GameUpdateCount >= riftReadyTick) {
                riftReadyTick = Main.GameUpdateCount + 150;
                tally.Reset(target);
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsStardustDragonRiftProj>(),
                    (int)(proj.damage * 0.6f), 3f, proj.owner);
            }
        }
    }
}
