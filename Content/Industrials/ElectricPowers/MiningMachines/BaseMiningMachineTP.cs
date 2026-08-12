using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.Storages;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>
    /// 矿机共享基类:作业循环、勘探缓存、模块槽的存档/同步与效果聚合。<br/>
    /// Mk1/Mk2 只差参数。模块槽编辑走"本地改 + SendData 推送"的客户端权威模型,
    /// 产出掷骰仅在权威端执行
    /// </summary>
    internal abstract class BaseMiningMachineTP : BaseBattery
    {
        #region 参数差异
        /// <summary>机器层级(保留给规则的 MinTier 门槛)</summary>
        public abstract int MachineTier { get; }
        /// <summary>基础镐力</summary>
        public virtual float BasePickPower => 59f;
        /// <summary>基础作业周期(帧)</summary>
        public virtual int WorkInterval => 20;
        /// <summary>每周期产出判定基础概率</summary>
        public virtual float YieldChance => 0.10f;
        /// <summary>每周期能耗</summary>
        public virtual int WorkConsumeUE => 5;
        /// <summary>模块槽数</summary>
        public virtual int ModuleSlotCount => 2;
        /// <summary>勘探宽度(格)</summary>
        public virtual int ScanWidth => 40;
        /// <summary>勘探深度(格)</summary>
        public virtual int ScanDepth => 40;
        /// <summary>工作动画帧间隔与帧数</summary>
        public virtual int FrameInterval => 5;
        public virtual int FrameMax => 3;
        /// <summary>工况抖动幅度(像素)</summary>
        public virtual int ShakeAmp => 2;
        /// <summary>钻头尘埃的出现分母(NextBool)</summary>
        public virtual int DustDenominator => 6;
        /// <summary>钻头尘埃相对机器左上角的偏移</summary>
        public virtual Vector2 ExcavateOffset => new(10, 40);
        public virtual float WorkPitch => -0.2f;
        public virtual float WorkVolume => 0.6f;
        /// <summary>无法作业时的提示文本</summary>
        public abstract LocalizedText DontWorkText { get; }
        #endregion

        /// <summary>勘探重扫基础周期(帧),按 WhoAmI 错峰</summary>
        private const int SurveyInterval = 600;
        /// <summary>集装组件的存储搜索半径(像素);从机器左上角起算,要盖过 Mk2 的机身再留一箱余量</summary>
        private const int DepositSearchRange = 320;

        /// <summary>模块架:槽位存储/校验/存档/网络由插件层承担,矿机只做 IMiningModule 聚合</summary>
        internal readonly MachineModuleRack ModuleRack = new(MachineModuleTarget.MiningMachine);
        /// <summary>勘探缓存,扫描前为 null</summary>
        internal MiningSurvey Survey;
        private int surveyTimer;
        private bool modifiersDirty = true;

        internal int time;
        internal int time2;
        internal int frame;
        internal Vector2 offsetPos;

        //模块聚合结果
        internal float EffectivePickPower;
        internal int EffectiveWorkInterval;
        internal float EffectiveYieldChance;
        internal int EffectiveEnergyCost;
        internal int EffectiveScanWidth;
        internal int EffectiveScanDepth;
        internal float DoubleDropChance;
        internal float RareBonus = 1f;
        internal float VeinMult = 1f;
        internal bool SmeltOutput;
        internal bool ChestDeposit;
        internal readonly HashSet<int> UnlockedOres = [];
        internal readonly Dictionary<int, float> OreFocus = [];
        /// <summary>现场熔炼的凑数账(矿物 ItemID → 未满一锭的余数),仅权威端使用</summary>
        private readonly Dictionary<int, int> smeltBuffer = [];

        /// <summary>地基是否满足作业条件,每帧刷新供 UI 读取</summary>
        internal bool FootingOk;
        internal bool Powered => MachineData != null && MachineData.UEvalue > CurrentEnergyCost;
        internal bool IsWorking => Powered && FootingOk;

        /// <summary>当前每周期能耗(含模块效果),取值前自动刷新聚合</summary>
        internal int CurrentEnergyCost {
            get {
                RefreshModifiers();
                return EffectiveEnergyCost;
            }
        }

        #region 模块槽
        internal Item[] EnsureModules() => ModuleRack.EnsureSlots(ModuleSlotCount);

        internal void MarkModulesDirty() {
            modifiersDirty = true;
            ModuleRack.MarkDirty();
        }

        /// <summary>聚合模块效果,脏标记驱动</summary>
        internal void RefreshModifiers() {
            if (!modifiersDirty) {
                return;
            }
            modifiersDirty = false;

            float pick = BasePickPower;
            float intervalMult = 1f;
            float yieldMult = 1f;
            float energyMult = 1f;
            float rareMult = 1f;
            float veinMult = 1f;
            float scanMult = 1f;
            float doubleMiss = 1f;
            bool smelt = false;
            bool deposit = false;
            UnlockedOres.Clear();
            OreFocus.Clear();
            foreach (Item item in EnsureModules()) {
                if (item == null || item.IsAir || item.ModItem is not IMiningModule module) {
                    continue;
                }
                pick += module.PickPowerBonus;
                intervalMult *= module.WorkIntervalMult;
                yieldMult *= module.YieldChanceMult;
                energyMult *= module.EnergyCostMult;
                rareMult *= module.RareByproductMult;
                veinMult *= module.VeinWeightMult;
                scanMult *= module.ScanSizeMult;
                doubleMiss *= 1f - MathHelper.Clamp(module.DoubleDropChance, 0f, 1f);
                smelt |= module.SmeltOutput;
                deposit |= module.ChestDeposit;
                module.CollectUnlockOres(UnlockedOres);
                module.CollectOreFocus(OreFocus);
            }
            EffectivePickPower = pick;
            EffectiveWorkInterval = Math.Max(4, (int)(WorkInterval * intervalMult));
            EffectiveYieldChance = YieldChance * yieldMult;
            EffectiveEnergyCost = Math.Max(1, (int)MathF.Round(WorkConsumeUE * energyMult));
            EffectiveScanWidth = Math.Max(8, (int)(ScanWidth * scanMult));
            EffectiveScanDepth = Math.Max(8, (int)(ScanDepth * scanMult));
            DoubleDropChance = 1f - doubleMiss;
            RareBonus = rareMult;
            VeinMult = veinMult;
            SmeltOutput = smelt;
            ChestDeposit = deposit;
        }

        /// <summary>同类模块每台限一枚</summary>
        internal bool HasModuleType(int itemType, int ignoreSlot = -1) {
            EnsureModules();
            return ModuleRack.HasType(itemType, ignoreSlot);
        }
        #endregion

        #region 勘探
        /// <summary>立即重扫(UI 打开/手动勘探用,主线程语境)</summary>
        internal void RescanNow() {
            RefreshModifiers();
            Survey = MiningSurvey.Scan(Position, Width / 16, Height / 16, EffectiveScanWidth, EffectiveScanDepth);
            surveyTimer = SurveyInterval + WhoAmI % 60 * 3;
        }

        private void UpdateSurvey() {
            if (Survey != null && --surveyTimer > 0) {
                return;
            }
            //扫描只读 tile 数据,并行阶段安全;尺寸吃勘探阵列模块的加成
            Survey = MiningSurvey.Scan(Position, Width / 16, Height / 16, EffectiveScanWidth, EffectiveScanDepth);
            surveyTimer = SurveyInterval + WhoAmI % 60 * 3;
        }

        internal MiningContext BuildContext() {
            RefreshModifiers();
            return new MiningContext {
                Tier = MachineTier,
                PickPower = EffectivePickPower,
                Survey = Survey,
                UnlockedOres = UnlockedOres,
                RareBonus = RareBonus,
                VeinMult = VeinMult,
                OreFocus = OreFocus,
            };
        }

        /// <summary>预计每分钟产出件数,供 UI 展示</summary>
        internal float EstimateYieldPerMinute() {
            RefreshModifiers();
            float richness = Survey?.VeinRichness ?? 0f;
            return 3600f / EffectiveWorkInterval * EffectiveYieldChance * (1f + richness * 0.5f);
        }
        #endregion

        #region 存档与同步
        public override void SendData(ModPacket data) {
            base.SendData(data);
            ModuleRack.Send(data, ModuleSlotCount);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ModuleRack.Receive(reader, ModuleSlotCount);
            MarkModulesDirty();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            ModuleRack.Save(tag, ModuleSlotCount);
            //熔炼余数账:平铺成 [id,数量,...] 保存
            if (smeltBuffer.Count > 0) {
                List<int> smeltData = [];
                foreach (KeyValuePair<int, int> pair in smeltBuffer) {
                    if (pair.Value > 0) {
                        smeltData.Add(pair.Key);
                        smeltData.Add(pair.Value);
                    }
                }
                tag["_SmeltBuffer"] = smeltData;
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            ModuleRack.Load(tag, ModuleSlotCount, GetType().Name);
            smeltBuffer.Clear();
            if (tag.TryGet("_SmeltBuffer", out List<int> smeltData)) {
                for (int i = 0; i + 1 < smeltData.Count; i += 2) {
                    if (smeltData[i + 1] > 0) {
                        smeltBuffer[smeltData[i]] = smeltData[i + 1];
                    }
                }
            }
            MarkModulesDirty();
        }
        #endregion

        /// <summary>右键交互:立即重扫并打开勘探终端(仅交互客户端会走到这里)</summary>
        public void RightEvent() {
            RescanNow();
            MiningMachineUI.Instance.Initialize(this);
        }

        /// <summary>作业条件:机身下方三行全为实心方块</summary>
        internal bool CheckFooting() {
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            for (int i = 0; i < tileWidth; i++) {
                for (int j = tileHeight; j < tileHeight + 3; j++) {
                    if (!Framing.GetTileSafely(Position + new Point16(i, j)).HasTile) {
                        return false;
                    }
                }
            }
            return true;
        }

        public override void UpdateMachine() {
            //通用聚合(储能扩容这类跨族效果)与矿机域聚合分开驱动
            EnsureModules();
            ModuleRack.Refresh();
            RefreshModifiers();
            UpdateSurvey();
            FootingOk = CheckFooting();

            if (MachineData.UEvalue <= EffectiveEnergyCost) {
                offsetPos = Vector2.Zero;
                return;
            }

            VaultUtils.ClockFrame(ref frame, FrameInterval, FrameMax);

            if (FootingOk) {
                if (!Main.dedServ) {
                    if (++time > 4) {
                        offsetPos = new Vector2(Rand.Next(-ShakeAmp, ShakeAmp), Rand.Next(0, ShakeAmp));
                        time = 0;
                    }

                    Vector2 excavatePos = PosInWorld + ExcavateOffset;
                    if (Rand.NextBool(DustDenominator)) {
                        //并行阶段Dust生成延迟到主线程执行(串行阶段立即执行)
                        Defer(() => Dust.NewDust(excavatePos, 1, 1, DustID.Stone));
                    }
                }

                if (++time2 > EffectiveWorkInterval) {
                    if (!Main.dedServ) {
                        //并行阶段音效播放延迟到主线程执行(串行阶段立即执行)
                        Defer(() => SoundEngine.PlaySound(SoundID.Item22 with { Pitch = WorkPitch, Volume = WorkVolume }, CenterInWorld));
                        Defer(() => SoundEngine.PlaySound(SoundID.Dig with { Pitch = WorkPitch, Volume = WorkVolume }, CenterInWorld));
                    }

                    if (!VaultUtils.isClient) {
                        TryYield();
                    }

                    MachineData.UEvalue -= EffectiveEnergyCost;
                    time2 = 0;
                }
                return;
            }

            if (!Main.dedServ) {
                if (++time2 > 4) {
                    //并行阶段音效播放延迟到主线程执行(串行阶段立即执行)
                    Defer(() => SoundEngine.PlaySound(SoundID.Item22 with { Pitch = WorkPitch, Volume = WorkVolume }, CenterInWorld));
                    time2 = 0;
                }

                if (++time > 180) {
                    //并行阶段CombatText生成及其后续修改延迟到主线程执行(串行阶段立即执行)
                    Defer(() => {
                        int text = CombatText.NewText(HitBox, Color.DarkSeaGreen, DontWorkText.Value);
                        Main.combatText[text].lifeTime *= 2;
                    });
                    time = 0;
                }
            }
        }

        /// <summary>权威端的产出判定:富矿提升判定频率,再按报告同源的权重掷骰</summary>
        private void TryYield() {
            float richness = Survey?.VeinRichness ?? 0f;
            float chance = EffectiveYieldChance * (1f + richness * 0.5f);
            if (Rand.NextFloat() >= chance) {
                return;
            }
            //并行阶段从TP的线程安全随机源取数后再计算掉落
            MiningContext ctx = BuildContext();
            if (!MiningMachineSystem.TryRollDrop(in ctx, Rand, out int itemID)) {
                return;
            }

            int stack = 1;
            if (DoubleDropChance > 0f && Rand.NextFloat() < DoubleDropChance) {
                stack = 2;
            }

            //现场熔炼:按原版配比凑数换锭,凑不满先记账
            if (SmeltOutput && MiningMachineSystem.SmeltTable.TryGetValue(itemID, out (int BarType, int OreCost) smelt)) {
                int total = smeltBuffer.GetValueOrDefault(itemID) + stack;
                int bars = total / smelt.OreCost;
                smeltBuffer[itemID] = total - bars * smelt.OreCost;
                if (bars <= 0) {
                    return;
                }
                itemID = smelt.BarType;
                stack = bars;
            }

            OutputItem(itemID, stack);
        }

        /// <summary>产出出口:装了集装组件优先存入近旁存储,失败回退落地</summary>
        private void OutputItem(int itemID, int stack) {
            if (!ChestDeposit) {
                DropItem(new Item(itemID, stack));
                return;
            }
            //存储查找与箱体写入固定在主线程执行(并行阶段经 Defer 转发,串行立即)
            Defer(() => {
                Item item = new(itemID, stack);
                foreach (IStorageProvider provider in StorageLoader.FindAllStorageTargets(Position, DepositSearchRange)) {
                    if (!provider.IsValid || !provider.CanAcceptItem(item)) {
                        continue;
                    }
                    //原版箱子改动后需广播变化槽位,否则开着箱子的玩家看到过期内容
                    ChestNetSync.Snapshot chestSnap = ChestNetSync.Capture(provider);
                    if (provider.DepositItem(item)) {
                        provider.PlayDepositAnimation();
                        ChestNetSync.SendChanged(chestSnap.ChestIndex, ChestNetSync.CollectChanged(chestSnap));
                        return;
                    }
                }
                //没有可用存储则落地(此时已在主线程,内部立即生成)
                DropItem(new Item(itemID, stack));
            });
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }
            EnsureModules();
            ModuleRack.DropAll(item => DropItem(item));
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();

        /// <summary>
        /// 矿机瓦片共用绘制:按 TP 的动画帧偏移取帧,缺电时压暗
        /// </summary>
        internal static bool DrawMachineTile<T>(int i, int j, SpriteBatch spriteBatch, int tileType, int frameRows) where T : BaseMiningMachineTP {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out T machine)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY + machine.frame * 18 * frameRows;
            Texture2D tex = TextureAssets.Tile[tileType].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange) + machine.offsetPos;
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (machine.MachineData.UEvalue < machine.CurrentEnergyCost) {
                drawColor.R /= 2;
                drawColor.G /= 2;
                drawColor.B /= 2;
                drawColor.A = 255;
            }

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }
}
