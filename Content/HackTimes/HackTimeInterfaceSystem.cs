using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间 UI 层管理</summary>
    internal class HackTimeInterfaceSystem : ModSystem
    {
        //骇客时间激活时隐藏的原版 UI 层
        private static readonly HashSet<string> HiddenLayers = [
            "Vanilla: Hotbar",
            "Vanilla: Resource Bars",
            "Vanilla: Inventory",
            "Vanilla: Info Accessories Bar",
            "Vanilla: Map / Minimap",
            "Vanilla: Diagnose Net",
            "Vanilla: Diagnose Video",
            "Vanilla: Entity Health Bars",
            "Vanilla: Emote Bubbles",
            "Vanilla: Builder Accessories",
            "Vanilla: Radial Hotbars",
        ];

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            //含淡出过程
            if (!HackTime.Active && HackTime.Intensity < 0.5f) return;

            foreach (var layer in layers) {
                if (HiddenLayers.Contains(layer.Name)) {
                    layer.Active = false;
                }
            }
        }
    }
}
