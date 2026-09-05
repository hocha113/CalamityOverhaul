using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 微光镜「虹彩终幕」（A 档）：棱镜剑群保留原版绕主光环（不入阵型系统）；
    /// 签名 = 突击令下 70 帧内两柄以上不同棱镜剑对焦点目标斩满 6 剑，
    /// 举行处决仪式：六柄虚像剑展扇序贯贯穿（<see cref="GsEmpressFinaleProj"/>，
    /// 每剑 0.35× 共六剑，展扇/连刺/碎光/余彩四相，冷却 180 帧）
    /// </summary>
    internal class GsEmpressBlade : GsMinionScheme
    {
        public override int TargetItemID => ItemID.EmpressBlade;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Prismatic Finale: under the assault order, six slashes from at least two blades open the execution rite; six phantom blades fan out above the marked prey and plunge through it one by one in rainbow order";
        /// <summary>棱镜剑原版绕主光环已是签名编队，不注册阵型 kit</summary>
        protected override GsMinionKit Kit => null;

        protected override int[] MinionProjTypes => [ProjectileID.EmpressBlade];

        /// <summary>刻痕计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint finaleReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        //==================== 签名：虹彩终幕 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (!MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            int count = tally.Bump(target, proj, 70, out int distinct);
            if (count >= 6 && distinct >= 2 && Main.GameUpdateCount >= finaleReadyTick) {
                finaleReadyTick = Main.GameUpdateCount + 180;
                tally.Reset(target);
                //终幕锁定目标经 NewProjectile 形参传入（索引 + 类型校验）
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsEmpressFinaleProj>(),
                    (int)(proj.damage * 0.35f), 3f, proj.owner,
                    target.whoAmI, target.type);
            }
        }
    }
}
