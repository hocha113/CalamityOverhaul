using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 拆解：把地上这件东西按配方拆回约六成材料。<br/>
    /// 全套唯一读配方表的协议，表由 <see cref="ItemSalvageRecipeIndex"/> 在 PostAddRecipes 建。<br/>
    /// 世界掉落物归权威端：销毁走 <c>SyncItem</c>，产物走 <c>Item.NewItem</c> 落地，
    /// 全程不碰任何玩家背包
    /// </summary>
    internal class ItemSalvage : QuickHackDef
    {
        /// <summary>
        /// 拆解返还率，写死的经济上限。返还材料再按原版两折卖出，
        /// 相对商店买入价是 0.6×0.2=12% 的双重折损，凑不成买卖正循环
        /// </summary>
        internal const float SalvageRatio = 0.6f;

        private static readonly Color Scrap = new(150, 225, 255);

        //产物清单的复用缓冲，权威端单线程使用
        private static readonly List<(int type, int count)> yieldBuf = [];

        public override void SetDefaults() {
            UploadTime = 130;
            RamCost = 5;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Item;
            UnlockedByDefault = false;
        }

        /// <summary>
        /// 设计稿标 0（即时），这里给 1 帧：追踪器先广播 EffectApply 再跑第 0 帧 OnTick，
        /// 销毁挪进 OnTick 后广播离站时物品还活着，复制端才解析得到目标、
        /// 施术者与旁观者才看得到拆解表现（即时销毁会让远端 TryResolve 直接失败）
        /// </summary>
        public override int GetDuration() => 1;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            //无配方或整张清单折算后为空 → 没得拆
            return HackTargets.TryItem(target, out Item item)
                && ItemSalvageRecipeIndex.TryBuildYield(item, null);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryItem(target, out Item item, out _)) return false;
            if (Main.netMode != NetmodeID.Server) EmitSalvage(item.Center);
            return true;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            //只在权威端跑；第 0 帧结算完就收队
            if (!HackTargets.TryItem(target, out Item item, out int itemIndex)) return false;
            if (!ItemSalvageRecipeIndex.TryBuildYield(item, yieldBuf)) return false;

            Rectangle box = item.Hitbox;
            //先毁后产：原物腾出的槽位允许被产物复用
            item.active = false;
            item.type = ItemID.None;
            item.stack = 0;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, number: itemIndex);
            }

            for (int i = 0; i < yieldBuf.Count; i++) {
                (int type, int count) = yieldBuf[i];
                int maxStack = Math.Max(1, ContentSamples.ItemsByType[type].maxStack);
                while (count > 0) {
                    int chunk = Math.Min(count, maxStack);
                    int idx = Item.NewItem(new EntitySource_WorldEvent(), box, type, chunk);
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, number: idx);
                    }
                    count -= chunk;
                }
            }
            yieldBuf.Clear();
            //返回 false 让效果当帧收尾，远端同帧拿到 EffectRemove
            return false;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryItem(target, out Item item)) {
                EmitSalvage(item.Center);
            }
        }

        //方形碎片朝四周迸开 + 火花上扬，读作被拆成零件
        private static void EmitSalvage(Vector2 center) {
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(0.3f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.6f, 3.4f);
                PRTLoader.NewParticle<PRT_CyberSquare>(center, vel, Scrap,
                    Main.rand.NextFloat(4f, 8f))
                    ?.Configure(Color.Lerp(Scrap, Color.White, 0.4f), 26);
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.4f, 1.4f),
                    Main.rand.NextFloat(-3.2f, -1.2f));
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Scrap, 0.7f)
                    ?.Configure(true, 20);
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f }, center);
        }
    }

    /// <summary>
    /// 拆解协议的配方反查表。<br/>
    /// 设计稿写 PostSetupContent 建表，但那时 <c>Main.recipe</c> 还是空的——
    /// 配方在 AddRecipes 阶段才注册，所以建表挪到 <see cref="PostAddRecipes"/>。<br/>
    /// 存 Recipe 引用而不是数组下标，配方排序整理不影响表的有效性
    /// </summary>
    internal sealed class ItemSalvageRecipeIndex : ModSystem
    {
        //产物类型 → 选中的配方
        private static readonly Dictionary<int, Recipe> recipeByItem = [];

        public override void PostAddRecipes() {
            recipeByItem.Clear();
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];
                if (recipe == null || recipe.Disabled || recipe.createItem.IsAir) {
                    continue;
                }
                int kinds = CountMaterialKinds(recipe);
                if (kinds == 0) continue;

                int type = recipe.createItem.type;
                if (!recipeByItem.TryGetValue(type, out Recipe chosen)) {
                    recipeByItem[type] = recipe;
                    continue;
                }
                //材料种类多者优先（照设计稿）；同种数取材料总值低的那条，
                //不给"挑贵配方拆解套利"留口子
                int chosenKinds = CountMaterialKinds(chosen);
                if (kinds > chosenKinds
                    || kinds == chosenKinds
                        && MaterialValue(recipe) < MaterialValue(chosen)) {
                    recipeByItem[type] = recipe;
                }
            }
        }

        public override void Unload() => recipeByItem.Clear();

        /// <summary>
        /// 算这件掉落物的返还清单；result 传 null 时只判有没有产出。<br/>
        /// 折算 = req.stack × item.stack × 0.6 ÷ createItem.stack 后四舍五入：<br/>
        /// 设计稿的 ceil + 保底 1 在 createItem.stack &gt; 1 的配方上是复制机
        /// （1 木 → 2 平台 → 拆回 1 木×2 = 2 木），四舍五入保证返还永不超过投入；<br/>
        /// 整张清单全部舍成 0 → 返回 false，由 <see cref="ItemSalvage.CanApplyTo(IHackTarget)"/> 拒绝
        /// </summary>
        internal static bool TryBuildYield(Item item, List<(int type, int count)> result) {
            result?.Clear();
            if (item == null || item.IsAir || item.stack <= 0) return false;
            if (!recipeByItem.TryGetValue(item.type, out Recipe recipe)) return false;

            int createStack = Math.Max(1, recipe.createItem.stack);
            bool any = false;
            for (int i = 0; i < recipe.requiredItem.Count; i++) {
                Item req = recipe.requiredItem[i];
                if (req == null || req.IsAir || req.stack <= 0) continue;
                int count = (int)Math.Floor(req.stack * (double)item.stack
                    * ItemSalvage.SalvageRatio / createStack + 0.5);
                if (count <= 0) continue;
                any = true;
                result?.Add((req.type, count));
            }
            return any;
        }

        private static int CountMaterialKinds(Recipe recipe) {
            int kinds = 0;
            for (int i = 0; i < recipe.requiredItem.Count; i++) {
                Item req = recipe.requiredItem[i];
                if (req != null && !req.IsAir && req.stack > 0) kinds++;
            }
            return kinds;
        }

        //单份产物折算的材料总值，只用于同种数配方间的择低
        private static long MaterialValue(Recipe recipe) {
            long total = 0;
            int createStack = Math.Max(1, recipe.createItem.stack);
            for (int i = 0; i < recipe.requiredItem.Count; i++) {
                Item req = recipe.requiredItem[i];
                if (req == null || req.IsAir || req.stack <= 0) continue;
                total += (long)req.value * req.stack;
            }
            return total / createStack;
        }
    }
}
