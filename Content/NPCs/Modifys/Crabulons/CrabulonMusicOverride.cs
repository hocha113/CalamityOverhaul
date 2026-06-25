using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 灾厄的菌生蟹 Boss 音乐由 <c>CalamityMod.Systems.CrabulonMusicScene</c>(<see cref="ModSceneEffect"/>)驱动，
    /// 它的激活判定只看屏幕附近是否存在菌生蟹类型 NPC，完全不读取 <see cref="NPC.boss"/>，
    /// 所以驯服后即便把 boss 置为 false 音乐照旧。此处在场上没有“野生”菌生蟹时压制它。
    /// <para/>
    /// 安全约束（务必保留现有判定形态）：tML 的 <c>SceneEffectLoader.UpdateSceneEffect</c> 跑在 <c>Player.Update</c> 尾部，
    /// 会按权重依次读取“活跃”场景效果的 <c>.Music</c> 直到音乐位被填满。某些音乐 Mod 的 <c>.Music</c> 可能抛异常
    /// （典型：UnCalamityModMusic.InfernumCompatibility.DecideOnMusicPath，在装了 InfernumModeMusic 且其 Call 返回 null 时
    /// 会 (bool)null 抛 NRE），一旦被读到就会中断 Player.Update，表现为“游戏不崩但角色卡死动不了”。
    /// 因此本类：①只接管菌生蟹这一个场景效果，绝不波及别的（可能抛错的）场景效果；
    /// ②野生时严格沿用灾厄原判定返回 true，让本场景效果自己占住音乐位，tML 就不会再去读后续场景效果的 .Music。
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

        public override bool? PreIsSceneEffectActive(ModSceneEffect modSceneEffect, Player player) {
            //该钩子会广播给所有已注册场景效果：必须先确认目标确实是菌生蟹音乐，
            //否则会误改别的 Mod 的场景效果激活态，进而让它的 .Music 被提前读取并可能抛错卡死
            if (modSceneEffect.GetType().FullName != CrabulonMusicSceneFullName) {
                return null;
            }

            //复刻灾厄“屏幕附近是否存在菌生蟹”的判定，但只认野生（未驯服）个体
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
                    //野生 Boss 在屏幕附近：返回 true（与灾厄原判定完全一致），
                    //让本场景效果占住音乐位，tML 不会继续读其它场景效果的 .Music——等于零行为改动、零卡死风险
                    return true;
                }
            }

            //仅剩驯服个体或没有个体：放行回退到普通/生态音乐（生态场景效果 .Music 走 MusicFilePath，安全不抛错）
            return false;
        }
    }
}
