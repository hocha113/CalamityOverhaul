using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering
{
    /// <summary>
    /// 玩家被女皇攻击命中的命中链（仅本人客户端）：先压黑再闪、定向震屏、
    /// 两族火花（径向散+沿击向拖）、三层音。强度按来源 0.4~1.5 分级，玩家不看血条也知道被哪招打了
    /// </summary>
    internal static class EmpressHitFeedback
    {
        public static void Play(Vector2 pos, Vector2 dir, float intensity, bool day) {
            if (VaultUtils.isServer) {
                return;
            }
            intensity = MathHelper.Clamp(intensity, 0.3f, 1.6f);
            float dayBlend = day ? 1f : 0f;

            EmpressScreenFX.PushImpactDim(0.35f * Math.Max(intensity - 0.3f, 0f));
            EmpressScreenFX.PushFlash(dir, 0.45f * intensity);
            EmpressMotion.ShakeAlong(pos, dir, 6f + 12f * intensity, 14);

            //径向散：椭圆分布带余弦瓣调制，越重越密
            int radial = (int)(14 * intensity);
            Vector2 lobe = Main.rand.NextVector2Unit();
            float lobeK = Main.rand.NextFloat(1f, 6f);
            for (int i = 0; i < radial; i++) {
                Vector2 v = Main.rand.NextVector2Circular(9f, 5f) * MathF.Sqrt(intensity);
                v *= 1f + 0.6f * MathF.Cos(Vector2.Dot(v.SafeNormalize(Vector2.Zero), lobe) * lobeK);
                float hue = Main.rand.NextFloat();
                PRTLoader.NewParticle<PRT_EmpressSpark>(pos + v * 3f, v * 0.9f, EmpressMotion.FormColor(hue, dayBlend, 0.72f),
                    Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(14, 24), hue, dayBlend);
            }
            //沿击向拖：径向按 rand² 分布，速度带击向偏置
            int along = (int)(20 * intensity);
            for (int i = 0; i < along; i++) {
                Vector2 v = Main.rand.NextVector2Unit() * MathF.Pow(Main.rand.NextFloat(), 2f);
                v = (v + dir * Main.rand.NextFloat()) * 11f * MathF.Sqrt(intensity);
                float hue = Main.rand.NextFloat();
                PRTLoader.NewParticle<PRT_EmpressPetalDust>(pos, v, EmpressMotion.FormColor(hue, dayBlend, 0.66f),
                    Main.rand.NextFloat(0.5f, 1.1f) * Math.Min(intensity, 1.2f))?.Configure(Main.rand.Next(20, 36), hue, dayBlend);
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(pos, Vector2.Zero, Color.White, 0.5f + 0.4f * intensity)?
                .Configure(14, 0.12f, dayBlend);

            //三层音：低频落点、中频击体、高频碎光；重招加一记远处轰鸣
            SoundEngine.PlaySound(SoundID.DD2_FlameburstTowerShot with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 4 }, pos);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.9f * intensity, Pitch = -0.9f, MaxInstances = 4 }, pos);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.7f, Pitch = 0.6f, MaxInstances = 4 }, pos);
            if (intensity >= 1.2f) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.4f, MaxInstances = 2 }, pos);
            }
        }

        /// <summary>灼痕期间二次命中的额外提示：一记重低音+全屏压黑加深</summary>
        public static void RepeatHitCue(Vector2 pos) {
            if (VaultUtils.isServer) {
                return;
            }
            EmpressScreenFX.PushImpactDim(0.35f);
            EmpressScreenFX.PushPrismPulse(pos, 0.5f, 22);
            SoundEngine.PlaySound(SoundID.NPCDeath58 with { Volume = 0.9f, Pitch = -0.6f, MaxInstances = 2 }, pos);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1f, Pitch = -0.7f, MaxInstances = 2 }, pos);
        }
    }
}
