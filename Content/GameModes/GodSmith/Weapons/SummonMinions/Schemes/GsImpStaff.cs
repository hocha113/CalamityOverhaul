using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 小恶魔法杖「狱火炮列」：后方横排炮列；
    /// 协同「聚火令」= 突击指令下同目标 30 帧内吃满 3 发火球，owner 在其脚下竖起狱炎柱
    /// （1.2×，喷发/舔舐/熄灭三相，冷却 150 帧）；火球命中补刷原版着火
    /// </summary>
    internal class GsImpStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.ImpStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Hellfire Battery: imps hold a rear artillery line; under the assault order, three fireballs into one foe raise a pyre column beneath it";

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Line,
            Radius = 70f,
            Spacing = 40f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.FlyingImp, ProjectileID.ImpFireball];

        /// <summary>聚火令计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint pyreReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.ImpFireball) {
                return;
            }
            //火球命中补刷着火（增强层，owner 端 AddBuff 骑原版 buff 同步）
            target.AddBuff(BuffID.OnFire, 240);

            //聚火令只在突击指令下生效
            if (MinionDoctrine.GetCommand(proj.owner) != MinionDoctrine.CommandAssault) {
                return;
            }
            int count = tally.Bump(target, proj, 30, out _);
            if (count >= 3 && Main.GameUpdateCount >= pyreReadyTick) {
                pyreReadyTick = Main.GameUpdateCount + 150;
                tally.Reset(target);
                //弹幕中心上提，令柱底（Center + 高度半程）恰落在目标脚底
                Projectile.NewProjectile(proj.GetSource_FromAI(),
                    target.Bottom - new Vector2(0f, 60f), Vector2.Zero,
                    ModContent.ProjectileType<GsImpPyreProj>(),
                    (int)(proj.damage * 1.2f), 4f, proj.owner);
            }
        }
    }
}
