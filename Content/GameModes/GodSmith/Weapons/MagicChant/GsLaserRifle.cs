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
    /// 激光枪重铸：扳机节奏。每三次连续正拍，第三发化为平行双联激光（各 0.75 倍）；
    /// 满层强化「过热扫射」：0.8s 内射速 1.6 倍、弹道 ±4 度扫摆，随后 40 帧
    /// 射速回落一成的疲软期。材质身份：相干光（紫）。<br/>
    /// 寄存器语义：CounterA = 连续正拍计数，TimerA/TimerB = 扫射窗/疲软窗关闭时刻
    /// </summary>
    internal class GsLaserRifle : GsChantScheme
    {
        public override int TargetItemID => ItemID.LaserRifle;

        protected override string GsDescFallback =>
            "Reforged: every third on-beat shot splits into twin parallel lasers;" +
            "\nat full resonance the next shot ignites a sweeping overdrive burst, then the barrel wilts briefly";

        protected override float BaseDamageMult => 1.06f;

        //困难入门武器，正拍返蓝压到 20%
        protected override float OnBeatManaRefund => 0.20f;

        protected override Color ChantColor => new(216, 96, 255);

        /// <summary>形态：双联激光</summary>
        private const float FormTwin = 10f;

        /// <summary>过热扫射窗（0.8s）</summary>
        private const int OverdriveTicks = 48;
        /// <summary>疲软窗</summary>
        private const int WiltTicks = 40;

        private static readonly Color CoherentWhite = new(240, 214, 255);

        public override float GsUseSpeedMultiplier(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return 1f;
            }
            GsChantPlayer chant = Chant(player);
            if (chant.BoundItemType != item.type) {
                return 1f;
            }
            uint now = Main.GameUpdateCount;
            if (now < chant.TimerA) {
                return 1.6f;
            }
            if (now < chant.TimerB) {
                return 0.9f;
            }
            return 1f;
        }

        protected override void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //连续正拍计数：正拍累进、平拍归零、强化不动
            if (chant.CurrentBeat == ChantBeat.OnBeat) {
                chant.CounterA++;
            }
            else if (chant.CurrentBeat == ChantBeat.Straight) {
                chant.CounterA = 0;
            }
            //扫射窗内弹道扫摆（发射参数 owner 端权威，掷随机合法）
            if (Main.GameUpdateCount < chant.TimerA) {
                velocity = velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-4f, 4f)));
            }
        }

        protected override bool? ChantShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //三连正拍：第三发双联
            if (chant.CurrentBeat != ChantBeat.OnBeat || chant.CounterA <= 0 || chant.CounterA % 3 != 0) {
                return null;
            }
            Vector2 side = velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * 7f;
            int twinDamage = Math.Max(1, (int)(damage * 0.75f));
            for (int i = 0; i < 2; i++) {
                Vector2 offset = i == 0 ? side : -side;
                QueueForm(player, FormTwin);
                Projectile.NewProjectile(source, position + offset, velocity, type,
                    twinDamage, knockback, player.whoAmI);
            }
            return false;
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //过热扫射：开窗后本发照常出膛（后续射速与扫摆由窗驱动）
            uint now = Main.GameUpdateCount;
            chant.TimerA = now + OverdriveTicks;
            chant.TimerB = now + OverdriveTicks + WiltTicks;
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            bool hot = router.MarkData is FormOnBeat or FormEmpower or FormTwin;
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * (hot ? 0.28f : 0.16f));
            //飞行相：紫曳光
            int interval = hot ? 3 : 6;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(proj.Center + Main.rand.NextVector2Circular(2.5f, 2.5f),
                    -proj.velocity * 0.04f, hot ? ChantColor : ChantColor * 0.7f,
                    Main.rand.NextFloat(0.22f, 0.4f));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //命中相：光爆
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(2.2f, 2.2f),
                    i % 2 == 0 ? ChantColor : CoherentWhite, Main.rand.NextFloat(0.28f, 0.46f));
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, ChantColor, 0.11f)?.Configure(7, 0.65f);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：光屑衰散
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    Main.rand.NextVector2Circular(0.8f, 0.8f), ChantColor * 0.75f,
                    Main.rand.NextFloat(0.2f, 0.34f));
            }
        }
    }
}
