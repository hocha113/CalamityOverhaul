using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 蜘蛛法杖「织网猎阵」：三角驻位（爬墙优先，引导减半防与攀爬 AI 打架）；
    /// 协同「缚网」= 同目标 60 帧内被两只不同蜘蛛命中满 4 次挂缚网罩
    /// （90 帧，owner 对其 +15%，等价替换原计划的叮附检测：附着态藏在原版 AI 深处不可靠读取）；
    /// 蜘蛛命中必挂原版酸性毒液
    /// </summary>
    internal class GsSpiderStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.SpiderStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Web Hunt: three spider breeds hold a triangle watch; concerted bites truss the prey in a binding web that deepens every wound, and every bite now envenoms";

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Triangle,
            Radius = 80f,
            SectorAnchor = -MathHelper.PiOver2,
            DriftMul = 0.5f,
            Grounded = true,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.VenomSpider, ProjectileID.JumperSpider, ProjectileID.DangerousSpider];

        /// <summary>缚网计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint bindReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        protected override void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //目标身上有自家缚网罩：集火 +15%
            if (MinionDoctrine.FindOwnedProj(proj.owner,
                ModContent.ProjectileType<GsWebBindProj>(), target.Center, 40f) != null) {
                modifiers.FinalDamage *= 1.15f;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //增强层：叮咬必挂酸性毒液
            target.AddBuff(BuffID.Venom, 240);

            int count = tally.Bump(target, proj, 60, out int distinct);
            if (count >= 4 && distinct >= 2 && Main.GameUpdateCount >= bindReadyTick) {
                bindReadyTick = Main.GameUpdateCount + 120;
                tally.Reset(target);
                //缚网罩目标经 NewProjectile 形参传入（索引 + 类型校验），随生成包过线
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsWebBindProj>(), 0, 0f, proj.owner,
                    target.whoAmI, target.type);
            }
        }
    }
}
