using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Prismglade
{
    /// <summary>
    /// 神圣之地氛围的逐玩家状态：被「棱光审判」命中后的强辉光余韵（纯本机视觉）。
    /// 命中判定在受害端本机结算，故只有被打中的玩家自己看到这层过曝辉光
    /// </summary>
    internal class PrismgladePlayer : ModPlayer
    {
        private const int JudgedGlowFrames = 38;

        /// <summary>强辉光余韵计时，命中帧置满后逐帧衰减</summary>
        internal int judgedGlow;

        /// <summary>命中拍：置满辉光计时并炸开一圈光尘（仅本机调用）</summary>
        internal void TriggerJudgedGlow() {
            judgedGlow = JudgedGlowFrames;
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                float ang = MathHelper.TwoPi * i / 7f + Main.rand.NextFloat(0.3f);
                var mote = PRTLoader.NewParticle<PRT_PrismgladeMote>(Player.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(2.2f, 4.5f),
                    default, Main.rand.NextFloat(0.26f, 0.4f));
                if (mote != null) {
                    mote.hue = i / 7f;
                    mote.Lifetime = Main.rand.Next(30, 50);
                }
            }
        }

        public override void PostUpdateMiscEffects() {
            if (judgedGlow <= 0 || Main.dedServ) {
                return;
            }
            judgedGlow--;
            float t = judgedGlow / (float)JudgedGlowFrames;
            //强辉光：白光裹身，前段过曝后段速降
            Lighting.AddLight(Player.Center, new Vector3(1.7f, 1.62f, 1.8f) * (0.5f + 1.7f * t * t));
            if (judgedGlow % 4 == 0) {
                var mote = PRTLoader.NewParticle<PRT_PrismgladeMote>(
                    Player.Center + Main.rand.NextVector2Circular(16f, 22f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)),
                    default, Main.rand.NextFloat(0.18f, 0.28f));
                if (mote != null) {
                    mote.Lifetime = Main.rand.Next(26, 40);
                }
            }
        }
    }
}
