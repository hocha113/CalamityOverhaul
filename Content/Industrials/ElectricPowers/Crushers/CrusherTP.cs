using CalamityOverhaul.Common;
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
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    /// <summary>粉碎机槽位与进度</summary>
    internal class CrusherData : MachineData
    {
        /// <summary>矿料槽</summary>
        internal Item InputItem = new Item();
        /// <summary>碎矿槽(与投入同种)</summary>
        internal Item OutputItem = new Item();
        /// <summary>进度0..Max</summary>
        internal int CrushProgress;
        /// <summary>完成所需进度</summary>
        internal int MaxCrushProgress = 90;
        /// <summary>单tick耗电(单次作业共3UE均摊)</summary>
        internal float UEPerTick = 3f / 90f;
        /// <summary>电量上限</summary>
        internal float MaxUE = 500;
        internal bool IsWorking => CrushProgress > 0 && UEvalue >= UEPerTick;

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(CrushProgress);
            ItemIO.Send(InputItem ?? new Item(), data, true, true);
            ItemIO.Send(OutputItem ?? new Item(), data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            CrushProgress = reader.ReadInt32();
            InputItem = ItemIO.Receive(reader, true, true);
            OutputItem = ItemIO.Receive(reader, true, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["Crusher_CrushProgress"] = CrushProgress;
            if (InputItem != null && !InputItem.IsAir) {
                tag["Crusher_InputItem"] = ItemIO.Save(InputItem);
            }
            if (OutputItem != null && !OutputItem.IsAir) {
                tag["Crusher_OutputItem"] = ItemIO.Save(OutputItem);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (!tag.TryGet("Crusher_CrushProgress", out CrushProgress)) {
                CrushProgress = 0;
            }
            InputItem = CWRSaveData.LoadItemFromTag(tag, "Crusher_InputItem", nameof(CrusherData));
            OutputItem = CWRSaveData.LoadItemFromTag(tag, "Crusher_OutputItem", nameof(CrusherData));
        }
    }

    /// <summary>粉碎机TP:2 矿进 3 矿出,产物同种,进度式作业</summary>
    internal class CrusherTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<CrusherTile>();
        public override int TargetItem => ModContent.ItemType<Crusher>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500 * ModuleRack.StorageMult;

        /// <summary>模块槽数</summary>
        internal const int ModuleSlotCount = 3;
        internal readonly MachineModuleRack ModuleRack = new(MachineModuleTarget.Crusher);

        internal CrusherData CruData => MachineData as CrusherData;
        /// <summary>工况抖动偏移,瓦片绘制消费</summary>
        internal Vector2 offsetPos;
        /// <summary>自动进料节拍</summary>
        private int autoFeedTimer;

        //===== 表现层字段(纯客户端,零网络) =====
        /// <summary>瓦片状态灯消费的警示状态</summary>
        internal ProcAlert VisualAlert;
        /// <summary>咬合/完成冲击的衰减振幅</summary>
        private float kickAmp;
        /// <summary>蓄压微颤的采样保持</summary>
        private Vector2 tremorHold;
        private int tremorTimer;
        //完成沿检测:上帧进度/出料/入料快照
        private int lastProgressVis;
        private int lastOutType;
        private int lastOutStack;
        private bool lastInputReady;
        private int lastInputType;
        private int fxCooldown;

        public override MachineData GetGeneratorDataInds() => new CrusherData {
            MaxUE = MaxUEValue,
        };

        public override void UpdateMachine() {
            ModuleRack.EnsureSlots(ModuleSlotCount);
            ModuleRack.Refresh();
            //储能扩容模块动上限,数据侧字段每帧对齐
            CruData.MaxUE = MaxUEValue;

            //表现层先行:缺电/堵料时下方作业分支早退,警示灯与冲击衰减仍要走
            UpdateVisualFX();

            //自动进料斗:输入槽空了就从近旁存储抽可粉碎矿(权威端,主线程经 Defer)
            if (!VaultUtils.isClient && ModuleRack.AutoFeed && ++autoFeedTimer >= 30) {
                autoFeedTimer = 0;
                if (CruData.InputItem == null || CruData.InputItem.IsAir) {
                    Defer(() => {
                        if (CruData.InputItem != null && !CruData.InputItem.IsAir) {
                            return;
                        }
                        Item got = MachineLogistics.TryWithdraw(Position,
                            stored => CrusherRecipes.CanCrush(stored), 30);
                        if (!got.IsAir) {
                            CruData.InputItem = got;
                            SendData();
                        }
                    });
                }
            }

            //没有电量时停止工作
            if (CruData.UEvalue < CruData.UEPerTick) {
                return;
            }

            //可否开始粉碎
            if (CruData.CrushProgress == 0 && CanStartCrushing()) {
                StartCrushing();
            }

            //执行粉碎
            if (CruData.CrushProgress > 0) {
                ProcessCrushing();
            }
        }

        private bool CanStartCrushing() {
            if (CruData.InputItem == null || CruData.InputItem.IsAir) {
                return false;
            }
            if (!CrusherRecipes.CanCrush(CruData.InputItem)) {
                return false;
            }
            if (CruData.InputItem.stack < CrusherRecipes.InputStack) {
                return false;
            }

            //输出槽必须为空或同种且有空间(产物与投入同种矿)
            if (CruData.OutputItem != null && !CruData.OutputItem.IsAir) {
                if (CruData.OutputItem.type != CruData.InputItem.type) {
                    return false;
                }
                if (CruData.OutputItem.stack + CrusherRecipes.OutputStack > CruData.OutputItem.maxStack) {
                    return false;
                }
            }
            return true;
        }

        private void StartCrushing() {
            CruData.CrushProgress = 1;
            if (!VaultUtils.isServer) {
                //并行阶段音效播放延迟到主线程执行(串行阶段立即执行)
                Defer(() => SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = -0.4f }, CenterInWorld));
            }
        }

        private void ProcessCrushing() {
            CruData.UEvalue -= CruData.UEPerTick;

            CruData.CrushProgress++;
            if (CruData.CrushProgress >= CruData.MaxCrushProgress) {
                CompleteCrushing();
            }
        }

        //=========================================================================
        // 表现层:警示状态、蓄压微颤、咬合冲击、完成喷吐。
        // 全部由同步字段与确定性冲程节拍驱动,各端本地自演,零网络
        //=========================================================================
        private void UpdateVisualFX() {
            if (Main.dedServ || CruData == null) {
                return;
            }

            bool powered = CruData.UEvalue >= CruData.UEPerTick;
            bool hasInput = CruData.InputItem != null && !CruData.InputItem.IsAir;
            bool inputCrushable = hasInput && CrusherRecipes.CanCrush(CruData.InputItem);

            //警示状态:缺电红呼吸;有料但开不了工(料不足两份/出料堵)黄呼吸
            if (!powered) {
                VisualAlert = ProcAlert.NoPower;
            }
            else if (inputCrushable && CruData.CrushProgress == 0 && !CanStartCrushing()) {
                VisualAlert = ProcAlert.Blocked;
            }
            else {
                VisualAlert = ProcAlert.None;
            }

            bool onScreen = ProcessingChainVFX.OnScreen(CenterInWorld);

            //完成沿:出料增长,或进度自满格跌零(自动出料直送存储时出料槽不增长)
            bool outNow = CruData.OutputItem != null && !CruData.OutputItem.IsAir;
            bool outGrew = outNow && (lastOutStack == 0
                || (CruData.OutputItem.type == lastOutType && CruData.OutputItem.stack > lastOutStack));
            bool progressFell = lastProgressVis >= CruData.MaxCrushProgress - 1
                && CruData.CrushProgress == 0 && lastInputReady;
            if (fxCooldown > 0) {
                fxCooldown--;
            }
            if ((outGrew || progressFell) && fxCooldown == 0) {
                fxCooldown = 10;
                if (onScreen) {
                    CompletionFX();
                }
            }

            //作业编舞:蓄压期微颤渐强,破碎瞬间冲击+岩屑迸溅
            if (CruData.IsWorking && onScreen) {
                float pressure = ProcessingChainVFX.JawPressure(CruData.CrushProgress);
                ProcessingChainVFX.JawCurve(CruData.CrushProgress, out bool bite);
                if (++tremorTimer >= 2) {
                    tremorTimer = 0;
                    tremorHold = new Vector2(Rand.NextFloat(-1f, 1f), Rand.NextFloat(-0.4f, 0.6f))
                        * (0.3f + pressure * 0.8f);
                }
                if (bite) {
                    kickAmp = 2.6f;
                    BiteFX();
                }
            }
            else {
                tremorHold *= 0.7f;
            }

            //机身位移 = 微颤 + 冲击衰减
            offsetPos = tremorHold;
            if (kickAmp > 0.05f) {
                offsetPos += new Vector2(Rand.NextFloat(-0.6f, 0.6f), 1f) * kickAmp;
                kickAmp *= 0.72f;
            }
            else {
                kickAmp = 0f;
            }

            //上帧快照
            lastProgressVis = CruData.CrushProgress;
            lastOutType = outNow ? CruData.OutputItem.type : 0;
            lastOutStack = outNow ? CruData.OutputItem.stack : 0;
            lastInputReady = inputCrushable && CruData.InputItem.stack >= CrusherRecipes.InputStack;
            if (hasInput) {
                lastInputType = CruData.InputItem.type;
            }
        }

        /// <summary>破碎瞬间:腔口岩屑迸溅+石尘,按输入矿取色</summary>
        private void BiteFX() {
            int oreType = CruData.InputItem?.IsAir == false ? CruData.InputItem.type : lastInputType;
            (Color body, Color glint) = ProcessingChainVFX.OreChipColors(oreType);
            Vector2 mouth = new(Position.X + 24f, Position.Y + 26f);
            //并行阶段粒子与音效延迟到主线程执行(串行阶段立即执行)
            Defer(() => {
                for (int k = 0; k < 5; k++) {
                    Vector2 vel = new(Rand.NextFloat(-2.4f, 2.4f), Rand.NextFloat(-3.2f, -1.1f));
                    PRTLoader.NewParticle<PRT_ProcChip>(mouth + new Vector2(Rand.NextFloat(-8f, 8f), 0f),
                        vel, body, Rand.NextFloat(0.8f, 1.25f))?.Configure(glint, Rand.Next(22, 34));
                }
                Dust.NewDust(mouth + new Vector2(-10f, -4f), 20, 6, DustID.Stone, 0f, -1.2f, 120, default, 1.0f);
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.24f, Pitch = -0.5f + Rand.NextFloat(0.12f)
                }, CenterInWorld);
            });
        }

        /// <summary>完成瞬间:出料口矿粒喷吐+机身细震</summary>
        private void CompletionFX() {
            (Color body, Color glint) = ProcessingChainVFX.OreChipColors(lastInputType);
            Vector2 outlet = new(Position.X + 40f, Position.Y + 39f);
            kickAmp = MathF.Max(kickAmp, 1.8f);
            Defer(() => {
                for (int k = 0; k < 8; k++) {
                    Vector2 vel = new(Rand.NextFloat(0.8f, 2.6f), Rand.NextFloat(-1.6f, -0.2f));
                    PRTLoader.NewParticle<PRT_ProcChip>(
                        outlet + new Vector2(Rand.NextFloat(-2f, 4f), Rand.NextFloat(-2f, 2f)),
                        vel, body, Rand.NextFloat(0.7f, 1.1f))?.Configure(glint, Rand.Next(26, 40));
                }
                Dust.NewDust(outlet, 8, 4, DustID.Stone, 0.6f, -0.8f, 130, default, 0.9f);
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.34f, Pitch = -0.15f }, CenterInWorld);
            });
        }

        private void CompleteCrushing() {
            //物品结算是权威端专属:客户端把进度停在满格等服务器的完成包
            if (VaultUtils.isClient) {
                CruData.CrushProgress = CruData.MaxCrushProgress;
                return;
            }

            if (!CrusherRecipes.CanCrush(CruData.InputItem)
                || CruData.InputItem.stack < CrusherRecipes.InputStack) {
                CruData.CrushProgress = 0;
                return;
            }

            int oreType = CruData.InputItem.type;

            //扣除投入
            CruData.InputItem.stack -= CrusherRecipes.InputStack;
            if (CruData.InputItem.stack <= 0) {
                CruData.InputItem.TurnToAir();
            }

            //产出同种矿
            if (CruData.OutputItem == null || CruData.OutputItem.IsAir) {
                CruData.OutputItem = new Item(oreType, CrusherRecipes.OutputStack);
            }
            else {
                CruData.OutputItem.stack += CrusherRecipes.OutputStack;
                if (CruData.OutputItem.stack > CruData.OutputItem.maxStack) {
                    CruData.OutputItem.stack = CruData.OutputItem.maxStack;
                }
            }

            CruData.CrushProgress = 0;

            //自动出料口:产物直接送进近旁存储(主线程经 Defer,原版箱走快照广播)
            if (ModuleRack.AutoEject) {
                Defer(() => {
                    if (CruData.OutputItem == null || CruData.OutputItem.IsAir) {
                        return;
                    }
                    Item toStore = CruData.OutputItem.Clone();
                    if (MachineLogistics.TryDeposit(Position, toStore)) {
                        CruData.OutputItem.TurnToAir();
                        SendData();
                    }
                });
            }

            SendData();
        }

        internal void HandleInputItem() {
            Item mouseItem = Main.mouseItem;

            //手持可粉碎矿,放入输入槽
            if (CrusherRecipes.CanCrush(mouseItem)) {
                if (CruData.InputItem == null || CruData.InputItem.IsAir) {
                    CruData.InputItem = mouseItem.Clone();
                    mouseItem.TurnToAir();
                }
                else if (CruData.InputItem.type == mouseItem.type) {
                    int space = CruData.InputItem.maxStack - CruData.InputItem.stack;
                    int transfer = System.Math.Min(space, mouseItem.stack);
                    CruData.InputItem.stack += transfer;
                    mouseItem.stack -= transfer;
                    if (mouseItem.stack <= 0) {
                        mouseItem.TurnToAir();
                    }
                }
                else {
                    //交换物品
                    Item temp = CruData.InputItem.Clone();
                    CruData.InputItem = mouseItem.Clone();
                    Main.mouseItem = temp;
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
                return;
            }

            //手为空,取出输入槽物品
            if (mouseItem.IsAir && CruData.InputItem != null && !CruData.InputItem.IsAir) {
                Main.mouseItem = CruData.InputItem.Clone();
                CruData.InputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        internal void HandleOutputItem() {
            Item mouseItem = Main.mouseItem;

            if (CruData.OutputItem == null || CruData.OutputItem.IsAir) {
                return;
            }

            if (mouseItem.IsAir) {
                Main.mouseItem = CruData.OutputItem.Clone();
                CruData.OutputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
            else if (mouseItem.type == CruData.OutputItem.type) {
                int space = mouseItem.maxStack - mouseItem.stack;
                int transfer = System.Math.Min(space, CruData.OutputItem.stack);
                mouseItem.stack += transfer;
                CruData.OutputItem.stack -= transfer;
                if (CruData.OutputItem.stack <= 0) {
                    CruData.OutputItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        public void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            //Shift点击快速放入/取出
            if (Main.keyState.PressingShift()) {
                if (CrusherRecipes.CanCrush(item)) {
                    if (CruData.InputItem == null || CruData.InputItem.IsAir) {
                        CruData.InputItem = item.Clone();
                        item.TurnToAir();
                    }
                    else if (CruData.InputItem.type == item.type) {
                        int space = CruData.InputItem.maxStack - CruData.InputItem.stack;
                        int transfer = System.Math.Min(space, item.stack);
                        CruData.InputItem.stack += transfer;
                        item.stack -= transfer;
                        if (item.stack <= 0) {
                            item.TurnToAir();
                        }
                    }
                    SendData();
                    SoundEngine.PlaySound(SoundID.Grab);
                    return;
                }

                //Shift点击取出所有物品(直接入背包,MP下地面掉落会被队友截走)
                if (CruData.InputItem != null && !CruData.InputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), CruData.InputItem.Clone());
                    CruData.InputItem.TurnToAir();
                }
                if (CruData.OutputItem != null && !CruData.OutputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), CruData.OutputItem.Clone());
                    CruData.OutputItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //打开UI
            var ui = UIHandleLoader.GetUIHandleOfType<CrusherUI>();
            if (ui != null) {
                ui.Interactive(this, newTP);
            }
        }

        public override void MachineKill() {
            //掉落槽内物品与模块
            if (!VaultUtils.isClient) {
                if (CruData.InputItem != null && !CruData.InputItem.IsAir) {
                    DropItem(CruData.InputItem.Clone());
                }
                if (CruData.OutputItem != null && !CruData.OutputItem.IsAir) {
                    DropItem(CruData.OutputItem.Clone());
                }
                ModuleRack.EnsureSlots(ModuleSlotCount);
                ModuleRack.DropAll(item => DropItem(item));
            }

            CruData.InputItem?.TurnToAir();
            CruData.OutputItem?.TurnToAir();

            //关闭UI
            var ui = UIHandleLoader.GetUIHandleOfType<CrusherUI>();
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
