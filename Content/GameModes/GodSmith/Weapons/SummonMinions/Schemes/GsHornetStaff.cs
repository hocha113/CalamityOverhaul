using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 黄蜂法杖「蜂巢火网」：侧上方六边蜂巢格悬停；
    /// 协同「毒液共振」= 同目标 120 帧内吃满 3 根毒刺，owner 垂落毒爆孢囊（0.9×，爆后留毒雾）；
    /// 突击技 = 毒刺出膛提速 ×1.3（各端首帧同倍改速，轨迹确定性一致）
    /// </summary>
    internal class GsHornetStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.HornetStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Hive Firenet: hornets hover in a honeycomb lattice; three stingers into one foe burst a venom pod that lingers as poison mist, and the assault order quickens every stinger";

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Hive,
            Radius = 70f,
            Spacing = 34f,
            //侧上扇区锚
            SectorAnchor = -2.2f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.Hornet, ProjectileID.HornetStinger];

        /// <summary>毒刺出膛提速标记（每端本地各标一次）</summary>
        private sealed class StingerState
        {
            public bool Boosted;
        }

        /// <summary>毒液共振计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint podReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.HornetStinger) {
                return;
            }
            //突击技：毒刺出膛帧提速（军旗状态各端一致，首帧同倍缩放随原生同步走）
            StingerState state = router.GetOrCreateState<StingerState>();
            if (!state.Boosted) {
                state.Boosted = true;
                if (MinionDoctrine.GetCommand(proj.owner) == MinionDoctrine.CommandAssault) {
                    proj.velocity *= 1.3f;
                }
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只统计毒刺（本体撞击不计共振）
            if (proj.type != ProjectileID.HornetStinger) {
                return;
            }
            int count = tally.Bump(target, proj, 120, out _);
            if (count >= 3 && Main.GameUpdateCount >= podReadyTick) {
                podReadyTick = Main.GameUpdateCount + 120;
                tally.Reset(target);
                Projectile.NewProjectile(proj.GetSource_FromAI(),
                    target.Center - new Vector2(0f, 100f), Vector2.Zero,
                    ModContent.ProjectileType<GsVenomPodProj>(),
                    (int)(proj.damage * 0.9f), 2f, proj.owner);
            }
        }
    }
}
