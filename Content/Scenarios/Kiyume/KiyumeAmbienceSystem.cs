using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume
{
    /// <summary>
    /// 鬼梦场景包络与环境光：全模块共用的 <see cref="Presence"/> 淡入淡出，
    /// 以及把原版日光整体改写成血暮的两个光照钩子。<br/>
    /// 色值与鬼伞鬼梦相位同源（KikasaDomainSystem 的 dream 分支），纯客户端表现
    /// </summary>
    internal class KiyumeAmbienceSystem : ModSystem
    {
        //梦色板：物块暗红余温，背景压向黑红——地形从红空里剥成剪影
        internal static Color DreamTile = new(150, 52, 44);
        internal static Color DreamBackground = new(64, 12, 14);
        /// <summary>物块/背景染色强度（1=完全接管原版日光）</summary>
        internal static float TileTintStrength = 0.92f;
        internal static float BackgroundTintStrength = 0.95f;
        /// <summary>整体压沉幅度：窗火与雾要当主要亮源，环境不能太亮</summary>
        internal static float Dim = 0.30f;

        private static float presence;

        /// <summary>0~1 场景在场包络；天幕、雾、氛围粒子共用一条</summary>
        internal static float Presence => presence;

        public override void OnWorldLoad() {
            presence = 0f;
            if (!Main.dedServ && KiyumeWorld.Active) {
                UI.KiyumeEntryReveal.Arm();
            }
        }

        public override void OnWorldUnload() => presence = 0f;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            bool want = KiyumeWorld.Active;
            presence = MathHelper.Lerp(presence, want ? 1f : 0f, want ? 0.06f : 0.12f);
            if (!want && presence < 0.004f) {
                presence = 0f;
                return;
            }
            KiyumeAmbienceFX.Update();
        }

        public override void ModifyLightingBrightness(ref float scale) {
            if (presence > 0.001f) {
                scale *= 1f - Dim * presence;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (presence <= 0.001f) {
                return;
            }
            //时间冻在夜里，原版底色近黑；这里几乎整条接管，梦里没有别的光源解释
            tileColor = Color.Lerp(tileColor, DreamTile, TileTintStrength * presence);
            backgroundColor = Color.Lerp(backgroundColor, DreamBackground, BackgroundTintStrength * presence);
        }
    }
}
