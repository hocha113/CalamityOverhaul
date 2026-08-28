using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 外星法杖「轨道校准」（A 档）：UFO 舰群结成蜂巢天顶阵；
    /// 签名 = 突击令下激光命中焦点目标积攒校准，50 帧内满 4 束即呼叫轨道歼灭光矛
    /// （<see cref="GsXenoOrbitalProj"/>，1.35×，锁定/光矛/电离三相演出，冷却 160 帧）；
    /// 增强层 = UFO 相位跳跃落点闪现全息环、激光命中溅离子光尘
    /// </summary>
    internal class GsXenoStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.XenoStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Orbital Calibration: under the assault order, four lasers into the marked foe finish the calibration and call down an annihilation lance from high orbit";

        private static readonly Color IonLime = new(150, 255, 96);
        private static readonly Color IonTeal = new(66, 214, 198);

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

        /// <summary>相位跳跃检测：UFO 上帧位置（各端本地视觉用）</summary>
        private readonly Dictionary<int, Vector2> lastUfoPos = [];

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, IonLime, IonTeal);

        //==================== 增强层：相位跳跃闪现环 ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.type != ProjectileID.UFOMinion) {
                return;
            }
            //瞬移检测：单帧位移远超飞行速度即视为相位跳跃（各端本地各自可见）
            if (lastUfoPos.TryGetValue(proj.whoAmI, out Vector2 prev)
                && prev.Distance(proj.Center) > 120f) {
                for (int i = 0; i < 6; i++) {
                    float ang = i / 6f * MathHelper.TwoPi;
                    PRTLoader.NewParticle<PRT_Light>(proj.Center + ang.ToRotationVector2() * 14f,
                        ang.ToRotationVector2() * 1.8f, IonTeal, 0.11f)?.Configure(12, 0.8f);
                }
            }
            lastUfoPos[proj.whoAmI] = proj.Center;
            if (lastUfoPos.Count > 64) {
                lastUfoPos.Clear();
            }
        }

        //==================== 签名：轨道校准 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.UFOLaser) {
                return;
            }
            //激光命中溅离子光尘（owner 端反馈）
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(target.Center,
                    -proj.velocity.SafeNormalize(Vector2.UnitY) * 2f,
                    IonLime, 0.12f)?.Configure(10, 0.8f);
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
