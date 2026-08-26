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
    /// 雀鸟法杖「群雀阵」：头顶 V 字雁行；
    /// 协同「俯冲链」= 首只命中后 60 帧内其余命中逐层 +15%（至多 3 层，带俯冲速度线）；
    /// 集结 = 旗点盘旋鸟群（0.35× 啄击圈）。原版带杖自动召一只的特性不碰
    /// </summary>
    internal class GsFinchStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.BabyBirdStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Finch Phalanx: finches fly a V formation; after the first strike, follow-up pecks on that foe gain stacking power, and the rally order forms a wheeling flock";

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Vee,
            Radius = 62f,
            Spacing = 30f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.BabyBird];

        /// <summary>俯冲链层数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint fieldReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router)
            => TryKeepRallyField(proj, GsRallyFieldProj.StanceFlock, 0.35f, 0.6f, ref fieldReadyTick);

        protected override void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            int layers = System.Math.Min(tally.Peek(target), 3);
            if (layers > 0) {
                modifiers.FinalDamage *= 1f + 0.15f * layers;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            int count = tally.Bump(target, proj, 60, out _);
            if (count < 2 || VaultUtils.isServer) {
                return;
            }
            //俯冲链反馈：沿来向拉出速度线（≤3 粒）
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    target.Center - dir * (14f + i * 10f),
                    dir * Main.rand.NextFloat(3f, 5f),
                    new Color(255, 208, 128), Main.rand.NextFloat(0.1f, 0.16f))?.Configure(10, 0.8f);
            }
        }
    }
}
