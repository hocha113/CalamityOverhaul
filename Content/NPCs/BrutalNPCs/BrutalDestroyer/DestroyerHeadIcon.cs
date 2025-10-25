using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerHeadIcon : CWRNPCOverride, ICWRLoader
    {
        public override int TargetID => -1;//设置为-1，也就是说会运行在所有NPC之上，这里可以让GetBossHeadRotation等钩子可以在type为0的NPC上运行

        /// <summary>
        /// 存储当前参与地图绘制时需要修改类型的毁灭者头部NPC索引
        /// 使用HashSet保证唯一性和快速查找
        /// </summary>
        internal static HashSet<int> HeadWhoAmIs { get; private set; } = [];

        /// <summary>
        /// 备份原始NPC类型，用于在绘制完成后恢复
        /// 键为NPC索引，值为原始type
        /// </summary>
        private static readonly Dictionary<int, int> OriginalNPCTypes = [];

        void ICWRLoader.LoadData() => On_Main.DrawMap += On_Main_DrawMap;

        void ICWRLoader.UnLoadData() {
            On_Main.DrawMap -= On_Main_DrawMap;
            //清理静态数据，防止内存泄漏
            HeadWhoAmIs?.Clear();
            OriginalNPCTypes?.Clear();
        }

        /// <summary>
        /// 该死的瑞德你他妈告诉我为什么要给毁灭者在这坨几千行函数里写个特判
        /// 这个方法通过临时修改NPC类型来绕过原版的特殊处理逻辑
        /// </summary>
        private static void On_Main_DrawMap(On_Main.orig_DrawMap orig, Main self, GameTime gameTime) {
            //提前检查是否需要进行特殊处理
            if (!ShouldProcessDestroyerHeads()) {
                orig.Invoke(self, gameTime);
                return;
            }

            //准备阶段：收集需要处理的NPC并备份原始类型
            PrepareDestroyerHeads();

            try {
                //执行原版绘制逻辑，此时毁灭者的type已经被临时改为None
                orig.Invoke(self, gameTime);//<---DestroyerHeadIcon的钩子会在这里运行，所以运行顺序不会出问题
            } finally {
                //恢复阶段：无论是否发生异常都要恢复NPC类型
                //使用finally块确保即使发生异常也能正确恢复
                RestoreDestroyerHeads();
            }
        }

        /// <summary>
        /// 检查是否需要处理毁灭者头部的特殊逻辑
        /// </summary>
        private static bool ShouldProcessDestroyerHeads() {
            //如果不存在任何毁灭者，或者重组被禁用，则不需要处理
            return CWRPlayer.TheDestroyer != -1 && !HeadPrimeAI.DontReform();
        }

        /// <summary>
        /// 准备阶段：收集所有需要处理的毁灭者头部NPC并临时修改其类型
        /// </summary>
        private static void PrepareDestroyerHeads() {
            HeadWhoAmIs.Clear();
            OriginalNPCTypes.Clear();

            foreach (var npc in Main.ActiveNPCs) {
                //只处理活跃的Boss级别的毁灭者头部
                if (!npc.boss || npc.type != NPCID.TheDestroyer) {
                    continue;
                }

                //记录这个NPC的索引和原始类型
                HeadWhoAmIs.Add(npc.whoAmI);
                OriginalNPCTypes[npc.whoAmI] = npc.type;

                //改成0来避开任何可能的ID特判检查
                //幸运的是这个函数里，改动ID这种事情并不危险
                npc.type = NPCID.None;
            }
        }

        /// <summary>
        /// 恢复阶段：将所有被修改的NPC类型恢复为原始值
        /// </summary>
        private static void RestoreDestroyerHeads() {
            foreach (var whoAmI in HeadWhoAmIs.ToHashSet()) {
                //安全检查：确保NPC索引有效
                if (!whoAmI.TryGetNPC(out var npc)) {
                    continue;
                }

                //尝试从备份中恢复原始类型
                if (OriginalNPCTypes.TryGetValue(whoAmI, out int originalType)) {
                    npc.type = originalType;//恢复，不然这个NPC就变成幽灵状态了
                }
                else {
                    //如果找不到备份，使用默认值
                    npc.type = NPCID.TheDestroyer;
                }
            }

            //清理本次操作的临时数据
            OriginalNPCTypes.Clear();
        }

        /// <summary>
        /// 清理并验证HeadWhoAmIs集合，移除无效的NPC索引
        /// 这个方法在每帧更新后调用，确保数据的有效性
        /// </summary>
        public static void ClearHeadWhoAmIs() {
            if (HeadWhoAmIs.Count == 0) {
                return;
            }

            HashSet<int> validIndices = [];

            foreach (var index in HeadWhoAmIs.ToHashSet()) {
                //验证NPC是否仍然有效且为Boss级别的毁灭者
                if (index.TryGetNPC(out var npc) && npc.boss && npc.type == NPCID.TheDestroyer) {
                    validIndices.Add(index);
                }
            }

            //只在集合发生变化时才更新，避免不必要的内存分配
            if (validIndices.Count != HeadWhoAmIs.Count) {
                HeadWhoAmIs = validIndices;
            }
        }

        public override float? GetBossHeadRotation() {
            //因为是非常规修改，npc的ID被临时修改为0，所以原版的 GetBossHeadRotation 不会运行，这里进行补充修改
            if (HeadWhoAmIs.Contains(npc.whoAmI)) {
                return npc.rotation + MathHelper.Pi;
            }
            return null;
        }

        public override int GetBossHeadTextureIndex() {
            //因为是非常规修改，npc的ID被临时修改为0，所以原版的 GetBossHeadTextureIndex 不会运行，这里进行补充修改
            if (HeadWhoAmIs.Contains(npc.whoAmI)) {
                return DestroyerHeadAI.iconIndex;
            }
            return -1;
        }
    }

    internal class DestroyerHeadIconSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            //在每帧更新后清理无效的头部NPC索引
            DestroyerHeadIcon.ClearHeadWhoAmIs();
        }
    }
}
