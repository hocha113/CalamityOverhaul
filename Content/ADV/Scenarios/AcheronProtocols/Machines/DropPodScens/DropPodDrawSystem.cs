using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>
    /// 空降仓演出屏幕叠加效果：仅， 空降仓主体、尾焰、灼烧光效等已移至 <see cref="DropPodActor"/> 在世界坐标中绘制
    /// </summary>
    internal class DropPodDrawSystem : ModSystem
    {
        //由DropPodActor同步过来的数据
        private static int dropTimer;
        private static float reentryHeat;

        /// <summary>

        /// 坠入渐黑进度 0~1，由 <see cref="DropPodActor"/> 每帧同步

        /// </summary>
        private static float landingFade;

        /// <summary>

        /// 由 <see cref="DropPodActor"/> 每帧调用，同步坠入渐黑进度

        /// </summary>
        internal static void SyncLandingFade(float fade) {
            landingFade = fade;
        }

        /// <summary>

        /// 由 <see cref="DropPodActor"/> 每帧调用，同步坠落计时和灼烧强度

        /// </summary>
        internal static void SyncDropTimer(int timer, float heat) {
            dropTimer = timer;
            reentryHeat = heat;
        }

        public override void PostUpdateEverything() {
            if (!DropPodWorld.Active) {
                dropTimer = 0;
                reentryHeat = 0f;
                landingFade = 0f;
                DropPodHeatHazeRender.Reset();
            }
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            //坠入环节：屏幕渐黑遮罩
            if (landingFade <= 0f) return;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.Black * landingFade);
        }

        public override void Unload() {
            dropTimer = 0;
            reentryHeat = 0f;
            landingFade = 0f;
        }
    }
}
