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
    /// 风暴海龙卷法杖「合流潮涌」：龙卷列成高空风暴横线；
    /// 签名 = 突击令下龙卷本体卷击与迷你鲨撞击在 45 帧内先后咬中焦点目标，
    /// 两股杀意合流成横扫浪墙（<see cref="GsTempestSurgeProj"/>，1.1×、重击退，冷却 150 帧）；
    /// 增强层 = 迷你鲨飞行拖水沫尾迹
    /// </summary>
    internal class GsTempestStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.TempestStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Confluence Surge: under the assault order, a tempest lash and a mini shark bite landing on one foe in quick succession merge into a sweeping tidal wall that batters everything in its path";

        private static readonly Color SeaBody = new(64, 140, 220);
        private static readonly Color FoamWhite = new(230, 246, 255);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Line,
            Radius = 104f,
            Spacing = 46f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.Tempest, ProjectileID.MiniSharkron];

        //==================== 合流记账（owner 命中路径独占消费） ====================

        /// <summary>龙卷本体最近咬中的焦点与时刻</summary>
        private int tempestNpc = -1;
        private uint tempestTick;
        /// <summary>迷你鲨最近咬中的焦点与时刻</summary>
        private int sharkNpc = -1;
        private uint sharkTick;
        private uint surgeReadyTick;

        /// <summary>双源合流窗口</summary>
        private const int SurgeWindow = 45;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, SeaBody, FoamWhite);

        //==================== 增强层：鲨行水沫 ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.type != ProjectileID.MiniSharkron
                || proj.timeLeft % 3 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                -proj.velocity * 0.04f, SeaBody, 0.11f)?.Configure(9, 0.65f);
        }

        //==================== 签名：合流潮涌 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (!MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            uint now = Main.GameUpdateCount;
            if (proj.type == ProjectileID.Tempest) {
                tempestNpc = target.whoAmI;
                tempestTick = now;
            }
            else if (proj.type == ProjectileID.MiniSharkron) {
                sharkNpc = target.whoAmI;
                sharkTick = now;
            }
            else {
                return;
            }
            //龙卷与鲨在窗口内先后咬中同一焦点 = 合流
            if (tempestNpc != target.whoAmI || sharkNpc != target.whoAmI
                || now > tempestTick + SurgeWindow || now > sharkTick + SurgeWindow
                || now < surgeReadyTick) {
                return;
            }
            surgeReadyTick = now + 150;
            tempestNpc = sharkNpc = -1;

            //浪墙从目标后方 120px 隆起，向焦点方向横扫
            Player owner = Main.player[proj.owner];
            float dir = owner.Center.X <= target.Center.X ? 1f : -1f;
            Vector2 spawnAt = target.Center - new Vector2(dir * 120f, 0f);
            Projectile.NewProjectile(proj.GetSource_FromAI(), spawnAt,
                new Vector2(dir * 6.5f, 0f),
                ModContent.ProjectileType<GsTempestSurgeProj>(),
                (int)(proj.damage * 1.1f), 7.5f, proj.owner);
        }
    }
}
