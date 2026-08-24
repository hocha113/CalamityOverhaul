using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>雹的演出集中处：齐掷重音、巨雹旋坠拖尾与碎裂爆点，全部端本地纯表现</summary>
    internal static class FuBaoFX
    {
        /// <summary>冰屑亮白</summary>
        private static readonly Color IceCore = new(238, 248, 255);

        /// <summary>齐掷重音：伞面凝霜一沉——霜环+冰珠甩落+沉重音，OnVolley 各端同拍</summary>
        internal static void VolleyAccent(Projectile umbrella, Color accent) {
            if (Main.dedServ) {
                return;
            }
            //凝霜环：比常规出手拍更沉的一记
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(umbrella.Center, Vector2.Zero,
                accent * 0.5f, 0.09f)?.Configure(0.09f, 0.7f, 12);
            for (int i = 0; i < 5; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 5f + 0.4f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_KikasaInkBead>(umbrella.Center + dir * 12f,
                    dir * Main.rand.NextFloat(1.8f, 3.4f) - Vector2.UnitY * 1f,
                    Color.Lerp(accent, IceCore, 0.4f), Main.rand.NextFloat(0.26f, 0.4f))
                    ?.Configure(Main.rand.Next(14, 22));
            }
            //冰壳互击的闷响垫在湿掌甩墨之下
            KikasaInk.Play(SoundID.Item50, umbrella.Center, 0.42f, -0.5f, 3);
            if (Vector2.Distance(Main.LocalPlayer.Center, umbrella.Center) < 900f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(1.4f);
            }
        }

        /// <summary>
        /// 巨雹旋坠：绕体公转的冰棱闪点+沿途抖落的霜屑。
        /// UpdateWhileHeld 逐帧调用（各端本地），旋相以 identity 错开
        /// </summary>
        internal static void HailSpin(Projectile drop, Color accent) {
            //绕体冰棱闪：公转相位确定性推进，读作雹体在旋
            if ((int)Main.GameUpdateCount % 2 == 0) {
                float ang = Main.GlobalTimeWrappedHourly * 9f + drop.identity * 1.7f;
                Vector2 orbit = drop.Center + ang.ToRotationVector2() * (11f * drop.scale);
                PRTLoader.NewParticle<PRT_Sparkle>(orbit, drop.velocity * 0.2f,
                    Color.Lerp(accent, IceCore, 0.5f) * 0.8f, Main.rand.NextFloat(0.3f, 0.45f))
                    ?.Configure(accent * 0.5f, Main.rand.Next(6, 10), 0.3f, 0.7f);
            }
            //急坠段抖落霜屑
            if (drop.velocity.Y > 8f && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(
                    drop.Center - drop.velocity * 0.5f + Main.rand.NextVector2Circular(5f, 3f),
                    drop.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    accent * 0.6f, Main.rand.NextFloat(0.1f, 0.16f))
                    ?.Configure(Main.rand.Next(10, 16), 0.6f);
            }
        }

        /// <summary>巨雹碎裂：冰屑放射+脆响+重音+近距小震，OnDropKill 各端派发</summary>
        internal static void HailShatter(Projectile drop, Color accent) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -drop.velocity.SafeNormalize(Vector2.UnitY);
            //放射冰棱：贴法线快、侧向慢
            for (int i = 0; i < 6; i++) {
                float spread = Main.rand.NextFloat(-1.1f, 1.1f);
                Vector2 vel = normal.RotatedBy(spread)
                    * Main.rand.NextFloat(2.5f, 7f) * (1f - MathF.Abs(spread) * 0.4f);
                PRTLoader.NewParticle<PRT_Line>(drop.Center + Main.rand.NextVector2Circular(6f, 6f),
                    vel, Color.Lerp(accent, IceCore, Main.rand.NextFloat(0.3f, 0.8f)) * 0.85f,
                    Main.rand.NextFloat(0.4f, 0.6f))?.Configure(true, Main.rand.Next(12, 20));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(drop.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(3f, 3f), IceCore * 0.8f,
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(false, Main.rand.Next(8, 14));
            }
            //碎裂霜环
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(drop.Center, Vector2.Zero,
                accent * 0.55f, 0.07f)?.Configure(0.07f, 0.55f, 11);

            //脆响+闷底：冰裂在上、重量在下
            KikasaInk.Play(SoundID.Item27, drop.Center, 0.5f, -0.3f, 4);
            KikasaInk.Play(SoundID.Item14, drop.Center, 0.16f, -0.7f, 2);
            if (Vector2.Distance(Main.LocalPlayer.Center, drop.Center) < 760f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(1.6f);
            }
        }
    }
}
