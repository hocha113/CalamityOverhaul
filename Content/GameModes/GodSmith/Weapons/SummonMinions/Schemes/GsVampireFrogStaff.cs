using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 吸血鬼蛙法杖「血蛙盛宴」：贴地双翼护卫；
    /// 协同「吸髓」= 蛙命中 8% 概率（owner 掷，命中钩子本就 owner 独占）溅出血髓珠飞向玩家回 1 血；
    /// 血月全体 +20%
    /// </summary>
    internal class GsVampireFrogStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.VampireFrogStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Blood Frog Feast: frogs flank you in twin files; their bites may spill a marrow bead that flies back to heal you, and the blood moon whips them into a crimson frenzy";
        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Wings,
            Radius = 46f,
            Spacing = 28f,
            Grounded = true,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.VampireFrog];

        /// <summary>吸髓冷却（owner 命中路径独占消费）</summary>
        private uint sipReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;

        protected override void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (Main.bloodMoon) {
                modifiers.FinalDamage *= 1.2f;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //吸髓：owner 端掷签 8%（本钩子只在攻击方端执行，天然安全）
            if (Main.GameUpdateCount < sipReadyTick || !Main.rand.NextBool(8, 100)) {
                return;
            }
            sipReadyTick = Main.GameUpdateCount + 30;
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center,
                new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 4f)),
                ModContent.ProjectileType<GsBloodSipProj>(), 0, 0f, proj.owner);
        }
    }
}
