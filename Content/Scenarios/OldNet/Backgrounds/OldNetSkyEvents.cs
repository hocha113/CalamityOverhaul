using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds
{
    /// <summary>
    /// 旧网天幕事件状态板与导演（纯视觉，client-only）：
    /// <see cref="Surge"/> 黑墙涌动脉冲（OldNetSky/Blackwall 消费），
    /// <see cref="GiantPos"/>/<see cref="GiantScale"/>/<see cref="GiantMix"/>
    /// 巨物剪影（OldNetSky uGiant 消费）。
    /// 全部状态本机确定性推进，不落存档不发包，事件是氛围不是玩法
    /// </summary>
    internal class OldNetSkyEvents : ModSystem
    {
        /// <summary>黑墙涌动 0~1</summary>
        internal static float Surge { get; private set; }
        /// <summary>巨物剪影中心（屏幕uv）</summary>
        internal static Vector2 GiantPos { get; private set; }
        /// <summary>巨物剪影尺度（uv 半径量级）</summary>
        internal static float GiantScale { get; private set; } = 0.2f;
        /// <summary>巨物在场强度 0~1</summary>
        internal static float GiantMix { get; private set; }
        /// <summary>疯域故障脉冲 0~1（OldNetGrade 消费；衰减区带门控）</summary>
        internal static float Glitch { get; private set; }

        //───── 涌动：低频随机起搏，一次 8~14s 包络 ─────
        private static int surgeTimer;
        private static int surgeDuration;
        private static int surgeCooldown;

        //───── 巨物：极低频（每次深潜 0~2 次量级），横穿远幕 ─────
        private static int giantTimer;
        private static int giantDuration;
        private static int giantCooldown;
        private static float giantStartX;
        private static float giantEndX;
        private static float giantY;

        //───── 疯域故障：衰减区限定，≥60s 随机节律的短促尖峰 ─────
        private static int glitchTimer;
        private static int glitchDuration;
        private static int glitchCooldown;
        private static float glitchAmp;

        public override void ClearWorld() => ResetAll();

        /// <summary>验收辅助：立刻起一段涌动（TestItem 触发用）</summary>
        internal static void DebugTriggerSurge() {
            surgeDuration = 60 * 10;
            surgeTimer = surgeDuration;
            surgeCooldown = 60 * 90;
        }

        /// <summary>验收辅助：立刻起一次疯域故障尖峰（TestItem 触发用，无视带门控）</summary>
        internal static void DebugTriggerGlitch() {
            glitchDuration = 16;
            glitchTimer = glitchDuration;
            glitchAmp = 0.9f;
            glitchCooldown = 60 * 60;
        }

        /// <summary>验收辅助：立刻放一次巨物横穿（TestItem 触发用）</summary>
        internal static void DebugTriggerGiant() {
            giantDuration = 60 * 45;
            giantTimer = giantDuration;
            giantCooldown = 60 * 150;
            giantStartX = -0.35f;
            giantEndX = 1.35f;
            giantY = 0.24f;
            GiantScale = 0.26f;
        }

        internal static void ResetAll() {
            Surge = 0f;
            GiantMix = 0f;
            Glitch = 0f;
            surgeTimer = surgeDuration = 0;
            giantTimer = giantDuration = 0;
            glitchTimer = glitchDuration = 0;
            //进世界后先各给一段静默期：氛围事件不抢开场
            surgeCooldown = 60 * 90;
            giantCooldown = 60 * 150;
            glitchCooldown = 60 * 60;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            if (!OldNetWorld.Active) {
                if (Surge > 0f || GiantMix > 0f) {
                    ResetAll();
                }
                return;
            }
            UpdateSurge();
            UpdateGiant();
            UpdateGlitch();
        }

        private static void UpdateGlitch() {
            if (glitchTimer > 0) {
                glitchTimer--;
                //前 30% 快攻至峰值，其后指数式退潮
                float t = 1f - glitchTimer / (float)glitchDuration;
                float env = t < 0.3f ? t / 0.3f : (1f - t) / 0.7f;
                Glitch = env * glitchAmp;
                return;
            }
            Glitch = 0f;
            if (--glitchCooldown > 0) {
                return;
            }
            //疯域限定：腐化 <0.5（衰减区之外）只重掷冷却不发作
            float corrupt = OldNetMetrics.CorruptionAt((int)(Main.LocalPlayer.Center.X / 16f));
            glitchCooldown = 60 * Main.rand.Next(60, 150);
            if (corrupt < 0.5f) {
                return;
            }
            //越深尖峰越烈：8~18 帧短促脉冲
            glitchDuration = Main.rand.Next(8, 19);
            glitchTimer = glitchDuration;
            glitchAmp = 0.35f + (corrupt - 0.5f) * 1.1f + Main.rand.NextFloat(0.15f);
        }

        private static void UpdateSurge() {
            if (surgeTimer > 0) {
                surgeTimer--;
                //起 20% 快攻，中段驻留，末 40% 缓退
                float t = 1f - surgeTimer / (float)surgeDuration;
                float env = MathHelper.Clamp(t / 0.2f, 0f, 1f)
                    * MathHelper.Clamp((1f - t) / 0.4f, 0f, 1f);
                Surge = MathHelper.Lerp(Surge, env, 0.2f);
                return;
            }
            Surge = MathHelper.Lerp(Surge, 0f, 0.05f);
            if (--surgeCooldown > 0) {
                return;
            }
            //越靠近墙越常涌：离墙远时冷却重掷更长
            surgeDuration = 60 * Main.rand.Next(8, 15);
            surgeTimer = surgeDuration;
            surgeCooldown = 60 * Main.rand.Next(70, 160);
            //起涌远响：低哑的门户开启声当作墙体呼气
            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.DD2_EtherianPortalOpen
                with { Volume = 0.35f, Pitch = -0.55f });
        }

        private static void UpdateGiant() {
            if (giantTimer > 0) {
                giantTimer--;
                float t = 1f - giantTimer / (float)giantDuration;
                //在场包络：两端各 18% 淡入淡出
                float env = MathHelper.Clamp(t / 0.18f, 0f, 1f)
                    * MathHelper.Clamp((1f - t) / 0.18f, 0f, 1f);
                GiantMix = env;
                GiantPos = new Vector2(MathHelper.Lerp(giantStartX, giantEndX, t), giantY);
                return;
            }
            GiantMix = MathHelper.Lerp(GiantMix, 0f, 0.1f);
            if (--giantCooldown > 0) {
                return;
            }
            //腐化越深巨物越可能现身；墙脚带不出（敬畏属于深处）
            float corrupt = OldNetMetrics.CorruptionAt(Main.LocalPlayer.Center.ToTileCoordinates().X);
            giantCooldown = 60 * Main.rand.Next(120, 260);
            if (corrupt < 0.1f || !Main.rand.NextBool(2)) {
                return;
            }
            //一次横穿：60~100s 极慢掠过远幕，天越腐越大
            giantDuration = 60 * Main.rand.Next(60, 100);
            giantTimer = giantDuration;
            //入场低鸣：蠕虫掘进声压低当作远处的位移轰鸣
            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.WormDigQuiet
                with { Volume = 0.55f, Pitch = -0.85f });
            bool leftToRight = Main.rand.NextBool();
            giantStartX = leftToRight ? -0.35f : 1.35f;
            giantEndX = leftToRight ? 1.35f : -0.35f;
            giantY = Main.rand.NextFloat(0.16f, 0.34f);
            GiantScale = 0.16f + corrupt * 0.14f + Main.rand.NextFloat(0.05f);
            CWRMod.Instance.Logger.Info($"[OldNet] 巨物远景事件 dur={giantDuration / 60}s scale={GiantScale:0.00}");
        }
    }
}
