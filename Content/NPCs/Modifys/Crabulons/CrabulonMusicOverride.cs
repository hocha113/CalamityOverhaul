using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 灾厄的菌生蟹 Boss 音乐由 <c>CalamityMod.Systems.CrabulonMusicScene</c> 这个 <see cref="ModSceneEffect"/> 驱动，
    /// 其激活判定只看屏幕内是否存在菌生蟹类型的 NPC，完全不读取 <see cref="NPC.boss"/>。
    /// 所以驯服后即便把 boss 置为 false，Boss 音乐依旧会播放；此处在场上没有“野生”菌生蟹时压制该场景效果。
    /// </summary>
    internal class CrabulonMusicOverride : SceneOverride
    {
        //灾厄菌生蟹音乐场景效果的 Type.FullName，无编译期引用，按名匹配
        private const string CrabulonMusicSceneFullName = "CalamityMod.Systems.CrabulonMusicScene";
        //与灾厄 BaseMusicSceneEffect.MusicDistance 默认值保持一致
        private const int MusicDistance = 5000;

        public override IEnumerable<string> GetActiveSceneEffectFullNames() {
            yield return CrabulonMusicSceneFullName;
        }

        //该钩子会向所有已注册场景效果广播，必须先确认目标确实是菌生蟹音乐再接管
        //返回非空即短路（优先于 PlayerOverride 判定），所以这里总给出明确结果，避免被其它 Override 误压制野生 Boss 音乐
        public override bool? PreIsSceneEffectActive(ModSceneEffect modSceneEffect, Player player) {
            if (modSceneEffect.GetType().FullName != CrabulonMusicSceneFullName) {
                return null;
            }

            //只认野生（未驯服）个体
            Rectangle screenRect = new((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != CWRID.NPC_Crabulon) {
                    continue;
                }
                if (npc.TryGetOverride<ModifyCrabulon>(out var modify) && modify.FeedValue > 0f) {
                    continue;//已驯服，不应触发 Boss 音乐
                }
                Rectangle npcBox = new((int)npc.Center.X - MusicDistance, (int)npc.Center.Y - MusicDistance, MusicDistance * 2, MusicDistance * 2);
                if (screenRect.Intersects(npcBox)) {
                    return true;//屏幕附近存在野生 Boss，照常播放
                }
            }

            return false;//仅有驯服个体或没有个体，压制 Boss 音乐
        }
    }
}
