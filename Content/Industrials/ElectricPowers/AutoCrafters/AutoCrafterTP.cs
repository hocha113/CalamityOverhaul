using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers;
using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Storages;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoCrafters
{
    /// <summary>自动合成台槽位、钉选身份与进度</summary>
    internal class AutoCrafterData : MachineData
    {
        /// <summary>样品槽:只做配方筛选,不被消耗,可随时取回</summary>
        internal Item SampleItem = new Item();
        /// <summary>成品槽</summary>
        internal Item OutputItem = new Item();
        /// <summary>钉选身份:产物 type,0 表示未钉选</summary>
        internal int PinnedResultType;
        /// <summary>钉选身份:产物数量</summary>
        internal int PinnedResultStack;
        /// <summary>钉选身份:原料无序哈希</summary>
        internal int PinnedHash;
        /// <summary>进度0..Max</summary>
        internal int CraftProgress;
        /// <summary>完成所需进度(60tick一拍)</summary>
        internal int MaxCraftProgress = 60;
        /// <summary>单次合成一次性耗电</summary>
        internal float CraftCost = 5f;
        /// <summary>电量上限</summary>
        internal float MaxUE = 800;

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(PinnedResultType);
            data.Write(PinnedResultStack);
            data.Write(PinnedHash);
            data.Write(CraftProgress);
            ItemIO.Send(SampleItem ?? new Item(), data, true, true);
            ItemIO.Send(OutputItem ?? new Item(), data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            PinnedResultType = reader.ReadInt32();
            PinnedResultStack = reader.ReadInt32();
            PinnedHash = reader.ReadInt32();
            CraftProgress = reader.ReadInt32();
            SampleItem = ItemIO.Receive(reader, true, true);
            OutputItem = ItemIO.Receive(reader, true, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["AutoCrafter_PinnedResultType"] = PinnedResultType;
            tag["AutoCrafter_PinnedResultStack"] = PinnedResultStack;
            tag["AutoCrafter_PinnedHash"] = PinnedHash;
            if (SampleItem != null && !SampleItem.IsAir) {
                tag["AutoCrafter_SampleItem"] = ItemIO.Save(SampleItem);
            }
            if (OutputItem != null && !OutputItem.IsAir) {
                tag["AutoCrafter_OutputItem"] = ItemIO.Save(OutputItem);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (!tag.TryGet("AutoCrafter_PinnedResultType", out PinnedResultType)) {
                PinnedResultType = 0;
            }
            if (!tag.TryGet("AutoCrafter_PinnedResultStack", out PinnedResultStack)) {
                PinnedResultStack = 0;
            }
            if (!tag.TryGet("AutoCrafter_PinnedHash", out PinnedHash)) {
                PinnedHash = 0;
            }
            CraftProgress = 0;
            SampleItem = CWRSaveData.LoadItemFromTag(tag, "AutoCrafter_SampleItem", nameof(AutoCrafterData));
            OutputItem = CWRSaveData.LoadItemFromTag(tag, "AutoCrafter_OutputItem", nameof(AutoCrafterData));
        }
    }

    /// <summary>
    /// 自动合成台TP:UI 钉选配方,每拍从近旁存储盘点进料,权威端原子合成。<br/>
    /// 配方身份存"产物+原料哈希"抗模组增删;原版箱子改动全走 ChestNetSync;
    /// 成品优先送近旁存储,送不出去落成品槽
    /// </summary>
    internal class AutoCrafterTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<AutoCrafterTile>();
        public override int TargetItem => ModContent.ItemType<AutoCrafter>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;

        internal AutoCrafterData CrafterData => MachineData as AutoCrafterData;

        /// <summary>钉选解析缓存;钉选变更或载入后置脏</summary>
        private Recipe resolvedRecipe;
        private bool resolveDirty = true;
        /// <summary>钉选着但解析不到(模组增删把配方抽走了)</summary>
        internal bool PinMissing { get; private set; }

        /// <summary>站台与液体快照,60tick 刷新</summary>
        private StationSnapshot stationSnap;
        private int stationTimer;
        internal bool StationOk { get; private set; }
        internal bool ConditionsOk { get; private set; }

        /// <summary>材料盘点缓存,30tick 主线程刷新;缺料的原料 type 供 UI 读</summary>
        internal bool MaterialsOk { get; private set; }
        internal int MissingIngredientType { get; private set; }
        private int materialTimer;

        //===== 表现层字段(纯客户端,零网络),瓦片绘制消费 =====
        /// <summary>装配头横位 0..1:作业时随进度双往返,阻塞冻在半路,待机缓回中位</summary>
        internal float HeadX01 = 0.5f;
        /// <summary>进度推进包络:打印线/头尖微光的强度</summary>
        internal float AdvanceGlow;
        /// <summary>完成闪光余量(tick),瓦片画全彩定格与亮闪</summary>
        internal int CompleteFlash;
        /// <summary>瓦片状态灯消费的警示状态</summary>
        internal ProcAlert VisualAlert;
        //完成沿与开工沿检测:上帧进度/出料快照
        private int lastProgressVis;
        private int lastOutType;
        private int lastOutStack;
        private int fxCooldown;

        internal Recipe ResolvedRecipe {
            get {
                RefreshResolve();
                return resolvedRecipe;
            }
        }

        public override MachineData GetGeneratorDataInds() => new AutoCrafterData {
            MaxUE = MaxUEValue,
        };

        internal void MarkResolveDirty() {
            resolveDirty = true;
            //钉选变了,旧的盘点结论作废
            materialTimer = 999;
            CrafterData.CraftProgress = 0;
        }

        /// <summary>按身份重解析钉选配方(只读 Main.recipe,任意端可跑)</summary>
        private void RefreshResolve() {
            if (!resolveDirty) {
                return;
            }
            resolveDirty = false;
            PinMissing = false;
            resolvedRecipe = null;

            if (CrafterData.PinnedResultType <= ItemID.None) {
                return;
            }
            resolvedRecipe = AutoCrafterRecipeId.Resolve(
                CrafterData.PinnedResultType, CrafterData.PinnedResultStack, CrafterData.PinnedHash);
            if (resolvedRecipe == null) {
                PinMissing = true;
            }
        }

        public override void UpdateMachine() {
            RefreshResolve();

            //表现层先行:下方按解析结果早退,警示灯与装配头动态仍要走
            UpdateVisualFX();

            //站台快照:只读 tile,并行阶段可直扫,按 WhoAmI 错峰
            if (stationSnap == null || ++stationTimer >= 60) {
                stationTimer = WhoAmI % 20;
                stationSnap = StationSnapshot.Scan(Position, Width / 16, Height / 16);
            }

            Recipe recipe = resolvedRecipe;
            if (recipe == null) {
                StationOk = ConditionsOk = MaterialsOk = false;
                CrafterData.CraftProgress = 0;
                return;
            }

            StationOk = stationSnap.SatisfiesTiles(recipe);
            ConditionsOk = stationSnap.SatisfiesConditions(recipe);

            //材料盘点:存储遍历固定主线程(并行阶段经 Defer 转发),结果缓存供推进与 UI
            if (++materialTimer >= 30) {
                materialTimer = 0;
                Defer(() => {
                    Recipe current = resolvedRecipe;
                    if (current == null) {
                        MaterialsOk = false;
                        return;
                    }
                    MaterialsOk = CountMaterials(current, out int missing);
                    MissingIngredientType = missing;
                });
            }

            //推进闸:电量够一次合成、站台条件材料齐、成品有去处
            bool outputOk = OutputSlotAcceptable(recipe);
            bool ready = CrafterData.UEvalue >= CrafterData.CraftCost
                && StationOk && ConditionsOk && MaterialsOk && outputOk;

            if (!ready) {
                //不满足冻结进度,恢复后接着走
                return;
            }

            CrafterData.CraftProgress++;
            if (CrafterData.CraftProgress >= CrafterData.MaxCraftProgress) {
                //客户端停在满格等服务器的完成包,本地结算会用漂移状态覆盖真实槽位
                if (VaultUtils.isClient) {
                    CrafterData.CraftProgress = CrafterData.MaxCraftProgress;
                    return;
                }
                Defer(TryCraftOnce);
            }
        }

        /// <summary>成品槽可否接住本次产物(空槽或同种且留得下)</summary>
        private bool OutputSlotAcceptable(Recipe recipe) {
            Item output = CrafterData.OutputItem;
            if (output == null || output.IsAir) {
                return true;
            }
            if (output.type != recipe.createItem.type) {
                return false;
            }
            return output.stack + recipe.createItem.stack <= output.maxStack;
        }

        /// <summary>盘点近旁存储的材料够不够一次合成;缺料时给出第一个缺口</summary>
        private bool CountMaterials(Recipe recipe, out int missingType) {
            missingType = 0;
            foreach (Item required in recipe.requiredItem) {
                if (required == null || required.IsAir) {
                    continue;
                }
                long have = 0;
                foreach (IStorageProvider provider in StorageLoader.FindAllStorageTargets(Position, MachineLogistics.SearchRange)) {
                    if (provider == null || !provider.IsValid || provider.Position == Position) {
                        continue;
                    }
                    foreach (Item stored in provider.GetStoredItems()) {
                        if (stored == null || stored.IsAir) {
                            continue;
                        }
                        if (AutoCrafterRecipeId.MatchesIngredient(recipe, stored, required)) {
                            have += stored.stack;
                        }
                    }
                    if (have >= required.stack) {
                        break;
                    }
                }
                if (have < required.stack) {
                    missingType = required.type;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 权威端原子合成:全检 → 计划抽料 → 执行抽料 → 扣电产出。
        /// 主线程语境(Defer 转发),原版箱子改动逐箱走 ChestNetSync
        /// </summary>
        private void TryCraftOnce() {
            Recipe recipe = resolvedRecipe;
            if (recipe == null || CrafterData.UEvalue < CrafterData.CraftCost
                || !StationOk || !ConditionsOk || !OutputSlotAcceptable(recipe)) {
                CrafterData.CraftProgress = 0;
                return;
            }

            if (!ConsumeIngredients(recipe)) {
                //盘点缓存过期,材料实际不够:归零重来,缓存下拍刷新
                CrafterData.CraftProgress = 0;
                MaterialsOk = false;
                return;
            }

            CrafterData.UEvalue -= CrafterData.CraftCost;
            CrafterData.CraftProgress = 0;

            //产出:优先直送近旁存储,送不出去落成品槽,再溢出落地(不吞产物)
            Item result = recipe.createItem.Clone();
            result.stack = recipe.createItem.stack;
            if (!MachineLogistics.TryDeposit(Position, result) && result.stack > 0) {
                Item output = CrafterData.OutputItem;
                if (output == null || output.IsAir) {
                    CrafterData.OutputItem = result.Clone();
                    result.stack = 0;
                }
                else if (output.type == result.type) {
                    int space = output.maxStack - output.stack;
                    int put = System.Math.Min(space, result.stack);
                    output.stack += put;
                    result.stack -= put;
                }
                if (result.stack > 0) {
                    DropItem(result);
                }
            }

            //完成音效移交客户端完成沿检测(CompletionFX),联机下远端也听得到
            SendData();
        }

        //=========================================================================
        // 表现层:警示状态、装配头双往返、开工入料流光、完成闪光环。
        // 全部由同步字段与本地确定性节拍驱动,各端自演,零网络
        //=========================================================================
        private void UpdateVisualFX() {
            if (Main.dedServ || CrafterData == null) {
                return;
            }

            bool powered = CrafterData.UEvalue >= CrafterData.CraftCost;
            bool pinned = CrafterData.PinnedResultType > 0;
            Recipe recipe = resolvedRecipe;

            //警示状态:缺电红呼吸;钉着但走不动(配方失踪/缺站台/缺料/成品堵)黄呼吸
            if (!powered) {
                VisualAlert = ProcAlert.NoPower;
            }
            else if (pinned && (PinMissing || recipe == null || !StationOk || !ConditionsOk
                || !MaterialsOk || !OutputSlotAcceptable(recipe))) {
                VisualAlert = ProcAlert.Blocked;
            }
            else {
                VisualAlert = ProcAlert.None;
            }

            int progress = CrafterData.CraftProgress;
            bool working = progress > 0;
            bool advancing = working && progress != lastProgressVis;
            AdvanceGlow = MathHelper.Lerp(AdvanceGlow, advancing ? 1f : 0f, advancing ? 0.3f : 0.06f);

            //装配头:作业时随进度双往返(阻塞进度冻结,头就冻在半路),待机缓回中位
            if (working) {
                float u = progress / (float)CrafterData.MaxCraftProgress * 2f;
                float frac = u - MathF.Floor(u);
                HeadX01 = 1f - MathF.Abs(1f - 2f * frac);
            }
            else {
                HeadX01 = MathHelper.Lerp(HeadX01, 0.5f, 0.06f);
            }

            bool onScreen = ProcessingChainVFX.OnScreen(CenterInWorld);

            //开工沿:原料聚拢,入料流光滑入台面
            if (lastProgressVis == 0 && working && onScreen) {
                IntakeFX();
            }

            //完成沿:成品增长,或进度自满格跌零(成品常直送近旁存储,槽不增长)
            bool outNow = CrafterData.OutputItem != null && !CrafterData.OutputItem.IsAir;
            bool outGrew = outNow && (lastOutStack == 0
                || (CrafterData.OutputItem.type == lastOutType && CrafterData.OutputItem.stack > lastOutStack));
            bool progressFell = lastProgressVis >= CrafterData.MaxCraftProgress - 1 && progress == 0;
            if (fxCooldown > 0) {
                fxCooldown--;
            }
            if ((outGrew || progressFell) && fxCooldown == 0) {
                fxCooldown = 10;
                CompletionFX(onScreen);
            }

            if (CompleteFlash > 0) {
                CompleteFlash--;
            }

            //上帧快照
            lastProgressVis = progress;
            lastOutType = outNow ? CrafterData.OutputItem.type : 0;
            lastOutStack = outNow ? CrafterData.OutputItem.stack : 0;
        }

        /// <summary>开工瞬间:入料流光自机体两侧滑入台面</summary>
        private void IntakeFX() {
            Vector2 table = new(Position.X + 24f, Position.Y + 26f);
            Defer(() => {
                for (int k = 0; k < 3; k++) {
                    float side = k % 2 == 0 ? -1f : 1f;
                    Vector2 spawn = table + new Vector2(side * Rand.NextFloat(34f, 52f),
                        Rand.NextFloat(-18f, -2f));
                    Vector2 vel = new(-side * 2.4f, Rand.NextFloat(0.2f, 1.2f));
                    PRTLoader.NewParticle<PRT_ProcIntake>(spawn, vel,
                        new Color(140, 210, 255), Rand.NextFloat(0.8f, 1.2f))
                        ?.Configure(table + new Vector2(Rand.NextFloat(-6f, 6f), 0f), 30);
                }
            });
        }

        /// <summary>完成瞬间:闪光环+星芒+上飘蓝尘,全彩定格由瓦片按 CompleteFlash 画</summary>
        private void CompletionFX(bool onScreen) {
            CompleteFlash = 18;
            Vector2 holo = new(Position.X + 24f, Position.Y + 15f);
            Defer(() => {
                SoundEngine.PlaySound(SoundID.Research with { Volume = 0.5f, Pitch = 0.2f }, CenterInWorld);
                if (!onScreen) {
                    return;
                }
                PRTLoader.NewParticle<PRT_ProcRing>(holo, Vector2.Zero,
                    new Color(120, 200, 255), 1f)?.Configure(6f, 26f, 16);
                PRTLoader.NewParticle<PRT_Sparkle>(holo, new Vector2(0f, -0.4f),
                    new Color(200, 240, 255), 0.4f)?.Configure(new Color(120, 200, 255), 16, 0.1f, 1.2f);
                for (int k = 0; k < 4; k++) {
                    Vector2 vel = new(Rand.NextFloat(-0.8f, 0.8f), Rand.NextFloat(-1.6f, -0.6f));
                    PRTLoader.NewParticle<PRT_ProcIntake>(holo + new Vector2(Rand.NextFloat(-8f, 8f), 4f), vel,
                        new Color(120, 200, 255), Rand.NextFloat(0.6f, 0.9f))
                        ?.Configure(holo + new Vector2(Rand.NextFloat(-14f, 14f), -22f), 24);
                }
            });
        }

        /// <summary>
        /// 抽料两段式:先只读计划(不动任何箱子),计划齐了再逐条执行;
        /// 执行中断时把已抽的退回存储或落地,保证不吞材料
        /// </summary>
        private bool ConsumeIngredients(Recipe recipe) {
            List<(IStorageProvider Provider, int ItemType, int Take)> plan = [];

            //计划:对每个原料在存储里找抵充来源(同 type 或配方组成员)
            foreach (Item required in recipe.requiredItem) {
                if (required == null || required.IsAir) {
                    continue;
                }
                int needed = required.stack;
                foreach (IStorageProvider provider in StorageLoader.FindAllStorageTargets(Position, MachineLogistics.SearchRange)) {
                    if (provider == null || !provider.IsValid || provider.Position == Position) {
                        continue;
                    }
                    foreach (Item stored in provider.GetStoredItems()) {
                        if (stored == null || stored.IsAir || needed <= 0) {
                            continue;
                        }
                        if (!AutoCrafterRecipeId.MatchesIngredient(recipe, stored, required)) {
                            continue;
                        }
                        int take = System.Math.Min(needed, stored.stack);
                        plan.Add((provider, stored.type, take));
                        needed -= take;
                    }
                    if (needed <= 0) {
                        break;
                    }
                }
                if (needed > 0) {
                    return false;
                }
            }

            //执行:逐条抽取,原版箱子逐箱快照广播
            List<Item> taken = [];
            foreach ((IStorageProvider provider, int itemType, int take) in plan) {
                ChestNetSync.Snapshot snap = ChestNetSync.Capture(provider);
                Item got = provider.WithdrawItem(itemType, take);
                if (got == null || got.IsAir || got.stack < take) {
                    //计划与执行间状态漂移(同帧主线程内理论不发生,防御性兜底):退料
                    if (got != null && !got.IsAir) {
                        taken.Add(got);
                    }
                    foreach (Item back in taken) {
                        if (!MachineLogistics.TryDeposit(Position, back) && back.stack > 0) {
                            DropItem(back);
                        }
                    }
                    return false;
                }
                ChestNetSync.SendChanged(snap.ChestIndex, ChestNetSync.CollectChanged(snap));
                taken.Add(got);
            }
            return true;
        }

        #region 槽位交互(交互客户端语境,本地改+SendData 推送)
        internal void HandleSampleItem() {
            Item mouseItem = Main.mouseItem;

            if (!mouseItem.IsAir) {
                //样品槽只存身份,一件就够
                Item old = CrafterData.SampleItem;
                Item put = mouseItem.Clone();
                put.stack = 1;
                if (old != null && !old.IsAir) {
                    //换样品:旧样品回到手上(手上原物堆叠减一)
                    mouseItem.stack -= 1;
                    if (mouseItem.stack <= 0) {
                        Main.mouseItem = old.Clone();
                    }
                    else {
                        Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), old.Clone());
                    }
                }
                else {
                    mouseItem.stack -= 1;
                    if (mouseItem.stack <= 0) {
                        mouseItem.TurnToAir();
                    }
                }
                CrafterData.SampleItem = put;
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
                return;
            }

            if (CrafterData.SampleItem != null && !CrafterData.SampleItem.IsAir) {
                Main.mouseItem = CrafterData.SampleItem.Clone();
                CrafterData.SampleItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        internal void HandleOutputItem() {
            Item mouseItem = Main.mouseItem;

            if (CrafterData.OutputItem == null || CrafterData.OutputItem.IsAir) {
                return;
            }

            if (mouseItem.IsAir) {
                Main.mouseItem = CrafterData.OutputItem.Clone();
                CrafterData.OutputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
            else if (mouseItem.type == CrafterData.OutputItem.type) {
                int space = mouseItem.maxStack - mouseItem.stack;
                int transfer = System.Math.Min(space, CrafterData.OutputItem.stack);
                mouseItem.stack += transfer;
                CrafterData.OutputItem.stack -= transfer;
                if (CrafterData.OutputItem.stack <= 0) {
                    CrafterData.OutputItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        /// <summary>钉选/取消钉选一条配方(UI 点击,客户端权威推送)</summary>
        internal void PinRecipe(Recipe recipe) {
            if (recipe == null) {
                CrafterData.PinnedResultType = 0;
                CrafterData.PinnedResultStack = 0;
                CrafterData.PinnedHash = 0;
            }
            else {
                CrafterData.PinnedResultType = recipe.createItem.type;
                CrafterData.PinnedResultStack = recipe.createItem.stack;
                CrafterData.PinnedHash = AutoCrafterRecipeId.ComputeIngredientHash(recipe);
            }
            MarkResolveDirty();
            SendData();
        }
        #endregion

        public void RightClickByTile(bool newTP) {
            //Shift点击快速取出成品与样品(直接入背包,MP下地面掉落会被队友截走)
            if (Main.keyState.PressingShift()) {
                bool tookAny = false;
                if (CrafterData.OutputItem != null && !CrafterData.OutputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), CrafterData.OutputItem.Clone());
                    CrafterData.OutputItem.TurnToAir();
                    tookAny = true;
                }
                if (CrafterData.SampleItem != null && !CrafterData.SampleItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), CrafterData.SampleItem.Clone());
                    CrafterData.SampleItem.TurnToAir();
                    tookAny = true;
                }
                if (tookAny) {
                    SendData();
                    SoundEngine.PlaySound(SoundID.Grab);
                }
                return;
            }

            //打开UI
            var ui = UIHandleLoader.GetUIHandleOfType<AutoCrafterUI>();
            if (ui != null) {
                ui.Interactive(this, newTP);
            }
        }

        public override void MachineKill() {
            if (!VaultUtils.isClient) {
                if (CrafterData.SampleItem != null && !CrafterData.SampleItem.IsAir) {
                    DropItem(CrafterData.SampleItem.Clone());
                }
                if (CrafterData.OutputItem != null && !CrafterData.OutputItem.IsAir) {
                    DropItem(CrafterData.OutputItem.Clone());
                }
            }

            CrafterData.SampleItem?.TurnToAir();
            CrafterData.OutputItem?.TurnToAir();

            //关闭UI
            var ui = UIHandleLoader.GetUIHandleOfType<AutoCrafterUI>();
            if (ui != null && ui.CurrentTP == this) {
                ui.IsActive = false;
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            //钉选身份可能随包变化,解析缓存重建
            resolveDirty = true;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            resolveDirty = true;
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
