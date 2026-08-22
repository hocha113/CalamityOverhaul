using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.UI
{
    /// <summary>
    /// 落地揭示：加载屏是硬切，直接亮起来会露馅。这里从全黑淡出，中途透一层暗红余烬，
    /// 让"睁眼"这一下有个过渡。<br/>
    /// 真正的入场演出是潮汐本身，雾从涨满退下去把村子还给你（<see cref="Fog.KiyumeFogTide"/>），
    /// 这层只负责接住加载屏最后那一帧
    /// </summary>
    internal class KiyumeEntryReveal : ModSystem
    {
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);
        private const int TotalFrames = 84;

        private static int timer;

        /// <summary>进世界时由 <see cref="KiyumeAmbienceSystem"/> 上膛</summary>
        internal static void Arm() => timer = TotalFrames;

        public override void OnWorldUnload() => timer = 0;

        public override void PostUpdateEverything() {
            if (timer > 0) {
                timer--;
            }
        }

        //PostDrawInterface 时批已开（DrawInterface_33_MouseText 内），直接画不要自开批
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (timer <= 0 || Main.dedServ || Main.gameMenu) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            float t = timer / (float)TotalFrames;
            var full = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            //黑幕：前段压得实，后段快速让开
            spriteBatch.Draw(px, full, PixelSrc, Color.Black * (t * t));
            //黑与亮之间透一层暗红：从梦里睁眼看见的第一色是红的
            float ember = MathF.Sin(MathHelper.Pi * (1f - t));
            spriteBatch.Draw(px, full, PixelSrc, new Color(96, 20, 16) * (ember * 0.34f));
        }
    }
}
