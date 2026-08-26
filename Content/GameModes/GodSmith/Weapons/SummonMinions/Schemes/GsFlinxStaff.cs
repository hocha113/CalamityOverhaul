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
    /// 雪精灵法杖「滚雪成崩」：纵队接力小跳；
    /// 协同「雪崩撞」= 同目标 90 帧窗内第 4 次滚撞伤害 ×1.5，炸冰尘并挂原版霜火；
    /// 集结 = 旗点堆雪障（0.3× 蹭伤 + 霜火）
    /// </summary>
    internal class GsFlinxStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.FlinxStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Snowball Avalanche: flinxes bound in a relay column; the fourth tackle on one foe within the window lands half again harder and frostburns, and the rally order piles a snow drift";

        private static readonly Color FrostPale = new(214, 238, 255);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Column,
            Radius = 40f,
            Spacing = 24f,
            Grounded = true,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.FlinxMinion];

        /// <summary>雪崩撞计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint fieldReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router)
            => TryKeepRallyField(proj, GsRallyFieldProj.StanceSnowDrift, 0.3f, 1.5f, ref fieldReadyTick);

        protected override void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //第 4 撞（此前已记 3 撞）升级
            if (tally.Peek(target) == 3) {
                modifiers.FinalDamage *= 1.5f;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            int count = tally.Bump(target, proj, 90, out _);
            if (count < 4) {
                return;
            }
            //雪崩撞落地：清窗 + 霜火 + 冰尘爆
            tally.Reset(target);
            target.AddBuff(BuffID.Frostburn, 240);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust snow = Dust.NewDustPerfect(target.Center
                    + Main.rand.NextVector2Circular(14f, 14f), DustID.Snow,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 60, default, 1.3f);
                snow.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f),
                    FrostPale, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(14, 22));
            }
        }
    }
}
