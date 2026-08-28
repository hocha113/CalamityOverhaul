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
    /// 乌鸦法杖「凶兆坍缩」：鸦群绕主盘旋成压扁的巡环；
    /// 签名 = 突击令下 60 帧内两只以上不同乌鸦对焦点目标啄满 4 次，
    /// 兆羽向心坍缩成凶兆爆鸣（<see cref="GsRavenOmenProj"/>，1.2×，暗影焰，冷却 130 帧）；
    /// 增强层 = 高速俯冲的影羽涂抹尾迹
    /// </summary>
    internal class GsRavenStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.RavenStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Omen Collapse: under the assault order, four pecks from two different ravens fold the marked prey's shadow into a dire omen that detonates in shadowflame";

        private static readonly Color OmenViolet = new(174, 96, 255);
        private static readonly Color ShadowInk = new(38, 22, 58);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Ring,
            Radius = 92f,
            RotSpeed = 0.005f,
            VerticalSquash = 0.62f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.Raven];

        /// <summary>兆印计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint omenReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.07f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, OmenViolet, ShadowInk);

        //==================== 增强层：俯冲影羽 ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.velocity.Length() < 9f || proj.timeLeft % 3 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                -proj.velocity * 0.04f, OmenViolet, 0.11f)?.Configure(9, 0.65f);
        }

        //==================== 签名：凶兆坍缩 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (!MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            int count = tally.Bump(target, proj, 60, out int distinct);
            if (count >= 4 && distinct >= 2 && Main.GameUpdateCount >= omenReadyTick) {
                omenReadyTick = Main.GameUpdateCount + 130;
                tally.Reset(target);
                //凶兆罩目标经 NewProjectile 形参传入（索引 + 类型校验），随生成包过线
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsRavenOmenProj>(),
                    (int)(proj.damage * 1.2f), 4f, proj.owner,
                    target.whoAmI, target.type);
            }
        }
    }
}
