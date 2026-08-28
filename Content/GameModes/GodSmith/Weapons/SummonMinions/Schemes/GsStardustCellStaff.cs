using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 星尘细胞法杖「有丝分裂」：细胞群结成贴身快转的原生质环阵；
    /// 签名 = 突击令下焦点目标身上附着的小细胞（原版附着弹）满 3 只时，
    /// 细胞本体的下一次撞击引发分裂潮：五枚分裂胞子四散寻的
    /// （<see cref="GsStardustCellMoteProj"/>，各 0.5×，重挂细胞侵蚀，冷却 140 帧）；
    /// 读附着态走弹幕扫描（ai[0]=1 且 ai[1]=目标），不摸原版 AI 本体
    /// </summary>
    internal class GsStardustCellStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.StardustCellStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Mitosis Tide: under the assault order, once three little cells cling to the marked foe, the next cell slam splits into five seeking spores that each deepen the cellular corrosion";

        private static readonly Color CellCyan = new(94, 202, 238);
        private static readonly Color NucleusBlue = new(58, 118, 236);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Ring,
            Radius = 54f,
            RotSpeed = 0.01f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.StardustCellMinion, ProjectileID.StardustCellMinionShot];

        /// <summary>分裂潮节流（owner 命中路径独占消费）</summary>
        private uint mitosisReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, CellCyan, NucleusBlue);

        //==================== 签名：有丝分裂 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只认细胞本体撞击；附着小细胞与胞子不触发（防自喂）
            if (proj.type != ProjectileID.StardustCellMinion
                || Main.GameUpdateCount < mitosisReadyTick
                || !MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI
                || CountLatchedCells(proj.owner, target) < 3) {
                return;
            }
            mitosisReadyTick = Main.GameUpdateCount + 140;
            //五枚胞子扇形撕出，初速经生成形参过线
            for (int i = 0; i < 5; i++) {
                float ang = -MathHelper.PiOver2 + (i - 2) * 0.55f
                    + Main.rand.NextFloat(-0.12f, 0.12f);
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(5f, 7f),
                    ModContent.ProjectileType<GsStardustCellMoteProj>(),
                    (int)(proj.damage * 0.5f), 1f, proj.owner);
            }
        }

        /// <summary>数焦点目标身上附着的自家小细胞（原版附着态：ai[0]=1，ai[1]=目标索引）</summary>
        private static int CountLatchedCells(int owner, NPC target) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == owner && proj.type == ProjectileID.StardustCellMinionShot
                    && proj.ai[0] == 1f && (int)proj.ai[1] == target.whoAmI) {
                    count++;
                }
            }
            return count;
        }
    }
}
