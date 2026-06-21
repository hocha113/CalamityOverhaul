using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.CybCourses
{
    //超梦入场演出层：六角网格揭示，时间由 CybCourseWorld 推进
    internal class CybCourseEntryRevealLayer : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            //仅在教程子世界激活、且演出仍在窗口内时注入
            if (!CybCourseWorld.Active) {
                return;
            }
            if (!CybCourseWorld.EntryRevealActive) {
                return;
            }

            //放到最末尾 → 绘制顺序最靠后 → 盖住一切（包括默认鼠标指针）
            //短暂的盖住是有意为之：开场仪式感更强，演出结束自动撤层
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
