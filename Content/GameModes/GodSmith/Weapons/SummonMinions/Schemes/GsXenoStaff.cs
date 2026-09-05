using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 外星法杖「轨道校准」（A 档）：UFO 舰群结成蜂巢天顶阵；
    /// 签名 = 突击令下激光命中焦点目标积攒校准，50 帧内满 4 束即呼叫轨道歼灭光矛
    /// （<see cref="GsXenoOrbitalProj"/>，1.35×，锁定/光矛/电离三相，冷却 160 帧）
    /// </summary>
    internal class GsXenoStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.XenoStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Orbital Calibration: under the assault order, four lasers into the marked foe finish the calibration and call down an annihilation lance from high orbit";
        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Hive,
            Radius = 84f,
            Spacing = 38f,
            SectorAnchor = -MathHelper.PiOver2,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.UFOMinion, ProjectileID.UFOLaser];

        /// <summary>校准计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint orbitalReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        //==================== 签名：轨道校准 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.UFOLaser) {
                return;
            }
            if (!MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            int count = tally.Bump(target, proj, 50, out _);
            if (count >= 4 && Main.GameUpdateCount >= orbitalReadyTick) {
                orbitalReadyTick = Main.GameUpdateCount + 160;
                tally.Reset(target);
                //歼灭矛锁定目标经 NewProjectile 形参传入（索引 + 类型校验）
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsXenoOrbitalProj>(),
                    (int)(proj.damage * 1.35f), 6f, proj.owner,
                    target.whoAmI, target.type);
            }
        }
    }
}
