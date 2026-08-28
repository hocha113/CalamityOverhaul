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
    /// 沙漠虎杖「伏杀爪撕」：沙漠虎是独兽（宝石计槽 831 不认领、也不入阵型，
    /// 地面扑杀 AI 全权原版）；签名 = 突击令下虎爪 50 帧内抓中焦点目标满 3 次，
    /// 施展伏杀仪式：三道风沙爪痕序贯撕开（<see cref="GsStormTigerRendProj"/>，
    /// 每道 0.55×，冷却随虎阶缩短 150/120/90 帧）；增强层 = 高速扑击的扬沙尾迹
    /// </summary>
    internal class GsStormTigerStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.StormTigerStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Ambush Rend: under the assault order, three tiger strikes within a breath unleash the kill rite; three storm-sand claw tears rip through the marked prey, and a mightier tiger stalks a shorter cooldown";

        private static readonly Color SandAmber = new(232, 186, 108);
        private static readonly Color SandDeep = new(150, 108, 56);

        /// <summary>独兽不入阵型，扑杀路线全权原版 AI</summary>
        protected override GsMinionKit Kit => null;

        protected override int[] MinionProjTypes
            => [ProjectileID.StormTigerAttack, ProjectileID.StormTigerTier1,
                ProjectileID.StormTigerTier2, ProjectileID.StormTigerTier3];

        /// <summary>爪击计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint rendReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, SandAmber, SandDeep);

        //==================== 增强层：扑击扬沙 ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            //只有虎体（各阶）高速扑击时扬沙，攻击斩击弹不加
            if (VaultUtils.isServer || proj.type == ProjectileID.StormTigerAttack
                || proj.velocity.Length() < 8f || proj.timeLeft % 3 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Spark>(
                proj.Center - proj.velocity * 0.4f + new Vector2(0f, proj.height * 0.3f),
                new Vector2(-proj.velocity.X * 0.06f, -Main.rand.NextFloat(0.4f, 1f)),
                Main.rand.NextBool() ? SandAmber : SandDeep,
                Main.rand.NextFloat(0.18f, 0.3f))?.Configure(true, Main.rand.Next(10, 16));
        }

        //==================== 签名：伏杀爪撕 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (!MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            int count = tally.Bump(target, proj, 50, out _);
            //同一目标身上只演一场伏杀
            if (count < 3 || Main.GameUpdateCount < rendReadyTick
                || MinionDoctrine.FindOwnedProj(proj.owner,
                    ModContent.ProjectileType<GsStormTigerRendProj>(), target.Center, 70f) != null) {
                return;
            }
            rendReadyTick = Main.GameUpdateCount + (uint)RendCooldown(proj.owner);
            tally.Reset(target);
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsStormTigerRendProj>(),
                (int)(proj.damage * 0.55f), 4f, proj.owner,
                target.whoAmI, target.type);
        }

        /// <summary>虎阶越高伏杀越勤：按在场虎体的阶级取冷却</summary>
        private static int RendCooldown(int owner) {
            Player player = Main.player[owner];
            if (player.ownedProjectileCounts[ProjectileID.StormTigerTier3] > 0) {
                return 90;
            }
            if (player.ownedProjectileCounts[ProjectileID.StormTigerTier2] > 0) {
                return 120;
            }
            return 150;
        }
    }
}
