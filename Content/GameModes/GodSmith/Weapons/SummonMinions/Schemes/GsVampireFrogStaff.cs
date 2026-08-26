using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 吸血鬼蛙法杖「血蛙盛宴」：贴地双翼护卫；
    /// 协同「吸髓」= 蛙命中 8% 概率（owner 掷，命中钩子本就 owner 独占）溅出血髓珠飞向玩家回 1 血；
    /// 血月全体 +20% 并披红雾描边
    /// </summary>
    internal class GsVampireFrogStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.VampireFrogStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Blood Frog Feast: frogs flank you in twin files; their bites may spill a marrow bead that flies back to heal you, and the blood moon whips them into a crimson frenzy";

        private static readonly Color BloodMist = new(196, 40, 44);

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

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            //血月红雾描边（各端本地按同条件绘制）
            if (!Main.bloodMoon) {
                return;
            }
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            int frames = Math.Max(1, Main.projFrames[proj.type]);
            Rectangle frame = tex.Frame(1, frames, 0, proj.frame % frames);
            float pulse = 0.45f + 0.15f * (float)Math.Sin(
                Main.GlobalTimeWrappedHourly * 5.1f + proj.identity * 0.97f);
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, frame,
                (BloodMist with { A = 0 }) * pulse, proj.rotation, frame.Size() / 2f,
                proj.scale * 1.12f,
                proj.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        }
    }
}
