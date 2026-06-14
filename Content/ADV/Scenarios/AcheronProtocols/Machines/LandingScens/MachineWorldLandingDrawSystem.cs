using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.LandingScens
{
    /// <summary>
    /// 机械世界着陆演出：屏幕叠加特效系统 管理着陆时的屏幕闪光、渐入渐出和摄像机震动
    /// </summary>
    internal class MachineWorldLandingDrawSystem : ModSystem
    {
        /// <summary>
        /// 着陆演出是否正在进行
        /// </summary>
        private static bool isActive;

        /// <summary>

        /// 黑屏渐入alpha（玩家刚进入世界时从黑屏过渡）

        /// </summary>
        private static float blackScreenAlpha;

        /// <summary>

        /// 黑屏渐入计时器

        /// </summary>
        private static int fadeInTimer;

        /// <summary>

        /// 黑屏渐入总时长（帧）

        /// </summary>
        private const int FadeInDuration = 60;

        /// <summary>

        /// 撞击白闪alpha

        /// </summary>
        private static float impactWhiteFlash;

        /// <summary>

        /// 屏幕震动强度

        /// </summary>
        private static float screenShakeIntensity;

        /// <summary>

        /// 弹出时的屏幕闪光

        /// </summary>
        private static float ejectFlash;

        internal static void Activate() {
            isActive = true;
            blackScreenAlpha = 1f;
            fadeInTimer = 0;
            impactWhiteFlash = 0.6f;
            screenShakeIntensity = 5f;
            ejectFlash = 0f;
        }

        internal static void TriggerImpactFlash() {
            impactWhiteFlash = 0.5f;
            screenShakeIntensity = 4f;
        }

        internal static void TriggerEjectFlash() {
            ejectFlash = 0.4f;
            screenShakeIntensity = 2f;
        }

        internal static void Deactivate() {
            isActive = false;
            blackScreenAlpha = 0f;
            impactWhiteFlash = 0f;
            screenShakeIntensity = 0f;
            ejectFlash = 0f;
        }

        public override void PostUpdateEverything() {
            if (!MachineWorld.Active || !isActive) {
                if (isActive) {
                    Deactivate();
                }
                return;
            }

            //黑屏渐入
            fadeInTimer++;
            if (fadeInTimer < FadeInDuration) {
                blackScreenAlpha = 1f - (float)fadeInTimer / FadeInDuration;
                blackScreenAlpha *= blackScreenAlpha; //二次衰减，更自然
            }
            else {
                blackScreenAlpha = 0f;
            }

            //闪光衰减
            impactWhiteFlash *= 0.94f;
            if (impactWhiteFlash < 0.01f) impactWhiteFlash = 0f;

            ejectFlash *= 0.92f;
            if (ejectFlash < 0.01f) ejectFlash = 0f;

            //震动衰减
            screenShakeIntensity *= 0.93f;
            if (screenShakeIntensity < 0.1f) screenShakeIntensity = 0f;

            //应用屏幕震动
            if (screenShakeIntensity > 0.1f) {
                Main.screenPosition += new Vector2(
                    Main.rand.NextFloat(-screenShakeIntensity, screenShakeIntensity),
                    Main.rand.NextFloat(-screenShakeIntensity, screenShakeIntensity));
            }
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (!MachineWorld.Active || !isActive) return;

            int sw = Main.screenWidth;
            int sh = Main.screenHeight;

            //黑屏叠加层
            if (blackScreenAlpha > 0.01f) {
                Texture2D pixel = CWRAsset.Placeholder_White.Value;
                Color blackColor = Color.Black * blackScreenAlpha;
                spriteBatch.Draw(pixel, new Rectangle(0, 0, sw, sh), blackColor);
            }

            //撞击白闪
            if (impactWhiteFlash > 0.01f) {
                Texture2D pixel = CWRAsset.Placeholder_White.Value;
                Color flashColor = Color.White * impactWhiteFlash;
                spriteBatch.Draw(pixel, new Rectangle(0, 0, sw, sh), flashColor);
            }

            //弹出闪光
            if (ejectFlash > 0.01f) {
                Texture2D pixel = CWRAsset.Placeholder_White.Value;
                Color ejectColor = new Color(200, 220, 255) * ejectFlash;
                spriteBatch.Draw(pixel, new Rectangle(0, 0, sw, sh), ejectColor);
            }
        }

        public override void Unload() {
            isActive = false;
            blackScreenAlpha = 0f;
            impactWhiteFlash = 0f;
            screenShakeIntensity = 0f;
            ejectFlash = 0f;
        }
    }
}
