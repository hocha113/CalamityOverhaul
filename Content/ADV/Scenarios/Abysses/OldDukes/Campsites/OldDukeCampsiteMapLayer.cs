using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    [VaultLoaden(CWRConstant.ADV + "Abysse/")]
    internal class OldDukeCampsiteMapLayer : ModMapLayer
    {
        private static Asset<Texture2D> OldflagpoleIcon = null;
        public override void Draw(ref MapOverlayDrawContext context, ref string text) {
            if (OldflagpoleIcon.IsDisposed) {
                return;//资源未加载不显示
            }
            if (!OldDukeCampsite.IsGenerated) {
                return;//营地未生成不显示
            }
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return;//未获取到存档不显示
            }
            if (!save.OldDukeFirstCampsiteDialogueCompleted) {
                return;//完成首次对话前不显示营地
            }
            var result = context.Draw(OldflagpoleIcon.Value, OldDukeCampsite.CampsitePosition / 16
                , Color.White, new SpriteFrame(1, 1, 0, 0), 0.5f, 0.6f, Alignment.Center);
            if (result.IsMouseOver) {
                text = OldDukeCampsite.TitleText.Value;
            }
        }
    }
}
