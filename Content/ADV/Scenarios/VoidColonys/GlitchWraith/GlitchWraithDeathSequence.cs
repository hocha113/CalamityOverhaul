using CalamityOverhaul.Content.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith
{
    /// <summary>
    /// 鬼乱码处决演出状态机
    /// 阶段：Mosaic花屏(36帧) → Black黑屏(24帧) → Kill玩家死亡
    /// 演出期间锁定玩家无敌免伤，避免其他伤害源干扰节奏
    /// </summary>
    internal static class GlitchWraithDeathSequence
    {
        private const int MosaicFrames = 36;
        private const int BlackFrames = 24;
        private const int TotalFrames = MosaicFrames + BlackFrames;

        /// <summary>剩余帧数，0表示未激活</summary>
        public static int Timer { get; private set; }
        /// <summary>目标玩家whoAmI</summary>
        private static int targetWhoAmI = -1;
        /// <summary>乱码随机种子每帧滚动</summary>
        private static int glitchSeed;

        public static bool Active => Timer > 0;

        /// <summary>
        /// 由GlitchWraithActor触碰玩家时调用
        /// </summary>
        public static void Trigger(Player player) {
            if (Active) return;
            Timer = TotalFrames;
            targetWhoAmI = player.whoAmI;
            glitchSeed = Main.rand.Next(int.MaxValue);

            //极端工业噪音：重低音低bit扭曲音色拼叠
            SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 1f, Pitch = -1f, PitchVariance = 0.2f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.8f }, player.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.9f, Pitch = -0.95f }, player.Center);

            //锁定玩家，避免被其他伤害源干扰
            player.immune = true;
            player.immuneTime = TotalFrames;
        }

        /// <summary>
        /// 每帧推进
        /// </summary>
        public static void Update() {
            if (!Active) return;
            glitchSeed = unchecked(glitchSeed * 1664525 + 1013904223);

            //锁定玩家每帧刷新免疫，防御其他伤害干扰
            if (targetWhoAmI >= 0 && targetWhoAmI < Main.maxPlayers) {
                Player p = Main.player[targetWhoAmI];
                if (p.active) {
                    p.immune = true;
                    p.immuneTime = Math.Max(p.immuneTime, Timer + 1);
                }
            }

            Timer--;
            if (Timer <= 0) {
                FinishKill();
            }
        }

        /// <summary>
        /// 序列结束，干净利落执行死亡
        /// </summary>
        private static void FinishKill() {
            if (targetWhoAmI >= 0 && targetWhoAmI < Main.maxPlayers) {
                Player p = Main.player[targetWhoAmI];
                if (p.active && !p.dead) {
                    p.immune = false;
                    p.immuneTime = 0;
                    PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(BloodAltarTP.SacrificeDeathReason.ToNetworkText(p.name));
                    p.KillMe(pd, p.statLifeMax2 * 10, 0);
                }
            }
            targetWhoAmI = -1;
        }

        /// <summary>
        /// 全屏覆盖绘制：上段马赛克花屏+色散撕裂，末尾黑屏
        /// </summary>
        public static void Draw(SpriteBatch sb) {
            if (!Active) return;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;

            int elapsed = TotalFrames - Timer;
            if (elapsed < MosaicFrames) {
                //花屏阶段，强度从0.3到1，块尺寸从16缩到6
                float t = elapsed / (float)MosaicFrames;
                int block = (int)MathHelper.Lerp(16, 6, t);
                float coverage = MathHelper.Lerp(0.35f, 1f, t);
                int s = glitchSeed;

                for (int y = 0; y < sh; y += block) {
                    for (int x = 0; x < sw; x += block) {
                        s = unchecked(s * 1103515245 + 12345);
                        //按coverage抽样，部分块保留透明让下层画面隐约可见
                        if (((uint)s & 0xFFFF) / 65535f > coverage) continue;
                        int r = s & 0xFF;
                        int g = (s >> 8) & 0xFF;
                        int b = (s >> 16) & 0xFF;
                        bool purpleFamily = (s & 2) == 0;
                        Color c = purpleFamily
                            ? new Color(180 + (g & 0x3F), 40 + (b & 0x3F), 255)
                            : new Color(255, 40 + (g & 0x3F), 60 + (b & 0x3F));
                        if ((s & 0x3F) == 0) c = new Color(0, 0, 0);
                        sb.Draw(pixel, new Rectangle(x, y, block, block), c);
                    }
                }

                //水平撕裂条，模拟扫描线错位
                for (int i = 0; i < 8; i++) {
                    s = unchecked(s * 1103515245 + 12345);
                    int ty = Math.Abs(s) % sh;
                    int th = 2 + (s & 0xF);
                    sb.Draw(pixel, new Rectangle(0, ty, sw, th), Color.White * 0.6f);
                }
            }
            else {
                //黑屏阶段
                float t = (elapsed - MosaicFrames) / (float)BlackFrames;
                float alpha = MathHelper.Lerp(0.6f, 1f, t);
                sb.Draw(pixel, new Rectangle(0, 0, sw, sh), Color.Black * alpha);
            }
        }
    }

    /// <summary>
    /// 演出系统：每帧推进计时器并在UI层之上覆盖绘制
    /// </summary>
    internal class GlitchWraithDeathSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            GlitchWraithDeathSequence.Update();
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            GlitchWraithDeathSequence.Draw(spriteBatch);
        }
    }
}
