using InnoVault.GameSystem;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>
    /// 致谢 ED 播放时接管主菜单 BGM（<see cref="AcknowledgmentUI"/> 打开期间）
    /// </summary>
    internal class AcknowledgmentMusicOverride : SceneOverride
    {
        public override void DecideMusic() {
            if (!Main.gameMenu || !AcknowledgmentUI.OnActive()) {
                return;
            }
            AcknowledgmentUI ui = AcknowledgmentUI.InstanceOrNull;
            if (ui == null) {
                return;
            }
            int targetID = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/ED_WEH");
            for (int i = 0; i < Main.musicFade.Length; i++) {
                if (i == targetID) {
                    continue;
                }
                Main.musicFade[i] = ui.MusicFade50 / 120f;
            }
            Main.newMusic = targetID;
        }
    }
}
