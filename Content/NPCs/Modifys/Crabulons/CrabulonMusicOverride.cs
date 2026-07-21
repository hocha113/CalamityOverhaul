using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 驯服后压制灾厄 CrabulonMusicScene(只认 type 不认 boss)；仅接管该场景
    /// 野生照原判定返回 true 占位，防后续场景 .Music 抛 NRE 卡死 Player.Update
    /// </summary>
    internal class CrabulonMusicOverride : SceneOverride
    {
        //灾厄场景 FullName，无编译期引用
        private const string CrabulonMusicSceneFullName = "CalamityMod.Systems.CrabulonMusicScene";
        //对齐 BaseMusicSceneEffect.MusicDistance 默认
        private const int MusicDistance = 5000;

        public override IEnumerable<string> GetActiveSceneEffectFullNames() {
            yield return CrabulonMusicSceneFullName;
        }

        public override bool? PreIsSceneEffectActive(ModSceneEffect modSceneEffect, Player player) {
            //钩子广播全场景，非目标必须 return null
            if (modSceneEffect.GetType().FullName != CrabulonMusicSceneFullName) {
                return null;
            }

            //复刻屏幕附近判定，只认野生
            Rectangle screenRect = new((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != CWRID.NPC_Crabulon) {
                    continue;
                }
                if (npc.TryGetOverride<ModifyCrabulon>(out var modify) && modify.FeedValue > 0f) {
                    continue;//已驯服
                }
                Rectangle npcBox = new((int)npc.Center.X - MusicDistance, (int)npc.Center.Y - MusicDistance, MusicDistance * 2, MusicDistance * 2);
                if (screenRect.Intersects(npcBox)) {
                    //true 占位，不让后续 .Music 被读到
                    return true;
                }
            }

            //无野生，放行生态音乐
            return false;
        }
    }
}
