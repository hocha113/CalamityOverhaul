using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.SlimeVats
{
    /// <summary>
    /// 史莱姆培养槽TP:水+电周期性培养凝胶,生物质发电机的燃料源头。<br/>
    /// 供水走无依赖设计:自动汲取机身邻接/下方的世界水体(权威端+原版液体同步),
    /// 或 UI 手动倒水桶;内部水缓冲 4 格(1020 单位,255=1格)。<br/>
    /// TODO(液体管道对接):液体管道网(Direction A,另一批次施工)落地后,
    /// 把 <see cref="WaterStored"/>/<see cref="WaterCapacity"/> 暴露为其 IFluidContainer
    /// (FluidType=LiquidID.Water)即可入网;本机数据语义已按 255单位=1格 对齐,无需改动结算
    /// </summary>
    internal class SlimeVatTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<SlimeVatTile>();
        public override int TargetItem => ModContent.ItemType<SlimeVat>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 500;
        /// <summary>全量包携带4格物品数据,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量

        internal const int ProduceSlotCount = 4;
        /// <summary>一轮培养的电力开销</summary>
        internal const float BrewCost = 8f;
        /// <summary>一轮培养的水耗(单位,255=1格)</summary>
        internal const int WaterCost = 255;
        /// <summary>一轮培养的凝胶产量</summary>
        internal const int GelPerCycle = 3;
        /// <summary>培养周期(tick),30秒一轮</summary>
        internal const int CycleTicks = 1800;
        /// <summary>内部水缓冲上限:4格</summary>
        internal const int WaterCapacity = 1020;
        //自动汲水节拍与扫描外扩(格)
        private const int PumpInterval = 30;
        private const int PumpExpand = 2;
        private const int PumpDepth = 4;
        //账本合批同步节流
        private const int NetInterval = 30;

        #endregion

        #region 字段

        internal Item[] Produce = new Item[ProduceSlotCount];
        internal bool Enabled = true;

        /// <summary>内部水缓冲(单位)</summary>
        internal int WaterStored;
        /// <summary>培养进度(tick 计),UI 进度条用</summary>
        internal float BrewProgress;

        internal bool IsWorking { get; private set; }
        internal float GlowIntensity;

        private int pumpTimer;
        private int textIdleTime;
        private byte brewRevision;
        private bool netDirty;
        private int netCooldown;

        #endregion

        #region 客户端视觉字段(凝胶丘/气泡/汲水,不入存档不入网络包)

        //凝胶丘承体:Fog 真 alpha 云团,多瓣错相形变拼出活凝胶
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Fog = null;

        /// <summary>派生作业强度 0~1,由已同步字段推出,MP 客户端也成立</summary>
        private float visualWork;
        /// <summary>蠕动相位:作业时快,休眠时残留极缓的一点余动</summary>
        private float gelPhase;
        /// <summary>丘体高度 0~1:随培养进度长起来,产出后瘪回去</summary>
        private float gelHeight;
        /// <summary>产出挤压回弹拍剩余帧</summary>
        private float squashTimer;
        private int bubbleTimer;
        private int lastWaterSeen = -1;
        /// <summary>汲水口水流演出剩余帧</summary>
        private int intakeTimer;

        /// <summary>凝胶丘底座中心(机身顶面)</summary>
        private Vector2 DomeCenter => PosInWorld + new Vector2(Width * 0.5f, 2f);

        /// <summary>
        /// 凝胶丘瓣参数表:(横偏, 相位, 频率倍率, 尺寸px, 亮层)。
        /// 底层三瓣暗胶承体,上层两瓣亮胶,相位频率各不相同=分层摆动
        /// </summary>
        private static readonly (float dx, float phase, float freq, float size, bool bright)[] gelLobes = [
            (-7f, 0.0f, 1.00f, 17f, false),
            (0f, 2.1f, 0.83f, 20f, false),
            (7f, 4.2f, 1.13f, 17f, false),
            (-3.5f, 1.2f, 1.27f, 13f, true),
            (3.5f, 3.6f, 0.91f, 13f, true),
        ];

        #endregion

        #region 属性

        internal bool ProduceHasSpace {
            get {
                foreach (Item item in Produce) {
                    if (item == null || item.IsAir) {
                        return true;
                    }
                    if (item.type == ItemID.Gel && item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion

        public override void SetBattery() {
            EnsureSlots();
        }

        public override void Initialize() {
            EnsureSlots();
        }

        private void EnsureSlots() {
            Produce ??= new Item[ProduceSlotCount];
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] ??= new Item();
            }
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            data.Write(WaterStored);
            data.Write(BrewProgress);
            data.Write(brewRevision);
            for (int i = 0; i < ProduceSlotCount; i++) {
                ItemIO.Send(Produce[i] ?? new Item(), data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            EnsureSlots();
            Enabled = reader.ReadBoolean();
            WaterStored = reader.ReadInt32();
            BrewProgress = reader.ReadSingle();
            byte newRevision = reader.ReadByte();
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] = ItemIO.Receive(reader, true);
            }

            //修订号推进才播培养演出,入世快照不播
            if (!TileProcessorNetWork.InitializeWorld && newRevision != brewRevision) {
                PlayBrewEffect();
            }
            brewRevision = newRevision;
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_Enabled"] = Enabled;
                tag["_WaterStored"] = WaterStored;
                tag["_BrewProgress"] = BrewProgress;
                List<TagCompound> produceTags = [];
                for (int i = 0; i < ProduceSlotCount; i++) {
                    produceTags.Add(ItemIO.Save(Produce[i] ?? new Item()));
                }
                tag["_Produce"] = produceTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"SlimeVatTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_WaterStored", out int water)) {
                    WaterStored = Math.Clamp(water, 0, WaterCapacity);
                }
                if (tag.TryGet("_BrewProgress", out float progress)) {
                    BrewProgress = Math.Clamp(progress, 0f, CycleTicks);
                }
                if (tag.TryGet("_Produce", out List<TagCompound> produceTags)) {
                    for (int i = 0; i < ProduceSlotCount && i < produceTags.Count; i++) {
                        Produce[i] = CWRSaveData.LoadItemTag(produceTags[i], $"{nameof(SlimeVatTP)}:_Produce");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"SlimeVatTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位/水量被UI/管道改动后调用:权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 汲水

        /// <summary>
        /// 自动汲取机身邻接与下方的世界水体:一拍抽一格,液体清除是世界改动,
        /// 权威端主线程执行后走原版液体同步
        /// </summary>
        private void TryPumpWater() {
            if (WaterStored > WaterCapacity - 255) {
                return;
            }

            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            int left = Position.X - PumpExpand;
            int right = Position.X + tileWidth + PumpExpand - 1;
            int top = Position.Y;
            int bottom = Position.Y + tileHeight + PumpDepth - 1;

            //先扫后抽:扫描只读,并行阶段安全;实际清水进主线程闭包
            Point16 found = Point16.Zero;
            for (int y = top; y <= bottom && found == Point16.Zero; y++) {
                for (int x = left; x <= right; x++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.LiquidAmount <= 0 || tile.LiquidType != LiquidID.Water) {
                        continue;
                    }
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        continue;
                    }
                    found = new Point16(x, y);
                    break;
                }
            }

            if (found == Point16.Zero) {
                return;
            }

            Defer(() => {
                if (WaterStored > WaterCapacity - 255) {
                    return;
                }
                Tile tile = Main.tile[found.X, found.Y];
                if (tile.LiquidAmount <= 0 || tile.LiquidType != LiquidID.Water) {
                    return;
                }

                WaterStored = Math.Min(WaterCapacity, WaterStored + tile.LiquidAmount);
                tile.LiquidAmount = 0;
                WorldGen.SquareTileFrame(found.X, found.Y, false);
                if (VaultUtils.isServer) {
                    NetMessage.sendWater(found.X, found.Y);
                }
                netDirty = true;
            });
        }

        /// <summary>UI 倒水桶:普通水桶+255并退还空桶,无底水桶白给;客户端权威编辑,调用方负责推送</summary>
        internal bool TryPourBucket(Item bucket) {
            if (bucket == null || bucket.IsAir || WaterStored > WaterCapacity - 255) {
                return false;
            }

            if (bucket.type == ItemID.BottomlessBucket) {
                WaterStored = Math.Min(WaterCapacity, WaterStored + 255);
                return true;
            }

            if (bucket.type == ItemID.WaterBucket) {
                bucket.stack--;
                if (bucket.stack <= 0) {
                    bucket.TurnToAir();
                }
                Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), new Item(ItemID.EmptyBucket));
                WaterStored = Math.Min(WaterCapacity, WaterStored + 255);
                return true;
            }

            return false;
        }

        #endregion

        #region 更新逻辑

        public override void UpdateMachine() {
            //权威端节流刷新
            if (netCooldown > 0) {
                netCooldown--;
            }
            if (netDirty && netCooldown <= 0 && VaultUtils.isServer) {
                netDirty = false;
                netCooldown = NetInterval;
                SendData();
            }
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            //MP 客户端上 IsWorking 不入包恒为 false,机身辉光改由派生的视觉作业强度驱动
            bool glowWorking = VaultUtils.isClient ? visualWork > 0.5f : IsWorking;
            GlowIntensity = glowWorking
                ? Math.Min(1f, GlowIntensity + 0.04f)
                : Math.Max(0f, GlowIntensity - 0.02f);

            //客户端视觉:凝胶丘/气泡/汲水水流,状态全部由已同步数据派生,零网络
            if (!VaultUtils.isServer) {
                UpdateClientVisual();
            }

            if (!Enabled) {
                IsWorking = false;
                return;
            }

            bool authority = !VaultUtils.isClient;
            if (!authority) {
                return;
            }

            //自动汲水
            if (++pumpTimer >= PumpInterval) {
                pumpTimer = 0;
                TryPumpWater();
            }

            //原料齐备才推进培养进度:缺水/缺电/满仓时培养液休眠
            bool canWork = WaterStored >= WaterCost && ProduceHasSpace && MachineData.UEvalue >= BrewCost;
            if (!canWork) {
                IsWorking = false;
                if (WaterStored < WaterCost) {
                    Prompt(SlimeVat.NoWaterText.Value);
                }
                else if (!ProduceHasSpace) {
                    Prompt(SlimeVat.FullText.Value);
                }
                else {
                    Prompt(SlimeVat.NoEnergyText.Value);
                }
                return;
            }

            IsWorking = true;
            BrewProgress += 1f;
            if (BrewProgress < CycleTicks) {
                return;
            }

            BrewProgress = 0f;
            netDirty = true;

            //结算在主线程做,与UI编辑无竞争
            Defer(() => {
                if (MachineData.UEvalue < BrewCost || WaterStored < WaterCost || !ProduceHasSpace) {
                    return;
                }

                WaterStored -= WaterCost;
                MachineData.UEvalue -= BrewCost;

                int remain = InsertItem(Produce, ItemID.Gel, GelPerCycle);
                if (remain > 0) {
                    DropItem(new Item(ItemID.Gel, remain));
                }

                brewRevision++;
                netDirty = true;

                PlayBrewEffect();
            });
        }

        private static int InsertItem(Item[] slots, int itemType, int count) {
            //先叠同类
            foreach (Item slot in slots) {
                if (count <= 0) {
                    return 0;
                }
                if (slot == null || slot.IsAir || slot.type != itemType || slot.stack >= slot.maxStack) {
                    continue;
                }
                int add = Math.Min(count, slot.maxStack - slot.stack);
                slot.stack += add;
                count -= add;
            }
            //再开新槽
            for (int i = 0; i < slots.Length && count > 0; i++) {
                if (slots[i] != null && !slots[i].IsAir) {
                    continue;
                }
                slots[i] = new Item(itemType, count);
                count = 0;
            }
            return count;
        }

        private void Prompt(string text) {
            if (textIdleTime > 0) {
                return;
            }
            textIdleTime = 300;
            //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
            Defer(() => CombatText.NewText(HitBox, SlimeVat.Tint, text));
        }

        /// <summary>培养演出:凝胶丘弹性挤压回弹,弹出几团活凝胶+飞沫;主线程调用,服务器跳过</summary>
        internal void PlayBrewEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            //挤出拍:凝胶丘猛压扁再过冲回弹,产出是"从胶体里挤出来"的
            squashTimer = 16f;

            Vector2 mouth = DomeCenter + new Vector2(0f, -6f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_FarmGelGlob>(mouth + Main.rand.NextVector2Circular(6f, 3f),
                    new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(2f, 3.6f)),
                    new Color(96, 216, 130), Main.rand.NextFloat(0.8f, 1.3f)).Configure(Main.rand.Next(50, 75));
            }
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(10f, 5f),
                    DustID.t_Slime, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.8f, 2.6f)),
                    120, new Color(78, 200, 120), 1.1f);
                dust.noGravity = false;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.35f, Pitch = 0.5f }, mouth);
        }

        #endregion

        #region 客户端视觉

        /// <summary>
        /// 客户端视觉总更新:凝胶丘生命周期,气泡,汲水水流。
        /// 运行在并行更新阶段:粒子生成经 Defer,随机数走 Rand
        /// </summary>
        private void UpdateClientVisual() {
            //派生作业状态:水/仓/电全部随全量包同步
            bool canWork = Enabled && WaterStored >= WaterCost && ProduceHasSpace && MachineData.UEvalue >= BrewCost;
            visualWork = MathHelper.Lerp(visualWork, canWork ? 1f : 0f, 0.04f);

            //蠕动速率:活性凝胶作业时活跃,休眠时也保留一点极缓的余动(活物不彻底死掉)
            gelPhase += 0.022f + visualWork * 0.05f;

            //丘高跟培养进度走:一轮周期里从矮丘长成饱满胶体,产出后随进度归零瘪回去
            float progressFrac = MathHelper.Clamp(BrewProgress / CycleTicks, 0f, 1f);
            float targetHeight = WaterStored <= 0 && !canWork ? 0.16f : 0.34f + progressFrac * 0.66f;
            gelHeight = MathHelper.Lerp(gelHeight, targetHeight, 0.02f);

            if (squashTimer > 0f) {
                squashTimer--;
            }

            //气泡:作业中从丘底升起,越接近产出越勤
            if (canWork && InScreen && --bubbleTimer <= 0) {
                bubbleTimer = Rand.Next(34, 70) - (int)(progressFrac * 20f);
                Vector2 spawn = DomeCenter + new Vector2(Rand.NextFloat(-8f, 8f), 1f);
                float burstY = DomeCenter.Y - 3f - gelHeight * 7f;
                Defer(() => PRTLoader.NewParticle<PRT_FarmGelBubble>(spawn, new Vector2(0f, -0.32f),
                    new Color(170, 255, 200), Rand.NextFloat(0.5f, 1f)).Configure(burstY));
            }

            //汲水检测:同步来的水量上涨=有水正进来,侧口放一段入水流
            if (lastWaterSeen >= 0 && WaterStored > lastWaterSeen) {
                intakeTimer = 40;
            }
            lastWaterSeen = WaterStored;

            if (intakeTimer > 0) {
                intakeTimer--;
                if (InScreen && (intakeTimer & 1) == 0) {
                    Vector2 port = PosInWorld + new Vector2(Width + 1f, Height * 0.25f);
                    Vector2 vel = new(-Rand.NextFloat(0.6f, 1.1f), Rand.NextFloat(0.8f, 1.5f));
                    float dustScale = Rand.NextFloat(0.8f, 1.2f);
                    Defer(() => {
                        Dust dust = Dust.NewDustPerfect(port, DustID.Water, vel, 60, default, dustScale);
                        dust.noGravity = false;
                    });
                }
            }
        }

        /// <summary>
        /// 槽顶凝胶体:多瓣两层错相形变拼出的活凝胶。
        /// 每瓣独立相位做反相压扁伸展(体积守恒的果冻感)+竖向浮沉,
        /// 丘高随培养进度生长,产出拍整体压扁过冲回弹。
        /// 刻意避开"单张贴图 sin 平移":贴图不平移,是分层摆动+形变+生命周期
        /// </summary>
        private void DrawGelDome(SpriteBatch spriteBatch) {
            if (gelHeight <= 0.05f || Fog == null) {
                return;
            }
            Texture2D tex = Fog.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 basePos = DomeCenter;

            //环境光与凝胶自发光混合:培养液有微弱生物荧光,暗处不至于全黑
            Color light = Lighting.GetColor(basePos.ToTileCoordinates());
            float lightLum = (light.R + light.G + light.B) / 765f;
            float selfGlow = 0.35f + 0.65f * lightLum;

            //产出拍包络:先压扁后过冲回弹,指数衰减的弹性
            float squashX = 1f, squashY = 1f;
            if (squashTimer > 0f) {
                float st = (16f - squashTimer) / 16f;
                float wave = MathF.Sin(st * MathHelper.TwoPi * 1.1f) * MathF.Exp(-st * 2.8f);
                squashX = 1f + wave * 0.4f;
                squashY = 1f - wave * 0.45f;
            }

            float wob = 0.45f + visualWork * 0.55f;
            float heightPx = (3f + gelHeight * 8f) * squashY;

            //Fog 的 alpha 密度偏稀(实测约旧烟图 0.32 倍),胶体要读得出体积,乘数取高位
            Color darkGel = new Color(24, 88, 48) * (0.92f * selfGlow);
            Color brightGel = new Color(96, 216, 130) * (0.62f * selfGlow);

            //接触底座:一坨压扁的暗胶给整座丘一个可读剪影锚点
            float baseW = (26f + gelHeight * 6f) / tex.Width;
            spriteBatch.Draw(tex, basePos + new Vector2(0f, -1f) - Main.screenPosition, null,
                new Color(16, 62, 34) * (0.85f * selfGlow), 0.35f, origin,
                new Vector2(baseW * squashX, baseW * 0.38f * squashY), SpriteEffects.None, 0f);

            for (int i = 0; i < gelLobes.Length; i++) {
                (float dx, float phase, float freq, float size, bool bright) = gelLobes[i];
                float lp = gelPhase * freq + phase;
                float bob = MathF.Sin(lp) * 1.7f * wob;
                //反相压扁伸展:横胀则纵缩,读作有体积的果冻在呼吸
                float sx = (1f + MathF.Sin(lp + 1.3f) * 0.10f * wob) * squashX;
                float sy = (1f - MathF.Sin(lp + 1.3f) * 0.11f * wob) * squashY;
                float baseScale = size * (0.55f + gelHeight * 0.45f) / tex.Width;
                Vector2 pos = basePos + new Vector2(dx * squashX, -heightPx * (bright ? 0.7f : 0.45f) + bob);
                //瓣朝向固定错开,镜像交替,不读成同一张贴纸
                float rot = phase * 0.5f;
                SpriteEffects fxs = (i & 1) == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                spriteBatch.Draw(tex, pos - Main.screenPosition, null, bright ? brightGel : darkGel,
                    rot, origin, new Vector2(baseScale * sx, baseScale * sy * 0.72f), fxs, 0f);
            }

            //表面湿亮高光:一点 A=0 加色亮斑沿丘顶独立摆动
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Vector2 glintPos = basePos + new Vector2(MathF.Sin(gelPhase * 0.66f) * 6f * wob, -heightPx - 1f);
            spriteBatch.Draw(glowTex, glintPos - Main.screenPosition, null,
                new Color(190, 255, 215, 0) * (0.4f + 0.2f * visualWork), 0f, glowTex.Size() * 0.5f,
                new Vector2(0.16f, 0.09f), SpriteEffects.None, 0f);
        }

        /// <summary>状态灯:与既有"缺电贴图变暗"互补的原因编码</summary>
        private void DrawStatusLamp(SpriteBatch spriteBatch) {
            FarmLampState state;
            if (Disabled || !Enabled) {
                state = FarmLampState.Off;
            }
            else if (MachineData.UEvalue < BrewCost) {
                state = FarmLampState.NoPower;
            }
            else if (WaterStored < WaterCost || !ProduceHasSpace) {
                state = FarmLampState.MissingResource;
            }
            else {
                state = FarmLampState.Working;
            }
            FarmStatusLamp.Draw(spriteBatch, PosInWorld + new Vector2(Width - 5f, 5f), state, SlimeVat.Tint, WhoAmI);
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<SlimeVatUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //倒出全部产出;缓冲里的水随拆机流失(与液体储罐的 v1 取舍一致)
            for (int i = 0; i < ProduceSlotCount; i++) {
                if (Produce[i] != null && !Produce[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Produce[i]);
                    Produce[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item vatItem = new Item(ModContent.ItemType<SlimeVat>());
            vatItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, vatItem);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            DrawGelDome(spriteBatch);
            DrawStatusLamp(spriteBatch);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        #endregion
    }
}
