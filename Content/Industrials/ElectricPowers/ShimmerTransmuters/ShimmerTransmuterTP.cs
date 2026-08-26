using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShimmerTransmuters
{
    /// <summary>
    /// 微光转化引擎:复刻原版 <see cref="Item.GetShimmered"/> 的物品到物品分支,分类顺序与其内联判定一致。
    /// 数据一律读官方源(<see cref="ItemID.Sets.ShimmerTransformToItem"/>、<see cref="ShimmerTransforms"/>、
    /// <see cref="RecipeLoader.ConsumeIngredient"/>),模组注册的转化与去合成定制自动生效。
    /// 币运与生成NPC两类不产出物品,机器拒收
    /// </summary>
    internal static class ShimmerTransmuteEngine
    {
        /// <summary>转化路径类别,按原版 GetShimmered 分支顺序取首个命中</summary>
        internal enum PathKind
        {
            None,
            /// <summary>币运(硬币入微光),机器不做</summary>
            CoinLuck,
            /// <summary>原版内联特判对(混沌杖/环境改造枪/无底桶/月亮砖/音乐盒)</summary>
            Special,
            /// <summary>直转表 ShimmerTransformToItem</summary>
            Direct,
            /// <summary>生成NPC(小动物/凝胶气球),机器不做</summary>
            MakeNPC,
            /// <summary>去合成 decraft</summary>
            Decraft,
        }

        /// <summary>微光等价类型(镜像原版私有 GetShimmerEquivalentType)</summary>
        internal static int GetEquivalentType(Item item) {
            int alt = ItemID.Sets.ShimmerCountsAsItem[item.type];
            return alt != -1 ? alt : item.type;
        }

        /// <summary>按原版 GetShimmered 的分支顺序分类,首个命中即返回</summary>
        internal static PathKind Classify(Item item) {
            if (item == null || item.IsAir) {
                return PathKind.None;
            }

            int eq = GetEquivalentType(item);
            if (ShimmerTransforms.IsItemTransformLocked(eq)) {
                return PathKind.None;
            }
            if (ItemID.Sets.CoinLuckValue[eq] > 0) {
                return PathKind.CoinLuck;
            }
            //原版内联特判对:前四组有月总门,月亮砖与音乐盒无门
            if ((eq == ItemID.RodofDiscord || eq == ItemID.Clentaminator
                || eq == ItemID.BottomlessBucket || eq == ItemID.BottomlessShimmerBucket) && NPC.downedMoonlord) {
                return PathKind.Special;
            }
            //空音乐盒(576)自身 createTile 也是 139,原版靠掉落物 shimmered 标记防重复,
            //机器槽位没有该标记,自转只会白烧微光,故排除
            if (eq == ItemID.LunarBrick
                || (item.createTile == TileID.MusicBoxes && item.type != ItemID.MusicBox)) {
                return PathKind.Special;
            }
            if (ItemID.Sets.ShimmerTransformToItem[eq] > 0) {
                return PathKind.Direct;
            }
            if (item.type == ItemID.GelBalloon || item.makeNPC > 0) {
                return PathKind.MakeNPC;
            }
            if (ShimmerTransforms.GetDecraftingRecipeIndex(eq) >= 0) {
                return PathKind.Decraft;
            }
            return PathKind.None;
        }

        /// <summary>机器可否处理:只收产出物品的三类(特判对/直转/去合成)</summary>
        internal static bool CanMachineProcess(Item item) {
            PathKind kind = Classify(item);
            return kind is PathKind.Special or PathKind.Direct or PathKind.Decraft;
        }

        /// <summary>月亮砖按当前月相映射(镜像原版 switch)</summary>
        private static int MoonPhaseBrick() => Main.GetMoonPhase() switch {
            MoonPhase.QuarterAtRight => ItemID.StarRoyaleBrick,
            MoonPhase.HalfAtRight => ItemID.CryocoreBrick,
            MoonPhase.ThreeQuartersAtRight => ItemID.CosmicEmberBrick,
            MoonPhase.Full => ItemID.HeavenforgeBrick,
            MoonPhase.ThreeQuartersAtLeft => ItemID.LunarRustBrick,
            MoonPhase.HalfAtLeft => ItemID.AstraBrick,
            MoonPhase.QuarterAtLeft => ItemID.DarkCelestialBrick,
            _ => ItemID.MercuryBrick,
        };

        /// <summary>
        /// 解析一次转化:填 results(产物)与 inputCost(消耗的输入件数)。
        /// 纯内存运算无副作用,两端可跑(模拟与结算共用)。
        /// 去合成产物可能被模组钩子清零,此时照常消耗输入,与原版行为一致
        /// </summary>
        internal static bool TryResolve(Item input, List<Item> results, out int inputCost) {
            results.Clear();
            inputCost = 0;

            switch (Classify(input)) {
                case PathKind.Special: {
                    int eq = GetEquivalentType(input);
                    int target;
                    if (eq == ItemID.RodofDiscord) {
                        target = ItemID.RodOfHarmony;
                    }
                    else if (eq == ItemID.Clentaminator) {
                        target = ItemID.Clentaminator2;
                    }
                    else if (eq == ItemID.BottomlessBucket) {
                        target = ItemID.BottomlessShimmerBucket;
                    }
                    else if (eq == ItemID.BottomlessShimmerBucket) {
                        target = ItemID.BottomlessBucket;
                    }
                    else if (eq == ItemID.LunarBrick) {
                        target = MoonPhaseBrick();
                    }
                    else if (input.createTile == TileID.MusicBoxes) {
                        target = ItemID.MusicBox;
                    }
                    else {
                        return false;
                    }
                    inputCost = 1;
                    results.Add(new Item(target));
                    return true;
                }
                case PathKind.Direct: {
                    inputCost = 1;
                    results.Add(new Item(ItemID.Sets.ShimmerTransformToItem[GetEquivalentType(input)]));
                    return true;
                }
                case PathKind.Decraft: {
                    int idx = ShimmerTransforms.GetDecraftingRecipeIndex(GetEquivalentType(input));
                    if (idx < 0) {
                        return false;
                    }
                    Recipe recipe = Main.recipe[idx];
                    inputCost = recipe.createItem.stack;
                    if (input.stack < inputCost) {
                        return false;
                    }
                    //产物源:定制微光结果优先,否则配方原料;遇空行即断,与原版一致
                    IEnumerable<Item> source = recipe.customShimmerResults ?? (IEnumerable<Item>)recipe.requiredItem;
                    foreach (Item ing in source) {
                        if (ing.type <= 0) {
                            break;
                        }
                        int amount = ing.stack;
                        RecipeLoader.ConsumeIngredient(recipe, ing.type, ref amount, isDecrafting: true);
                        if (amount > 0) {
                            results.Add(new Item(ing.type, amount));
                        }
                    }
                    return true;
                }
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// 微光转化槽TP:输入槽物品按原版微光规则转化,产物入四个输出槽。
    /// 耗微光液(液体网络供给,只收微光)与电,作业仅权威端结算;
    /// 客户端镜像推进进度,到点停格等服务器的结算包
    /// </summary>
    internal class ShimmerTransmuterTP : BaseBattery, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<ShimmerTransmuterTile>();
        public override int TargetItem => ModContent.ItemType<ShimmerTransmuter>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;

        #region 液体容器契约:只收微光
        public int FluidType { get; set; } = LiquidID.Shimmer;
        public int FluidAmount { get; set; }
        public int FluidCapacity => 4 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Consumer;
        public bool CanAcceptFluid(int liquidId)
            => liquidId == LiquidID.Shimmer && FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>单次转化电费</summary>
        internal const float JobCostUE = 10f;
        /// <summary>单次转化微光耗量(64 单位约四分之一格)</summary>
        internal const int ShimmerPerJob = 64;
        /// <summary>作业节拍(帧)</summary>
        internal const int BeatTicks = 120;
        /// <summary>输出槽数</summary>
        internal const int OutputSlotCount = 4;

        /// <summary>待转化输入槽</summary>
        internal Item InputItem = new Item();
        /// <summary>产物输出槽</summary>
        internal Item[] OutputItems = new Item[OutputSlotCount];

        /// <summary>作业进度(帧),两端镜像推进,权威端到点结算</summary>
        internal int Progress;

        /// <summary>结算复用的产物缓冲</summary>
        private readonly List<Item> resolveBuffer = [];

        /// <summary>本帧工况(喂给物块发光与UI状态灯)</summary>
        internal bool IsWorking => Progress > 0;

        #region 纯客户端表现状态(转化辉光/星尘/完成爆点)
        private float animTime;
        /// <summary>转化辉光强度,随镜像进度抬升</summary>
        private float auraVis;
        /// <summary>完成闪光,指数退潮</summary>
        private float flashT;
        private int lastOutputTotal = -1;
        private int sparkleTimer;
        #endregion

        public override void SetBattery() {
            for (int i = 0; i < OutputSlotCount; i++) {
                OutputItems[i] ??= new Item();
            }
        }

        public override void UpdateMachine() {
            if (!Main.dedServ) {
                UpdateTransmuteVisual();
            }

            if (!CanRunJob()) {
                Progress = 0;
                return;
            }

            if (Progress < BeatTicks) {
                Progress++;
            }
            if (Progress < BeatTicks) {
                return;
            }

            //物品结算是权威端专属:客户端停格在满进度等服务器的结算包,
            //本地结算再推送会用漂移状态覆盖服务器的真实槽位
            if (VaultUtils.isClient) {
                return;
            }

            CompleteJob();
        }

        /// <summary>作业条件:有可转物、电够、微光够、产物能全部入槽(UI 也用它判阻塞原因)</summary>
        internal bool CanRunJob() {
            if (InputItem == null || InputItem.IsAir) {
                return false;
            }
            if (MachineData.UEvalue < JobCostUE || FluidAmount < ShimmerPerJob) {
                return false;
            }
            if (!ShimmerTransmuteEngine.TryResolve(InputItem, resolveBuffer, out _)) {
                return false;
            }
            return TryPlaceOutputs(resolveBuffer, dryRun: true);
        }

        /// <summary>权威端结算一次转化</summary>
        private void CompleteJob() {
            if (!ShimmerTransmuteEngine.TryResolve(InputItem, resolveBuffer, out int inputCost)) {
                Progress = 0;
                return;
            }
            if (!TryPlaceOutputs(resolveBuffer, dryRun: true)) {
                Progress = 0;
                return;
            }

            InputItem.stack -= inputCost;
            if (InputItem.stack <= 0) {
                InputItem.TurnToAir();
            }
            FluidAmount -= ShimmerPerJob;
            if (FluidAmount < 0) {
                FluidAmount = 0;
            }
            MachineData.UEvalue -= JobCostUE;
            TryPlaceOutputs(resolveBuffer, dryRun: false);
            Progress = 0;

            //微光转化特效走原版通道:单机直接播,服务器广播 ShimmerActions 包让各客户端播
            Vector2 center = CenterInWorld;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 0, (int)center.X, (int)center.Y);
            }
            else {
                Defer(() => Item.ShimmerEffect(center));
            }

            SendData();
        }

        /// <summary>
        /// 产物入输出槽:同类型堆叠优先,再占空槽,按 maxStack 分堆。
        /// dryRun 只模拟不落账,模拟与落账共用一套遍历避免逻辑漂移
        /// </summary>
        private bool TryPlaceOutputs(List<Item> results, bool dryRun) {
            //镜像当前槽位状态(type/stack),模拟装载
            Span<int> types = stackalloc int[OutputSlotCount];
            Span<int> stacks = stackalloc int[OutputSlotCount];
            for (int i = 0; i < OutputSlotCount; i++) {
                Item slot = OutputItems[i];
                bool empty = slot == null || slot.IsAir;
                types[i] = empty ? 0 : slot.type;
                stacks[i] = empty ? 0 : slot.stack;
            }

            foreach (Item result in results) {
                int remain = result.stack;
                int cap = result.maxStack;
                //先并入同类型未满槽
                for (int i = 0; i < OutputSlotCount && remain > 0; i++) {
                    if (types[i] != result.type || stacks[i] >= cap) {
                        continue;
                    }
                    int add = System.Math.Min(cap - stacks[i], remain);
                    stacks[i] += add;
                    remain -= add;
                }
                //再占空槽
                for (int i = 0; i < OutputSlotCount && remain > 0; i++) {
                    if (types[i] != 0) {
                        continue;
                    }
                    int add = System.Math.Min(cap, remain);
                    types[i] = result.type;
                    stacks[i] = add;
                    remain -= add;
                }
                if (remain > 0) {
                    return false;
                }
            }

            if (!dryRun) {
                for (int i = 0; i < OutputSlotCount; i++) {
                    if (types[i] == 0) {
                        OutputItems[i] = new Item();
                    }
                    else if (OutputItems[i] == null || OutputItems[i].IsAir || OutputItems[i].type != types[i]) {
                        OutputItems[i] = new Item(types[i], stacks[i]);
                    }
                    else {
                        OutputItems[i].stack = stacks[i];
                    }
                }
            }
            return true;
        }

        #region 表现推进(纯客户端,零网络)
        /// <summary>输出四槽的总件数,变化=一次转化结算到达</summary>
        private int OutputTotal() {
            int total = 0;
            for (int i = 0; i < OutputSlotCount; i++) {
                Item slot = OutputItems[i];
                if (slot != null && !slot.IsAir) {
                    total += slot.stack;
                }
            }
            return total;
        }

        /// <summary>
        /// 转化辉光挂在两端镜像的 <see cref="Progress"/> 上(真实进度),
        /// 完成爆点由输出槽变化(结算事件包)触发,与原版 ShimmerEffect 同拍
        /// </summary>
        private void UpdateTransmuteVisual() {
            animTime += 1f / 60f;
            float progressT = MathHelper.Clamp(Progress / (float)BeatTicks, 0f, 1f);
            auraVis = MathHelper.Lerp(auraVis, progressT, 0.15f);
            flashT *= 0.9f;

            bool near = FluidVFX.NearLocalPlayer(CenterInWorld);
            FluidStyle style = FluidVFX.GetStyle(LiquidID.Shimmer);

            //作业中:星尘自槽体上浮,越接近完成越密
            if (IsWorking && near) {
                int interval = 20 - (int)(progressT * 13f);
                if (++sparkleTimer >= interval) {
                    sparkleTimer = 0;
                    Vector2 spawn = new(PosInWorld.X + Main.rand.NextFloat(6f, Width - 6f), PosInWorld.Y + Height - 8f);
                    Color tint = Color.Lerp(style.Main, style.Bright, Main.rand.NextFloat());
                    Defer(() => {
                        PRTLoader.NewParticle<PRT_Sparkle>(spawn, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.5f, 1.2f)),
                            tint, Main.rand.NextFloat(0.2f, 0.32f))?.Configure(tint * 0.8f, Main.rand.Next(26, 40), 0.04f, 0.9f);
                    });
                }
            }

            //完成:输出总件数上升=结算到达(原版 ShimmerEffect 由结算端广播,这里补机器自己的爆点)
            int total = OutputTotal();
            if (lastOutputTotal < 0) {
                lastOutputTotal = total;
            }
            bool completed = total > lastOutputTotal;
            lastOutputTotal = total;
            if (!completed) {
                return;
            }
            flashT = 1f;
            if (!near) {
                return;
            }
            Vector2 center = CenterInWorld;
            Defer(() => {
                PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(center, Vector2.Zero, style.Bright * 0.8f, 1f)
                    ?.Configure(0.06f, 0.24f, 24);
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0f, 0.8f);
                    Color tint = Color.Lerp(style.Main, style.Bright, Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Sparkle>(center + Main.rand.NextVector2Circular(8f, 8f), vel,
                        tint, Main.rand.NextFloat(0.24f, 0.4f))?.Configure(tint * 0.85f, 30, 0.06f, 1f);
                }
            });
        }
        #endregion

        #region 机面覆层:槽体微光液窗(物块下)+悬浮物影+虹彩辉光+状态灯(物块上)
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            //微光液窗:3x3 机体沿用 3x4 占位贴图的上三行,窗区按 48px 高折算
            Vector2 basePos = PosInWorld - Main.screenPosition;
            Rectangle chamber = new(
                (int)(basePos.X + 0.21f * Width), (int)(basePos.Y + 0.27f * Height),
                (int)(0.50f * Width), (int)(0.62f * Height));
            FluidVFX.DrawLiquidWindow(spriteBatch, chamber, LiquidID.Shimmer,
                MathHelper.Clamp(FluidAmount / (float)FluidCapacity, 0f, 1f), animTime,
                auraVis * 0.8f, WhoAmI + 13);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 basePos = PosInWorld - Main.screenPosition;
            FluidStyle style = FluidVFX.GetStyle(LiquidID.Shimmer);
            bool hasInput = InputItem != null && !InputItem.IsAir;

            //状态灯:作业=虹彩呼吸,有输入但阻塞(缺电/缺微光/输出满)=琥珀慢闪,空闲=熄灭
            Color lamp;
            if (Disabled || (!IsWorking && !hasInput)) {
                lamp = new Color(30, 26, 24);
            }
            else if (IsWorking) {
                //虹彩:色相随时间游移,呼应原版 shimmer 视觉语言
                float hue = (0.72f + 0.08f * MathF.Sin(animTime * 2.1f)) % 1f;
                Color iri = Main.hslToRgb(hue, 0.8f, 0.68f);
                lamp = FluidVFX.Glow(iri, 0.6f + 0.4f * MathF.Sin(animTime * 4.2f));
            }
            else {
                lamp = FluidVFX.Glow(new Color(255, 170, 50), 0.4f + 0.25f * MathF.Sin(animTime * 2.2f));
            }
            spriteBatch.Draw(px, new Rectangle((int)(basePos.X + Width) - 7, (int)basePos.Y + 5, 3, 3), lamp);

            if (!hasInput) {
                return;
            }

            //悬浮物影:待转物悬在槽口上方,作业中被虹彩包裹并轻浮
            Main.instance.LoadItem(InputItem.type);
            Texture2D itemTex = TextureAssets.Item[InputItem.type].Value;
            float fit = MathF.Min(18f / itemTex.Width, 18f / itemTex.Height);
            float bob = IsWorking ? MathF.Sin(animTime * 2.6f) * 2.2f : 0f;
            Vector2 ghostPos = new(basePos.X + Width / 2f, basePos.Y - 10f + bob);
            Vector2 origin = itemTex.Size() * 0.5f;

            //虹彩辉光垫底(随进度增强)
            if (auraVis > 0.03f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float hue = (0.78f + 0.10f * MathF.Sin(animTime * 1.7f)) % 1f;
                Color iri = Main.hslToRgb(hue, 0.85f, 0.7f);
                spriteBatch.Draw(glow, ghostPos, null, FluidVFX.Glow(iri, 0.55f * auraVis),
                    0f, glow.Size() * 0.5f, 0.55f + 0.1f * MathF.Sin(animTime * 3.3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, ghostPos, null, FluidVFX.Glow(style.Bright, 0.35f * auraVis),
                    0f, glow.Size() * 0.5f, 0.3f, SpriteEffects.None, 0f);
            }

            //物影:作业中带一点微光染色,阻塞时压暗
            Color ghostTint = IsWorking
                ? Color.Lerp(Color.White, style.Main, 0.25f + 0.2f * auraVis)
                : new Color(120, 115, 130);
            spriteBatch.Draw(itemTex, ghostPos, null, ghostTint * (IsWorking ? 0.95f : 0.7f),
                0f, origin, fit, SpriteEffects.None, 0f);

            //完成闪光
            if (flashT > 0.03f) {
                spriteBatch.Draw(itemTex, ghostPos, null, FluidVFX.Glow(Color.White, flashT * 0.9f),
                    0f, origin, fit, SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 交互
        /// <summary>右键交互(交互客户端执行):Shift 全取;手持可转物快放;否则开UI</summary>
        public void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                //Shift 全部取出,直接入背包(MP 下地面掉落会被队友截走)
                bool took = false;
                if (InputItem != null && !InputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), InputItem.Clone());
                    InputItem.TurnToAir();
                    took = true;
                }
                for (int i = 0; i < OutputSlotCount; i++) {
                    if (OutputItems[i] != null && !OutputItems[i].IsAir) {
                        Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), OutputItems[i].Clone());
                        OutputItems[i].TurnToAir();
                        took = true;
                    }
                }
                if (took) {
                    SendData();
                    SoundEngine.PlaySound(SoundID.Grab);
                }
                return;
            }

            //手持可转物:放入输入槽(空槽/同型堆叠)
            if (ShimmerTransmuteEngine.CanMachineProcess(item)) {
                if (InputItem == null || InputItem.IsAir) {
                    InputItem = item.Clone();
                    item.TurnToAir();
                }
                else if (InputItem.type == item.type) {
                    int space = InputItem.maxStack - InputItem.stack;
                    int transfer = System.Math.Min(space, item.stack);
                    InputItem.stack += transfer;
                    item.stack -= transfer;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                    }
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //开UI
            var ui = UIHandleLoader.GetUIHandleOfType<ShimmerTransmuterUI>();
            ui?.Interactive(this, newTP);
        }

        /// <summary>UI 输入槽交互:可转物放入/堆叠/交换,空手取出</summary>
        internal void HandleInputItem() {
            Item mouseItem = Main.mouseItem;

            if (ShimmerTransmuteEngine.CanMachineProcess(mouseItem)) {
                if (InputItem == null || InputItem.IsAir) {
                    InputItem = mouseItem.Clone();
                    mouseItem.TurnToAir();
                }
                else if (InputItem.type == mouseItem.type) {
                    int space = InputItem.maxStack - InputItem.stack;
                    int transfer = System.Math.Min(space, mouseItem.stack);
                    InputItem.stack += transfer;
                    mouseItem.stack -= transfer;
                    if (mouseItem.stack <= 0) {
                        mouseItem.TurnToAir();
                    }
                }
                else {
                    Item temp = InputItem.Clone();
                    InputItem = mouseItem.Clone();
                    Main.mouseItem = temp;
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
                return;
            }

            if (mouseItem.IsAir && InputItem != null && !InputItem.IsAir) {
                Main.mouseItem = InputItem.Clone();
                InputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        /// <summary>UI 输出槽交互:空手取出,同型并入手中</summary>
        internal void HandleOutputItem(int slot) {
            if (slot < 0 || slot >= OutputSlotCount) {
                return;
            }
            Item output = OutputItems[slot];
            if (output == null || output.IsAir) {
                return;
            }

            Item mouseItem = Main.mouseItem;
            if (mouseItem.IsAir) {
                Main.mouseItem = output.Clone();
                output.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
            else if (mouseItem.type == output.type) {
                int space = mouseItem.maxStack - mouseItem.stack;
                int transfer = System.Math.Min(space, output.stack);
                mouseItem.stack += transfer;
                output.stack -= transfer;
                if (output.stack <= 0) {
                    output.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }
        #endregion

        public override void MachineKill() {
            //槽内物品随拆机倒出(权威端)
            if (!VaultUtils.isClient) {
                if (InputItem != null && !InputItem.IsAir) {
                    DropItem(InputItem.Clone());
                }
                for (int i = 0; i < OutputSlotCount; i++) {
                    if (OutputItems[i] != null && !OutputItems[i].IsAir) {
                        DropItem(OutputItems[i].Clone());
                    }
                }
            }
            InputItem?.TurnToAir();
            for (int i = 0; i < OutputSlotCount; i++) {
                OutputItems[i]?.TurnToAir();
            }

            var ui = UIHandleLoader.GetUIHandleOfType<ShimmerTransmuterUI>();
            if (ui != null && ui.CurrentTP == this) {
                ui.IsActive = false;
            }
        }

        #region 存档与同步:液体/槽位/进度追加在基类之后
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write((byte)FluidType);
            data.Write(FluidAmount);
            data.Write(Progress);
            ItemIO.Send(InputItem ?? new Item(), data, true, true);
            for (int i = 0; i < OutputSlotCount; i++) {
                ItemIO.Send(OutputItems[i] ?? new Item(), data, true, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            FluidType = reader.ReadByte();
            FluidAmount = reader.ReadInt32();
            Progress = reader.ReadInt32();
            InputItem = ItemIO.Receive(reader, true, true);
            for (int i = 0; i < OutputSlotCount; i++) {
                OutputItems[i] = ItemIO.Receive(reader, true, true);
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["FluidType"] = FluidType;
            tag["FluidAmount"] = FluidAmount;
            if (InputItem != null && !InputItem.IsAir) {
                tag["Transmuter_InputItem"] = ItemIO.Save(InputItem);
            }
            for (int i = 0; i < OutputSlotCount; i++) {
                if (OutputItems[i] != null && !OutputItems[i].IsAir) {
                    tag[$"Transmuter_OutputItem{i}"] = ItemIO.Save(OutputItems[i]);
                }
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Shimmer;
            FluidAmount = tag.TryGet("FluidAmount", out int amount) ? amount : 0;
            InputItem = CWRSaveData.LoadItemFromTag(tag, "Transmuter_InputItem", nameof(ShimmerTransmuterTP));
            for (int i = 0; i < OutputSlotCount; i++) {
                OutputItems[i] = CWRSaveData.LoadItemFromTag(tag, $"Transmuter_OutputItem{i}", nameof(ShimmerTransmuterTP));
            }
        }
        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
            if (HoverTP) {
                FluidHelper.DrawFluidBar(this, this);
            }
        }
    }
}
