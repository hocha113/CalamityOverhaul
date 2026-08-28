using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 灰色滋滋枪重铸：混沌节拍化。随机效果池原样保留（经典味不动）；
    /// 正拍光束必然滋出高压段（1.3 倍）；满层强化「超载滋滋」：单发 2.5 倍
    /// 超载光束，穿透 +2、体积膨大。免蓝语义原样保留。材质身份：混沌电（灰白）。<br/>
    /// 与设计的偏差：原版随机效果硬编码在弹幕 AI 内不可安全拦截，
    /// 「锁定重现」按计划兜底降级为「正拍必 roll 高伤段」，只动伤害乘区不碰效果池
    /// </summary>
    internal class GsGrayZapinator : GsChantScheme
    {
        public override int TargetItemID => ItemID.ZapinatorGray;

        protected override string GsDescFallback =>
            "Reforged: chaos stays chaos, but on-beat zaps always surge high voltage;" +
            "\nat full resonance the next zap overloads into a swollen piercing beam";

        //期望值控制 110%，正拍高压段已计入
        protected override float BaseDamageMult => 1.04f;

        //免蓝武器：法力经济不动
        protected override float OnBeatManaRefund => 0f;

        protected override Color ChantColor => new(200, 208, 220);

        private static readonly Color VoltWhite = new(238, 244, 255);

        protected override void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //正拍必 roll 高压段：混沌之上叠一层确定性
            if (chant.CurrentBeat == ChantBeat.OnBeat) {
                damage = (int)(damage * 1.3f);
            }
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //超载滋滋：单发膨大穿透光束（随机效果池由原版弹幕 AI 照常掷）
            int idx = Projectile.NewProjectile(source, position, velocity, type,
                Math.Max(1, (int)(damage * 2.5f)), knockback * 1.3f, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile beam = Main.projectile[idx];
                beam.scale *= 1.4f;
                if (beam.penetrate > 0) {
                    beam.penetrate += 2;
                }
                beam.netUpdate = true;
            }
            return false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            bool hot = router.MarkData is FormOnBeat or FormEmpower;
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * (hot ? 0.3f : 0.16f));
            //飞行相：混沌电的灰白噪点闪（identity 错相抖闪，不掷判定随机）
            int interval = hot ? 3 : 6;
            if (proj.timeLeft % interval == 0) {
                bool flick = ((proj.identity * 2654435761u + (uint)proj.timeLeft) & 3) == 0;
                PRTLoader.NewParticle<PRT_Spark>(proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -proj.velocity * 0.04f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    flick ? VoltWhite : ChantColor,
                    Main.rand.NextFloat(0.2f, 0.36f))?.Configure(false, Main.rand.Next(6, 12));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //命中相：电弧炸裂
            if (VaultUtils.isServer) {
                return;
            }
            int count = router.MarkData == FormEmpower ? 6 : 4;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(2f, 2f),
                    i % 2 == 0 ? ChantColor : VoltWhite, Main.rand.NextFloat(0.4f, 0.65f));
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：噪点残电
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    Vector2.Zero, i == 0 ? VoltWhite : ChantColor, Main.rand.NextFloat(0.3f, 0.5f));
            }
        }
    }
}
