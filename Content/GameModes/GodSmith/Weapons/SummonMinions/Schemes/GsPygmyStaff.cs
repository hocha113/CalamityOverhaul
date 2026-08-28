using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 矮人妖法杖「猎首战鼓」：矮人妖列成猎手横排；
    /// 签名 = 集结令下投矛命中即在旗点竖起猎首图腾（<see cref="GsPygmyTotemProj"/>，
    /// 战鼓光环内自家仆从 +10%，图腾一次只立一座，跟随旗令换防）；
    /// 增强层 = 光环内的投矛命中补深蛊毒（原版毒液续长）
    /// </summary>
    internal class GsPygmyStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.PygmyStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Headhunt Drums: under the rally order, a spear strike raises a totem at the flag; inside its war-drum ring your minions hit 10% harder and pygmy spears steep their venom deeper";

        private static readonly Color FeatherGreen = new(112, 200, 96);
        private static readonly Color TorchOrange = new(255, 156, 60);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Line,
            Radius = 54f,
            Spacing = 36f,
            Grounded = true,
            DriftMul = 0.8f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.Pygmy, ProjectileID.Pygmy2, ProjectileID.Pygmy3,
                ProjectileID.Pygmy4, ProjectileID.PygmySpear];

        /// <summary>立图腾节流（owner 命中路径独占消费）</summary>
        private uint totemReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, FeatherGreen, TorchOrange);

        //==================== 战鼓光环增伤 ====================

        protected override void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (MinionDoctrine.FindOwnedProj(proj.owner,
                ModContent.ProjectileType<GsPygmyTotemProj>(), target.Center,
                GsPygmyTotemProj.AuraRadius) != null) {
                modifiers.FinalDamage *= 1.10f;
            }
        }

        //==================== 签名：立图腾 + 光环内蛊毒 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.PygmySpear) {
                return;
            }
            //光环内的投矛把毒蛊得更深（原版毒液续长，骑原版 buff 同步）
            if (MinionDoctrine.FindOwnedProj(proj.owner,
                ModContent.ProjectileType<GsPygmyTotemProj>(), target.Center,
                GsPygmyTotemProj.AuraRadius) != null) {
                target.AddBuff(BuffID.Venom, 300);
            }

            //集结令下竖图腾：一次一座，旗点换位即随下次命中重立
            if (Main.GameUpdateCount < totemReadyTick
                || MinionDoctrine.GetCommand(proj.owner) != MinionDoctrine.CommandRally
                || !MinionDoctrine.TryGetRallyPoint(proj.owner, out Vector2 flagPoint)
                || Main.player[proj.owner].ownedProjectileCounts[
                    ModContent.ProjectileType<GsPygmyTotemProj>()] > 0) {
                return;
            }
            totemReadyTick = Main.GameUpdateCount + 60;
            Projectile.NewProjectile(proj.GetSource_FromAI(),
                flagPoint + new Vector2(0f, 6f), Vector2.Zero,
                ModContent.ProjectileType<GsPygmyTotemProj>(), 0, 0f, proj.owner);
        }
    }
}
