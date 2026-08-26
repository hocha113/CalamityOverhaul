using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers;
using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>回收机槽位与进度</summary>
    internal class RecyclerData : MachineData
    {
        /// <summary>装备槽</summary>
        internal Item InputItem = new Item();
        /// <summary>锭料槽</summary>
        internal Item OutputItem = new Item();
        /// <summary>进度0..Max</summary>
        internal int RecycleProgress;
        /// <summary>完成所需进度</summary>
        internal int MaxRecycleProgress = 120;
        /// <summary>单tick耗电(单次作业共5UE均摊)</summary>
        internal float UEPerTick = 5f / 120f;
        /// <summary>电量上限</summary>
        internal float MaxUE = 500;
        internal bool IsWorking => RecycleProgress > 0 && UEvalue >= UEPerTick;

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(RecycleProgress);
            ItemIO.Send(InputItem ?? new Item(), data, true, true);
            ItemIO.Send(OutputItem ?? new Item(), data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            RecycleProgress = reader.ReadInt32();
            InputItem = ItemIO.Receive(reader, true, true);
            OutputItem = ItemIO.Receive(reader, true, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["Recycler_RecycleProgress"] = RecycleProgress;
            if (InputItem != null && !InputItem.IsAir) {
                tag["Recycler_InputItem"] = ItemIO.Save(InputItem);
            }
            if (OutputItem != null && !OutputItem.IsAir) {
                tag["Recycler_OutputItem"] = ItemIO.Save(OutputItem);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (!tag.TryGet("Recycler_RecycleProgress", out RecycleProgress)) {
                RecycleProgress = 0;
            }
            InputItem = CWRSaveData.LoadItemFromTag(tag, "Recycler_InputItem", nameof(RecyclerData));
            OutputItem = CWRSaveData.LoadItemFromTag(tag, "Recycler_OutputItem", nameof(RecyclerData));
        }
    }

    /// <summary>回收机TP:装备按稀有度拆解成锭,数量权威端掷骰</summary>
    internal class RecyclerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<RecyclerTile>();
        public override int TargetItem => ModContent.ItemType<Recycler>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500 * ModuleRack.StorageMult;

        /// <summary>模块槽数</summary>
        internal const int ModuleSlotCount = 3;
        internal readonly MachineModuleRack ModuleRack = new(MachineModuleTarget.Recycler);

        internal RecyclerData RecData => MachineData as RecyclerData;
        /// <summary>自动进料节拍</summary>
        private int autoFeedTimer;

        //===== 表现层字段(纯客户端,零网络),瓦片绘制消费 =====
        /// <summary>拆解臂横位 0..1(工位编舞,进度确定性驱动)</summary>
        internal float ArmX01 = 0.5f;
        /// <summary>拆解臂下压 0..1</summary>
        internal float ArmDrop01;
        /// <summary>切割驻留强度 0..1,喂接触点亮斑</summary>
        internal float CutGlow;
        /// <summary>稀有度辉光脉冲,工位收尾泵起后衰减</summary>
        internal float RarityPulse;
        /// <summary>瓦片状态灯消费的警示状态</summary>
        internal ProcAlert VisualAlert;
        //五工位横位表:在被拆装备上跳动作业
        private static readonly float[] stations = [0.10f, 0.82f, 0.38f, 0.94f, 0.60f];
        private int sparkTimer;
        //完成沿检测:上帧进度/出料/入料快照
        private int lastProgressVis;
        private int lastOutType;
        private int lastOutStack;
        private bool lastInputReady;
        private int lastInputRare;
        private int fxCooldown;

        public override MachineData GetGeneratorDataInds() => new RecyclerData {
            MaxUE = MaxUEValue,
        };

        public override void UpdateMachine() {
            ModuleRack.EnsureSlots(ModuleSlotCount);
            ModuleRack.Refresh();
            //储能扩容模块动上限,数据侧字段每帧对齐
            RecData.MaxUE = MaxUEValue;

            //表现层先行:缺电时下方作业分支早退,警示灯与臂归位仍要走
            UpdateVisualFX();

            //自动进料斗:输入槽空了就从近旁存储抽可拆装备(权威端,主线程经 Defer)
            if (!VaultUtils.isClient && ModuleRack.AutoFeed && ++autoFeedTimer >= 30) {
                autoFeedTimer = 0;
                if (RecData.InputItem == null || RecData.InputItem.IsAir) {
                    Defer(() => {
                        if (RecData.InputItem != null && !RecData.InputItem.IsAir) {
                            return;
                        }
                        Item got = MachineLogistics.TryWithdraw(Position,
                            stored => RecyclerTables.CanRecycle(stored), 1);
                        if (!got.IsAir) {
                            RecData.InputItem = got;
                            SendData();
                        }
                    });
                }
            }

            //没有电量时停止工作
            if (RecData.UEvalue < RecData.UEPerTick) {
                return;
            }

            //可否开始拆解
            if (RecData.RecycleProgress == 0 && CanStartRecycling()) {
                StartRecycling();
            }

            //执行拆解
            if (RecData.RecycleProgress > 0) {
                ProcessRecycling();
            }
        }

        private bool CanStartRecycling() {
            if (!RecyclerTables.CanRecycle(RecData.InputItem)) {
                return false;
            }

            //输出槽须为空或与预估锭种一致且留有余量(掷骰上浮 +1)
            if (RecData.OutputItem != null && !RecData.OutputItem.IsAir) {
                (int barType, int baseCount) = RecyclerTables.ResolveByRarity(RecData.InputItem.rare);
                if (RecData.OutputItem.type != barType) {
                    return false;
                }
                if (RecData.OutputItem.stack + baseCount + 1 > RecData.OutputItem.maxStack) {
                    return false;
                }
            }
            return true;
        }

        private void StartRecycling() {
            RecData.RecycleProgress = 1;
            if (!VaultUtils.isServer) {
                //并行阶段音效播放延迟到主线程执行(串行阶段立即执行)
                Defer(() => SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = -0.1f }, CenterInWorld));
            }
        }

        private void ProcessRecycling() {
            RecData.UEvalue -= RecData.UEPerTick;

            RecData.RecycleProgress++;
            if (RecData.RecycleProgress >= RecData.MaxRecycleProgress) {
                CompleteRecycling();
            }
        }

        //=========================================================================
        // 表现层:五工位拆解编舞(移位→下压→切割驻留→抬升)、切割火花与零件碎片、
        // 稀有度辉光脉冲、完成锭落斗。进度确定性驱动,各端本地自演,零网络
        //=========================================================================
        private void UpdateVisualFX() {
            if (Main.dedServ || RecData == null) {
                return;
            }

            bool powered = RecData.UEvalue >= RecData.UEPerTick;
            bool hasInput = RecData.InputItem != null && !RecData.InputItem.IsAir;
            bool inputValid = hasInput && RecyclerTables.CanRecycle(RecData.InputItem);

            //警示状态:缺电红呼吸;有装备但开不了工(出料堵/锭种不符)黄呼吸
            if (!powered) {
                VisualAlert = ProcAlert.NoPower;
            }
            else if (inputValid && RecData.RecycleProgress == 0 && !CanStartRecycling()) {
                VisualAlert = ProcAlert.Blocked;
            }
            else {
                VisualAlert = ProcAlert.None;
            }

            bool onScreen = ProcessingChainVFX.OnScreen(CenterInWorld);

            //完成沿:出料增长,或进度自满格跌零(自动出料直送存储时出料槽不增长)
            bool outNow = RecData.OutputItem != null && !RecData.OutputItem.IsAir;
            bool outGrew = outNow && (lastOutStack == 0
                || (RecData.OutputItem.type == lastOutType && RecData.OutputItem.stack > lastOutStack));
            bool progressFell = lastProgressVis >= RecData.MaxRecycleProgress - 1
                && RecData.RecycleProgress == 0 && lastInputReady;
            if (fxCooldown > 0) {
                fxCooldown--;
            }
            if ((outGrew || progressFell) && fxCooldown == 0) {
                fxCooldown = 10;
                if (onScreen) {
                    CompletionFX(outGrew && outNow ? RecData.OutputItem.type : 0);
                }
            }

            //工位编舞
            RarityPulse *= 0.94f;
            if (RecData.IsWorking) {
                UpdateArmChoreography(onScreen);
            }
            else {
                //归位:臂回中,压下量放掉
                ArmX01 = MathHelper.Lerp(ArmX01, 0.5f, 0.08f);
                ArmDrop01 = MathHelper.Lerp(ArmDrop01, 0f, 0.15f);
                CutGlow = MathHelper.Lerp(CutGlow, 0f, 0.2f);
            }

            //上帧快照
            lastProgressVis = RecData.RecycleProgress;
            lastOutType = outNow ? RecData.OutputItem.type : 0;
            lastOutStack = outNow ? RecData.OutputItem.stack : 0;
            lastInputReady = inputValid;
            if (inputValid) {
                lastInputRare = RecData.InputItem.rare;
            }
        }

        /// <summary>
        /// 五工位循环(24tick/工位):移位6→下压4→切割驻留9→抬升4→歇1。
        /// 驻留期喷金属火花,偶发零件碎片;工位收尾泵稀有度辉光
        /// </summary>
        private void UpdateArmChoreography(bool onScreen) {
            int p = Math.Min(RecData.RecycleProgress, RecData.MaxRecycleProgress - 1);
            int cycle = Math.Min(p / 24, stations.Length - 1);
            int t = p % 24;
            float prevX = cycle == 0 ? 0.5f : stations[cycle - 1];
            float curX = stations[cycle];

            if (t < 6) {
                float m = MathHelper.SmoothStep(0f, 1f, t / 6f);
                ArmX01 = MathHelper.Lerp(prevX, curX, m);
                ArmDrop01 = MathHelper.Lerp(ArmDrop01, 0f, 0.4f);
                CutGlow = 0f;
            }
            else if (t < 10) {
                ArmX01 = curX;
                float q = (t - 6) / 4f;
                ArmDrop01 = q * q;
                CutGlow = 0f;
            }
            else if (t < 19) {
                ArmX01 = curX;
                ArmDrop01 = 1f;
                CutGlow = 1f;
                //切割火花:接触点扇形喷出,打台面反弹;低概率蹦出零件碎片
                if (onScreen && ++sparkTimer >= 2) {
                    sparkTimer = 0;
                    SpawnCutSparks(Rand.NextBool(9));
                }
            }
            else if (t < 23) {
                ArmX01 = curX;
                float r = (t - 19) / 4f;
                ArmDrop01 = 1f - r * (2f - r);
                CutGlow = 0f;
                if (t == 19) {
                    //工位收尾:稀有度辉光脉冲
                    RarityPulse = 1f;
                    if (onScreen) {
                        Color rare = ItemRarity.GetColor(lastInputRare);
                        Vector2 pulsePos = ContactWorldPos();
                        Defer(() => PRTLoader.NewParticle<PRT_Sparkle>(pulsePos, new Vector2(0f, -0.5f),
                            rare, 0.34f)?.Configure(rare, 16, 0.08f, 1.1f));
                    }
                }
            }
            else {
                ArmDrop01 = 0f;
                CutGlow = 0f;
            }
        }

        /// <summary>拆解臂尖与装备的接触点(世界坐标),瓦片几何同源</summary>
        private Vector2 ContactWorldPos()
            => new(Position.X + 17f + ArmX01 * 14f, Position.Y + 23f);

        /// <summary>切割火花+偶发零件碎片</summary>
        private void SpawnCutSparks(bool withShard) {
            Vector2 contact = ContactWorldPos();
            float floorY = Position.Y + 26f;
            Defer(() => {
                int count = Rand.Next(1, 3);
                for (int k = 0; k < count; k++) {
                    float dir = Rand.NextBool() ? 1f : -1f;
                    Vector2 vel = new(dir * Rand.NextFloat(1.4f, 3.4f), Rand.NextFloat(-2.4f, -0.4f));
                    PRTLoader.NewParticle<PRT_ProcSpark>(contact, vel,
                        new Color(255, 168, 64), Rand.NextFloat(0.8f, 1.3f))
                        ?.Configure(Rand.Next(14, 26), floorY);
                }
                if (withShard) {
                    Vector2 vel = new(Rand.NextFloat(-1.8f, 1.8f), Rand.NextFloat(-2.8f, -1.4f));
                    PRTLoader.NewParticle<PRT_ProcChip>(contact, vel,
                        new Color(96, 104, 100), Rand.NextFloat(0.7f, 1.0f))
                        ?.Configure(new Color(210, 220, 214), Rand.Next(24, 36), 0.85f);
                }
            });
        }

        /// <summary>完成瞬间:真实锭贴图弧线落入分选斗,触斗叮当</summary>
        private void CompletionFX(int knownBarType) {
            //出料直送存储时槽里看不到锭种,按稀有度确定性解析补上
            int barType = knownBarType > ItemID.None
                ? knownBarType : RecyclerTables.ResolveByRarity(lastInputRare).BarType;
            Vector2 spawn = new(Position.X + 26f, Position.Y + 20f);
            float floorY = Position.Y + 38f;
            Defer(() => {
                PRTLoader.NewParticle<PRT_ProcBarDrop>(spawn, new Vector2(1.7f, -2.4f),
                    Color.White, 1f)?.Configure(barType, floorY, 48);
                for (int k = 0; k < 3; k++) {
                    Vector2 vel = new(Rand.NextFloat(-1.6f, 2.2f), Rand.NextFloat(-2.2f, -0.6f));
                    PRTLoader.NewParticle<PRT_ProcSpark>(spawn + new Vector2(0f, 2f), vel,
                        new Color(255, 168, 64), Rand.NextFloat(0.7f, 1.0f))
                        ?.Configure(Rand.Next(12, 20), floorY);
                }
            });
        }

        private void CompleteRecycling() {
            //物品结算是权威端专属:客户端把进度停在满格等服务器的完成包
            if (VaultUtils.isClient) {
                RecData.RecycleProgress = RecData.MaxRecycleProgress;
                return;
            }

            if (!RecyclerTables.CanRecycle(RecData.InputItem)) {
                RecData.RecycleProgress = 0;
                return;
            }

            //权威掷骰:锭种确定性解析,数量 ±1 抖动 + 价值护栏
            RecyclerTables.RollOutput(RecData.InputItem, Rand, out int barType, out int count);

            //消耗一件装备
            RecData.InputItem.stack -= 1;
            if (RecData.InputItem.stack <= 0) {
                RecData.InputItem.TurnToAir();
            }

            //产出并入锭料槽;锭种不符(价值护栏降级)或溢出的部分落地,不吞物品
            int overflow = 0;
            if (RecData.OutputItem == null || RecData.OutputItem.IsAir) {
                RecData.OutputItem = new Item(barType, count);
            }
            else if (RecData.OutputItem.type == barType) {
                int space = RecData.OutputItem.maxStack - RecData.OutputItem.stack;
                int put = System.Math.Min(space, count);
                RecData.OutputItem.stack += put;
                overflow = count - put;
            }
            else {
                overflow = count;
            }
            if (overflow > 0) {
                DropItem(new Item(barType, overflow));
            }

            RecData.RecycleProgress = 0;

            //自动出料口:产物直接送进近旁存储(主线程经 Defer,原版箱走快照广播)
            if (ModuleRack.AutoEject) {
                Defer(() => {
                    if (RecData.OutputItem == null || RecData.OutputItem.IsAir) {
                        return;
                    }
                    Item toStore = RecData.OutputItem.Clone();
                    if (MachineLogistics.TryDeposit(Position, toStore)) {
                        RecData.OutputItem.TurnToAir();
                        SendData();
                    }
                });
            }

            SendData();
        }

        internal void HandleInputItem() {
            Item mouseItem = Main.mouseItem;

            //手持可拆装备,放入装备槽(装备均不可堆叠,直接交换)
            if (RecyclerTables.CanRecycle(mouseItem)) {
                if (RecData.InputItem == null || RecData.InputItem.IsAir) {
                    RecData.InputItem = mouseItem.Clone();
                    mouseItem.TurnToAir();
                }
                else {
                    Item temp = RecData.InputItem.Clone();
                    RecData.InputItem = mouseItem.Clone();
                    Main.mouseItem = temp;
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
                return;
            }

            //手为空,取出装备槽物品
            if (mouseItem.IsAir && RecData.InputItem != null && !RecData.InputItem.IsAir) {
                Main.mouseItem = RecData.InputItem.Clone();
                RecData.InputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        internal void HandleOutputItem() {
            Item mouseItem = Main.mouseItem;

            if (RecData.OutputItem == null || RecData.OutputItem.IsAir) {
                return;
            }

            if (mouseItem.IsAir) {
                Main.mouseItem = RecData.OutputItem.Clone();
                RecData.OutputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
            else if (mouseItem.type == RecData.OutputItem.type) {
                int space = mouseItem.maxStack - mouseItem.stack;
                int transfer = System.Math.Min(space, RecData.OutputItem.stack);
                mouseItem.stack += transfer;
                RecData.OutputItem.stack -= transfer;
                if (RecData.OutputItem.stack <= 0) {
                    RecData.OutputItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        public void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            //Shift点击快速放入/取出
            if (Main.keyState.PressingShift()) {
                if (RecyclerTables.CanRecycle(item) && (RecData.InputItem == null || RecData.InputItem.IsAir)) {
                    RecData.InputItem = item.Clone();
                    item.TurnToAir();
                    SendData();
                    SoundEngine.PlaySound(SoundID.Grab);
                    return;
                }

                //Shift点击取出所有物品(直接入背包,MP下地面掉落会被队友截走)
                if (RecData.InputItem != null && !RecData.InputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), RecData.InputItem.Clone());
                    RecData.InputItem.TurnToAir();
                }
                if (RecData.OutputItem != null && !RecData.OutputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), RecData.OutputItem.Clone());
                    RecData.OutputItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //打开UI
            var ui = UIHandleLoader.GetUIHandleOfType<RecyclerUI>();
            if (ui != null) {
                ui.Interactive(this, newTP);
            }
        }

        public override void MachineKill() {
            //掉落槽内物品与模块
            if (!VaultUtils.isClient) {
                if (RecData.InputItem != null && !RecData.InputItem.IsAir) {
                    DropItem(RecData.InputItem.Clone());
                }
                if (RecData.OutputItem != null && !RecData.OutputItem.IsAir) {
                    DropItem(RecData.OutputItem.Clone());
                }
                ModuleRack.EnsureSlots(ModuleSlotCount);
                ModuleRack.DropAll(item => DropItem(item));
            }

            RecData.InputItem?.TurnToAir();
            RecData.OutputItem?.TurnToAir();

            //关闭UI
            var ui = UIHandleLoader.GetUIHandleOfType<RecyclerUI>();
            if (ui != null && ui.CurrentTP == this) {
                ui.IsActive = false;
            }
        }

        #region 存档与同步:模块架追加在既有字段之后,槽数固定故两端对称
        public override void SendData(ModPacket data) {
            base.SendData(data);
            ModuleRack.Send(data, ModuleSlotCount);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ModuleRack.Receive(reader, ModuleSlotCount);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            ModuleRack.Save(tag, ModuleSlotCount);
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            ModuleRack.Load(tag, ModuleSlotCount, GetType().Name);
        }
        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
