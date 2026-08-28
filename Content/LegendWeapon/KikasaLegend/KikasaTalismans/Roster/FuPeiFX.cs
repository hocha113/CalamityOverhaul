using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>沛的演出与伴生弹幕集中处：盆满溢沿、开闸喷发，全部各端本地</summary>
    internal static class FuPeiFX
    {
        /// <summary>盆满边沿：碗沿荡一圈溢珠+满溢环+一记沉水鸣，读作「满了，可以倒了」</summary>
        internal static void BowlFullCue(Projectile umbrella, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(KikasaInk.InkSplash, umbrella.Center, 0.5f, -0.65f, 3);
            for (int i = 0; i < 12; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 12f + 0.26f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    umbrella.Center + new Vector2(dir.X * 30f, -6f + dir.Y * 4f),
                    new Vector2(dir.X * Main.rand.NextFloat(1.4f, 2.4f), -Main.rand.NextFloat(0.8f, 1.6f)),
                    Main.rand.NextBool(3) ? KikasaInk.InkDeep : accent,
                    Main.rand.NextFloat(0.2f, 0.3f))?.Configure(Main.rand.Next(16, 26));
            }
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(umbrella.Center - Vector2.UnitY * 4f,
                Vector2.Zero, accent * 0.5f, 0.06f)?.Configure(0.06f, 0.42f, 12);
        }

        /// <summary>满蓄持续溢沿：碗沿两侧渗珠外滚，低频常态泵——盆一直是满的</summary>
        internal static void BowlBrim(Projectile umbrella, Color accent) {
            if (Main.dedServ || !Main.rand.NextBool(3)) {
                return;
            }
            float side = Main.rand.NextBool() ? 1f : -1f;
            float rim = Main.rand.NextFloat(24f, 32f);
            PRTLoader.NewParticle<PRT_KikasaInkBead>(
                umbrella.Center + new Vector2(side * rim, -4f),
                new Vector2(side * Main.rand.NextFloat(0.5f, 1.3f), -Main.rand.NextFloat(0.2f, 0.7f)),
                Main.rand.NextBool(4) ? accent : KikasaInk.InkBody,
                Main.rand.NextFloat(0.16f, 0.26f))?.Configure(Main.rand.Next(18, 28));
        }

        /// <summary>开闸拍：瀑源白沫喷环+顺倾向急沫线与碎珠+一口墨雾+近距短震屏，各端本地</summary>
        internal static void GateOpenBurst(Projectile pour, Color accent) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = pour.ai[0].ToRotationVector2();
            KikasaInk.Play(KikasaInk.InkSpray, pour.Center, 0.6f, -0.5f, 2);
            KikasaInk.Play(KikasaInk.InkSplash, pour.Center, 0.7f, -0.7f, 3);
            //喷环：源头一圈白沫脉冲
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(pour.Center, Vector2.Zero,
                Color.Lerp(accent, Color.White, 0.5f) * 0.55f, 0.08f)?.Configure(0.08f, 0.8f, 14);
            //顺流急沫：沿倾向甩白线与碎珠
            for (int i = 0; i < 6; i++) {
                Vector2 vel = dir.RotatedByRandom(0.42f) * Main.rand.NextFloat(7f, 13f);
                PRTLoader.NewParticle<PRT_Line>(pour.Center + dir * Main.rand.NextFloat(6f, 26f),
                    vel, Color.Lerp(accent, Color.White, 0.55f) * 0.7f,
                    Main.rand.NextFloat(0.45f, 0.7f))?.Configure(false, Main.rand.Next(8, 13));
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(3f, 7f)
                    - Vector2.UnitY * Main.rand.NextFloat(0f, 1.5f);
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    pour.Center + Main.rand.NextVector2Circular(8f, 8f),
                    vel, Main.rand.NextBool(3) ? KikasaInk.InkDeep : accent,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(Main.rand.Next(16, 26));
            }
            PRTLoader.NewParticle<PRT_KikasaInkMist>(pour.Center + dir * 10f, dir * 1.2f,
                KikasaInk.InkDeep, Main.rand.NextFloat(0.9f, 1.2f))?.Configure(Main.rand.Next(24, 34));
            //近距短震：开闸是一记重闸落水
            if (Vector2.Distance(Main.LocalPlayer.Center, pour.Center) < 900f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(2f);
            }
        }
    }

    /// <summary>
    /// 沛·开闸拍：满蓄开泼瞬间瀑源的一瞬 AoE 判定（ai[0]=判定半径 px、ai[1]=倾向弧度），
    /// 击退顺倾向压去；演出走 OnPourStart 各端派发，本体不自绘，伤害随生成包自含
    /// </summary>
    internal class FuPeiGateBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>判定半径（px），生成包 ai[0]</summary>
        private ref float Radius => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                float radius = MathHelper.Clamp(Radius <= 0f ? 90f : Radius, 40f, 150f);
                Projectile.Resize((int)(radius * 2f), (int)(radius * 2f));
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //开闸的水往倾向冲：击退顺流压向
            float dirX = MathF.Cos(Projectile.ai[1]);
            if (MathF.Abs(dirX) > 0.05f) {
                modifiers.HitDirectionOverride = dirX >= 0f ? 1 : -1;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
