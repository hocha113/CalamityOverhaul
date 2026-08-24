using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>澍的演出集中处：急救窗触发爆点、窗内伞面金泵与金滴速度线，全部端本地纯表现</summary>
    internal static class FuShuFX
    {
        /// <summary>找到持雨人当前的悬伞（无伞返回 null，演出退化到人身上）</summary>
        private static Projectile FindUmbrella(Player owner) {
            int type = ModContent.ProjectileType<KikasaRainUmbrella>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && proj.owner == owner.whoAmI) {
                    return proj;
                }
            }
            return null;
        }

        /// <summary>
        /// 急救窗触发：伞面金边涟漪+金珠环甩+身周水环+清音。
        /// OnOwnerHurt 各持雨端调用（旁观端缺此挂钩时无碍，演出主场在受击者本机）
        /// </summary>
        internal static void RescueBurst(Player owner, Color accent) {
            if (Main.dedServ) {
                return;
            }
            Projectile umbrella = FindUmbrella(owner);
            Vector2 anchor = umbrella?.Center ?? owner.Top;

            //伞面金边涟漪：两圈错拍
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(anchor, Vector2.Zero,
                accent * 0.65f, 0.1f)?.Configure(0.1f, 0.9f, 14);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(anchor, Vector2.Zero,
                Color.Lerp(accent, Color.White, 0.4f) * 0.45f, 0.06f)?.Configure(0.06f, 0.6f, 10);

            //金珠自伞缘整圈崩出
            for (int i = 0; i < 8; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 8f + 0.2f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_KikasaInkBead>(anchor + dir * 10f,
                    dir * Main.rand.NextFloat(2f, 4f) - Vector2.UnitY * 1.2f,
                    Color.Lerp(accent, Color.White, 0.35f), Main.rand.NextFloat(0.26f, 0.4f))
                    ?.Configure(Main.rand.Next(14, 22));
            }

            //身周水环：及时雨落到了人身上
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(owner.Center, Vector2.Zero,
                accent * 0.4f, 0.07f)?.Configure(0.07f, 0.5f, 12);

            //清音：金铃一记+水花垫底
            KikasaInk.Play(SoundID.Item4, anchor, 0.32f, 0.25f, 2);
            KikasaInk.Play(KikasaInk.InkSplash, anchor, 0.4f, -0.1f, 3);
        }

        /// <summary>窗内伞面金泵：伞沿渗金滴+偶发金闪，UpdateWhileHeld 各端逐帧调用</summary>
        internal static void WindowPump(Player owner, Color accent) {
            Projectile umbrella = FindUmbrella(owner);
            if (umbrella == null) {
                return;
            }
            //伞缘渗金：泡透的伞在这三秒里滴的是金水
            if (Main.rand.NextBool(6)) {
                float xOff = Main.rand.NextFloat(-1f, 1f) * 30f;
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                    umbrella.Center + new Vector2(xOff, 6f), Vector2.Zero,
                    accent * 0.9f, Main.rand.NextFloat(0.5f, 0.75f))?.Configure(Main.rand.Next(20, 32));
            }
            //伞面金闪：窗还开着的读数
            if (Main.rand.NextBool(14)) {
                PRTLoader.NewParticle<PRT_Light>(
                    umbrella.Center + Main.rand.NextVector2Circular(24f, 8f),
                    -Vector2.UnitY * 0.3f, Color.Lerp(accent, Color.White, 0.4f) * 0.6f,
                    Main.rand.NextFloat(0.12f, 0.2f))?.Configure(Main.rand.Next(12, 20), 0.7f);
            }
        }

        /// <summary>澍标坠滴的金色速度线：只在急坠段拖线，逐帧短线叠成金线</summary>
        internal static void GoldDropLine(Projectile drop, Color accent) {
            if (drop.velocity.Y < 8f) {
                return;
            }
            PRTLoader.NewParticle<PRT_Line>(
                drop.Center - drop.velocity * 0.6f + Main.rand.NextVector2Circular(4f, 2f),
                drop.velocity * 0.2f,
                Color.Lerp(accent, Color.White, 0.45f) * 0.75f,
                Main.rand.NextFloat(0.38f, 0.58f))?.Configure(false, 9);
        }
    }
}
