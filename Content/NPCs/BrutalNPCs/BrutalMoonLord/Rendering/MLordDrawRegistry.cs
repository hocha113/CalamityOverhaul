using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>
    /// 月总部件绘制登记。原版 396/397/398 均为 hide=true，唯一绘制入口是
    /// Main.CacheNPCDraws 填充的 DrawCacheNPCsMoonMoon——但原版扫描硬编码
    /// "左右各取第一只手 + 全找齐才登记"，四臂的下对永远进不了缓存（Draw 根本不被调用），
    /// 手部生成前核心也会整体隐形。此处在核心的 DrawBehind 拍上剔除原版登记的半套条目，
    /// 按层序全量重排：核心→下对手→上对手→头（下对垫底、头压最前，与原版层序一致）
    /// </summary>
    internal class MLordDrawRegistry : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.MoonLordCore;

        public override void DrawBehind(NPC npc, int index) {
            //仅在重做接管本体时接手登记，原版/其他模组的月总不受影响
            if (!npc.TryGetOverride(out MoonLordCoreAI _)) {
                return;
            }
            List<int> cache = Main.instance.DrawCacheNPCsMoonMoon;
            //剔除原版对本组的登记（可能只含一对手），防重复绘制；不触碰其他组条目
            for (int i = cache.Count - 1; i >= 0; i--) {
                int cached = cache[i];
                if (cached == index) {
                    cache.RemoveAt(i);
                    continue;
                }
                NPC part = Main.npc[cached];
                if (part.active && (part.type == NPCID.MoonLordHand || part.type == NPCID.MoonLordHead)
                    && (int)part.ai[MLordAiSlots.PartCoreIndex] == index) {
                    cache.RemoveAt(i);
                }
            }
            //全量重登记：缓存按加入序绘制，后加者压前
            cache.Add(index);
            MLordPartsStatus parts = MLordFacts.ScanParts(npc);
            Span<int> order = stackalloc int[] { 2, 3, 0, 1 };
            foreach (int slot in order) {
                int hand = parts.HandIndex(slot);
                if (hand >= 0 && Main.npc[hand].active) {
                    cache.Add(hand);
                }
            }
            if (parts.Head >= 0 && Main.npc[parts.Head].active) {
                cache.Add(parts.Head);
            }
        }
    }
}
