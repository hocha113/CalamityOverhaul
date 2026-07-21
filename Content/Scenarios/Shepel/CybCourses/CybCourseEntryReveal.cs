using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //入场六角揭示层，时间由CybCourseWorld推进
    internal class CybCourseEntryRevealLayer : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!CybCourseWorld.Active) {
                return;
            }
            if (!CybCourseWorld.EntryRevealActive) {
                return;
            }

            //末层盖住常规UI，演出结束撤
            layers.Add(new LegacyGameInterfaceLayer(
                "CWRMod: CybCourse Entry Reveal",
                delegate {
                    CybCourseWorld.DrawEntryRevealOverlay(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}
