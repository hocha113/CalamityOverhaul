using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 微光镜「虹彩终幕」（A 档）：棱镜剑群保留原版绕主光环（不入阵型系统）；
    /// 签名 = 突击令下 70 帧内两柄以上不同棱镜剑对焦点目标斩满 6 剑，
    /// 举行处决仪式：六柄虚像剑展扇序贯贯穿（<see cref="GsEmpressFinaleProj"/>，
    /// 每剑 0.35× 共六剑，展扇/连刺/碎光/余彩四相，冷却 180 帧）；
    /// 增强层 = 每柄剑冲刺拖 identity 定相的虹彩涂抹
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

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player,
                Main.hslToRgb(Main.GlobalTimeWrappedHourly % 1f, 1f, 0.66f), Color.White);

        //==================== 增强层：虹彩涂抹 ====================

        /// <summary>每柄剑的专属 hue（identity 定相，同一柄剑颜色恒定）</summary>
        private static Color BladeTint(Projectile proj, float lum = 0.64f)
            => Main.hslToRgb(proj.identity * 0.1618f % 1f, 1f, lum);

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.velocity.Length() < 8f || proj.timeLeft % 2 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                -proj.velocity * 0.04f, BladeTint(proj), 0.12f)?.Configure(10, 0.75f);
        }

        /// <summary>剑体加色残影：速度拉伸的虹彩涂层（绘制禁随机，identity 定相）</summary>
        public override void GsProjPostDraw(Projectile proj, Color lightColor,
            GodSmithProjRouter router) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float speed = proj.velocity.Length();
            float stretch = 1f + MathHelper.Clamp(speed * 0.08f, 0f, 1.1f);
            float breathe = 0.75f + 0.25f * (float)Math.Sin(
                Main.GlobalTimeWrappedHourly * 5f + proj.identity * 1.3f);
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null,
                (BladeTint(proj) with { A = 0 }) * (0.33f * breathe),
                proj.velocity.ToRotation(), glow.Size() / 2f,
                new Vector2(0.42f * stretch, 0.26f), SpriteEffects.None, 0);
        }

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
