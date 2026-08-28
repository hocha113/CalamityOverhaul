using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.BottlingMachines
{
    /// <summary>瓶装机转换表:装瓶(空容器+储液→成品)与倒空(满容器→储液+空容器)</summary>
    internal static class BottlingRecipes
    {
        /// <summary>装瓶行:该液型每件消耗 Units 单位,产出 ResultType</summary>
        internal readonly record struct FillRecipe(int LiquidType, int Units, int ResultType);
        /// <summary>倒空行:每件回收 Units 单位该液型,返还 ReturnType</summary>
        internal readonly record struct DrainRecipe(int LiquidType, int Units, int ReturnType);

        /// <summary>空容器可装的液型候选;微光无对应物品,按设计不可瓶装</summary>
        internal static readonly Dictionary<int, FillRecipe[]> FillTable = new() {
            [ItemID.Bottle] = [
                new FillRecipe(LiquidID.Water, 25, ItemID.BottledWater),
                new FillRecipe(LiquidID.Honey, 25, ItemID.BottledHoney),
            ],
            [ItemID.EmptyBucket] = [
                new FillRecipe(LiquidID.Water, FluidHelper.UnitsPerTile, ItemID.WaterBucket),
                new FillRecipe(LiquidID.Lava, FluidHelper.UnitsPerTile, ItemID.LavaBucket),
                new FillRecipe(LiquidID.Honey, FluidHelper.UnitsPerTile, ItemID.HoneyBucket),
            ],
        };

        internal static readonly Dictionary<int, DrainRecipe> DrainTable = new() {
            [ItemID.BottledWater] = new DrainRecipe(LiquidID.Water, 25, ItemID.Bottle),
            [ItemID.BottledHoney] = new DrainRecipe(LiquidID.Honey, 25, ItemID.Bottle),
            [ItemID.WaterBucket] = new DrainRecipe(LiquidID.Water, FluidHelper.UnitsPerTile, ItemID.EmptyBucket),
            [ItemID.LavaBucket] = new DrainRecipe(LiquidID.Lava, FluidHelper.UnitsPerTile, ItemID.EmptyBucket),
            [ItemID.HoneyBucket] = new DrainRecipe(LiquidID.Honey, FluidHelper.UnitsPerTile, ItemID.EmptyBucket),
        };

        internal static bool CanProcess(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            return FillTable.ContainsKey(item.type) || DrainTable.ContainsKey(item.type);
        }
    }

    /// <summary>
    /// 瓶装机TP:输入槽放空瓶/空桶时抽储液装满,放整瓶/整桶时倒空进储液,成品入输出槽。
    /// 作业只在权威端推进并结算,槽位变化以事件包推给客户端;
    /// 输入输出槽经 StorageProvider 对接物品管道
    /// </summary>
    internal class BottlingMachineTP : BaseBattery, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<BottlingMachineTile>();
        public override int TargetItem => ModContent.ItemType<BottlingMachine>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;

        #region 液体容器契约
        public int FluidType { get; set; }
        public int FluidAmount { get; set; }
        public int FluidCapacity => 4 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Consumer;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>单次作业电费</summary>
        internal const float JobCostUE = 5f;
        /// <summary>作业节拍(帧)</summary>
        internal const int BeatTicks = 60;

        /// <summary>待处理容器槽</summary>
        internal Item InputItem = new Item();
        /// <summary>成品槽</summary>
        internal Item OutputItem = new Item();

        private int jobTimer;

        #region 纯客户端表现状态(灌装动画/完成闪光/状态灯)
        /// <summary>灌装进度 0..1,条件齐备时按节拍推进,结算事件到达即闪光归零</summary>
        private float fillT;
        /// <summary>完成闪光 0..1,指数退潮</summary>
        private float flashT;
        /// <summary>本轮是倒空(液面下降)还是装瓶(液面上升)</summary>
        private bool visualIsDrain;
        /// <summary>工作可行(镜像结算前置条件),喂状态灯与软管脉动</summary>
        private bool visualWorking;
        private bool outputSnapshotInited;
        private int lastOutputType;
        private int lastOutputStack;
        private float animTime;
        #endregion

        public override void UpdateMachine() {
            if (!Main.dedServ) {
                UpdateBottlingVisual();
            }

            //作业仅权威端推进,客户端槽位与液量等事件包
            if (VaultUtils.isClient) {
                return;
            }
            if (++jobTimer < BeatTicks) {
                return;
            }
            jobTimer = 0;

            if (MachineData.UEvalue < JobCostUE || InputItem == null || InputItem.IsAir) {
                return;
            }

            if (BottlingRecipes.DrainTable.TryGetValue(InputItem.type, out var drain)) {
                TryDrainJob(drain);
            }
            else if (BottlingRecipes.FillTable.TryGetValue(InputItem.type, out var fills)) {
                TryFillJob(fills);
            }
        }

        /// <summary>输出槽可否再收一件该物品</summary>
        private bool OutputCanTake(int itemType) {
            if (OutputItem == null || OutputItem.IsAir) {
                return true;
            }
            return OutputItem.type == itemType && OutputItem.stack < OutputItem.maxStack;
        }

        private void PushOutput(int itemType) {
            if (OutputItem == null || OutputItem.IsAir) {
                OutputItem = new Item(itemType);
            }
            else {
                OutputItem.stack++;
            }
        }

        private void ConsumeOneInput() {
            InputItem.stack--;
            if (InputItem.stack <= 0) {
                InputItem.TurnToAir();
            }
        }

        /// <summary>倒空:满容器的液体回收进储液,返还空容器</summary>
        private void TryDrainJob(BottlingRecipes.DrainRecipe drain) {
            if (!CanAcceptFluid(drain.LiquidType)) {
                return;
            }
            if (FluidCapacity - FluidAmount < drain.Units) {
                return;
            }
            if (!OutputCanTake(drain.ReturnType)) {
                return;
            }

            if (FluidAmount <= 0) {
                FluidType = drain.LiquidType;
            }
            FluidAmount += drain.Units;
            MachineData.UEvalue -= JobCostUE;
            ConsumeOneInput();
            PushOutput(drain.ReturnType);
            SendData();
        }

        /// <summary>装瓶:按储液类型匹配候选行,装满一件空容器</summary>
        private void TryFillJob(BottlingRecipes.FillRecipe[] fills) {
            if (FluidAmount <= 0) {
                return;
            }
            foreach (var fill in fills) {
                if (fill.LiquidType != FluidType || FluidAmount < fill.Units) {
                    continue;
                }
                if (!OutputCanTake(fill.ResultType)) {
                    continue;
                }

                FluidAmount -= fill.Units;
                MachineData.UEvalue -= JobCostUE;
                ConsumeOneInput();
                PushOutput(fill.ResultType);
                SendData();
                return;
            }
        }

        #region 表现推进(纯客户端,零网络)
        /// <summary>镜像结算前置条件:电够+有可处理输入+液路成立+输出有位</summary>
        private bool CanWorkVisual() {
            if (Disabled || MachineData.UEvalue < JobCostUE || InputItem == null || InputItem.IsAir) {
                return false;
            }
            if (BottlingRecipes.DrainTable.TryGetValue(InputItem.type, out var drain)) {
                visualIsDrain = true;
                return CanAcceptFluid(drain.LiquidType)
                    && FluidCapacity - FluidAmount >= drain.Units
                    && OutputCanTake(drain.ReturnType);
            }
            if (BottlingRecipes.FillTable.TryGetValue(InputItem.type, out var fills)) {
                visualIsDrain = false;
                if (FluidAmount <= 0) {
                    return false;
                }
                foreach (var fill in fills) {
                    if (fill.LiquidType == FluidType && FluidAmount >= fill.Units && OutputCanTake(fill.ResultType)) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 灌装动画按节拍本地推进,推到满停格;成品槽变化(结算事件包)才闪光归零——
        /// 完成瞬间以服务器结算为准,不靠本地计时猜
        /// </summary>
        private void UpdateBottlingVisual() {
            animTime += 1f / 60f;
            visualWorking = CanWorkVisual();

            if (visualWorking) {
                fillT = MathHelper.Clamp(fillT + 1f / BeatTicks, 0f, 1f);
            }
            else {
                fillT = MathHelper.Lerp(fillT, 0f, 0.12f);
            }
            flashT *= 0.88f;

            //成品槽变化=一次结算完成;首帧只记快照,防入世/放置时的存量误判成完成
            int outType = OutputItem == null || OutputItem.IsAir ? 0 : OutputItem.type;
            int outStack = OutputItem == null || OutputItem.IsAir ? 0 : OutputItem.stack;
            bool completed = outputSnapshotInited && outType != 0
                && (outType != lastOutputType || outStack > lastOutputStack);
            outputSnapshotInited = true;
            lastOutputType = outType;
            lastOutputStack = outStack;
            if (!completed) {
                return;
            }

            flashT = 1f;
            fillT = 0f;
            if (!FluidVFX.NearLocalPlayer(CenterInWorld)) {
                return;
            }
            FluidStyle style = FluidVFX.GetStyle(FluidType);
            Vector2 bottlePos = new(CenterInWorld.X, PosInWorld.Y + Height - 10f);
            Defer(() => {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(bottlePos + Main.rand.NextVector2Circular(6f, 6f),
                        new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)), style.Bright, Main.rand.NextFloat(0.22f, 0.34f))
                        ?.Configure(style.Bright * 0.8f, 22, 0.05f, 0.8f);
                }
            });
        }
        #endregion

        #region 机面覆层:灌装窗(容器剪影+液面升降)+软管脉动+完成闪光+状态灯
        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 basePos = PosInWorld - Main.screenPosition;
            Color lit = Lighting.GetColor(Position.ToPoint());
            FluidStyle style = FluidVFX.GetStyle(FluidType);

            //状态灯:作业=青绿呼吸,有输入但阻塞=琥珀,空闲=熄灭
            Color lamp;
            bool hasInput = InputItem != null && !InputItem.IsAir;
            if (Disabled || (!visualWorking && !hasInput)) {
                lamp = new Color(30, 26, 24);
            }
            else if (visualWorking) {
                lamp = FluidVFX.Glow(new Color(90, 255, 170), 0.55f + 0.45f * MathF.Sin(animTime * 5f));
            }
            else {
                lamp = FluidVFX.Glow(new Color(255, 170, 50), 0.4f + 0.25f * MathF.Sin(animTime * 2.2f));
            }
            spriteBatch.Draw(px, new Rectangle((int)(basePos.X + Width) - 12, (int)basePos.Y + 8, 6, 6), lamp);

            if (!hasInput) {
                //最后一件输入被消耗的那次完成:借成品贴图把闪光打完
                if (flashT > 0.03f && lastOutputType > 0) {
                    Main.instance.LoadItem(lastOutputType);
                    Texture2D outTex = TextureAssets.Item[lastOutputType].Value;
                    float outFit = MathF.Min(30f / outTex.Width, 30f / outTex.Height);
                    Vector2 outPos = new(basePos.X + Width / 2f, basePos.Y + Height - 20f);
                    spriteBatch.Draw(outTex, outPos, null, FluidVFX.Glow(Color.White, flashT * 0.85f),
                        0f, outTex.Size() * 0.5f, outFit, SpriteEffects.None, 0f);
                }
                return;
            }

            //容器剪影:输入物品缩进机面下部
            Main.instance.LoadItem(InputItem.type);
            Texture2D itemTex = TextureAssets.Item[InputItem.type].Value;
            float fit = MathF.Min(30f / itemTex.Width, 30f / itemTex.Height);
            Vector2 bottleCenter = new(basePos.X + Width / 2f, basePos.Y + Height - 20f);
            Vector2 origin = itemTex.Size() * 0.5f;
            spriteBatch.Draw(itemTex, bottleCenter, null, new Color(120, 126, 138).MultiplyRGB(lit),
                0f, origin, fit, SpriteEffects.None, 0f);

            //瓶内液面:装瓶自下而上升,倒空自上而下降
            float frac = visualIsDrain ? 1f - fillT : fillT;
            if (frac > 0.02f && (visualWorking || fillT > 0.02f)) {
                int sliceH = (int)(itemTex.Height * MathHelper.Clamp(frac, 0f, 1f));
                if (sliceH > 0) {
                    Rectangle slice = new(0, itemTex.Height - sliceH, itemTex.Width, sliceH);
                    Vector2 slicePos = bottleCenter + new Vector2(0f, (itemTex.Height * 0.5f - sliceH) * fit);
                    spriteBatch.Draw(itemTex, slicePos, slice, style.Main * 0.9f,
                        0f, new Vector2(itemTex.Width * 0.5f, 0f), fit, SpriteEffects.None, 0f);
                    //液面亮线
                    spriteBatch.Draw(px, new Rectangle((int)(bottleCenter.X - itemTex.Width * fit * 0.4f),
                        (int)slicePos.Y, (int)(itemTex.Width * fit * 0.8f), 1),
                        FluidVFX.Glow(style.Bright, 0.4f));
                }
            }

            //软管脉动:作业中两粒液色辉点自机顶滑向瓶口
            if (visualWorking) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 hoseTop = new(basePos.X + Width / 2f, basePos.Y + 8f);
                Vector2 hoseBottom = bottleCenter - new Vector2(0f, itemTex.Height * fit * 0.5f + 2f);
                for (int i = 0; i < 2; i++) {
                    float t = (animTime * 0.9f + i * 0.5f) % 1f;
                    Vector2 dotPos = Vector2.Lerp(hoseTop, hoseBottom, t);
                    spriteBatch.Draw(glow, dotPos, null, FluidVFX.Glow(style.Bright, 0.4f * MathF.Sin(t * MathHelper.Pi)),
                        0f, glow.Size() * 0.5f, 0.12f, SpriteEffects.None, 0f);
                }
            }

            //完成闪光:整瓶过曝一拍
            if (flashT > 0.03f) {
                spriteBatch.Draw(itemTex, bottleCenter, null, FluidVFX.Glow(Color.White, flashT * 0.85f),
                    0f, origin, fit, SpriteEffects.None, 0f);
            }
        }
        #endregion

        /// <summary>右键交互(交互客户端执行):放入可处理容器/空手取成品/Shift 全取</summary>
        public void RightClickByTile() {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                //Shift 全部取出,直接入背包(MP 下地面掉落会被队友截走)
                if (InputItem != null && !InputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), InputItem.Clone());
                    InputItem.TurnToAir();
                }
                if (OutputItem != null && !OutputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), OutputItem.Clone());
                    OutputItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //手持可处理容器:放入输入槽(空槽/同型堆叠/异型不动)
            if (BottlingRecipes.CanProcess(item)) {
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

            //空手:取出成品
            if (item.IsAir && OutputItem != null && !OutputItem.IsAir) {
                Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), OutputItem.Clone());
                OutputItem.TurnToAir();
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        public override void MachineKill() {
            //槽内物品随拆机倒出(权威端)
            if (!VaultUtils.isClient) {
                if (InputItem != null && !InputItem.IsAir) {
                    DropItem(InputItem.Clone());
                }
                if (OutputItem != null && !OutputItem.IsAir) {
                    DropItem(OutputItem.Clone());
                }
            }
            InputItem?.TurnToAir();
            OutputItem?.TurnToAir();
        }

        #region 存档与同步:液体与槽位追加在基类之后
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write((byte)FluidType);
            data.Write(FluidAmount);
            ItemIO.Send(InputItem ?? new Item(), data, true, true);
            ItemIO.Send(OutputItem ?? new Item(), data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            FluidType = reader.ReadByte();
            FluidAmount = reader.ReadInt32();
            InputItem = ItemIO.Receive(reader, true, true);
            OutputItem = ItemIO.Receive(reader, true, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["FluidType"] = FluidType;
            tag["FluidAmount"] = FluidAmount;
            if (InputItem != null && !InputItem.IsAir) {
                tag["Bottling_InputItem"] = ItemIO.Save(InputItem);
            }
            if (OutputItem != null && !OutputItem.IsAir) {
                tag["Bottling_OutputItem"] = ItemIO.Save(OutputItem);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Water;
            FluidAmount = tag.TryGet("FluidAmount", out int amount) ? amount : 0;
            InputItem = CWRSaveData.LoadItemFromTag(tag, "Bottling_InputItem", nameof(BottlingMachineTP));
            OutputItem = CWRSaveData.LoadItemFromTag(tag, "Bottling_OutputItem", nameof(BottlingMachineTP));
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
