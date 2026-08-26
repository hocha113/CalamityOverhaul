using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Apiaries
{
    /// <summary>
    /// 养蜂箱TP:消耗空玻璃瓶与电力,周期性灌装蜂蜜瓶。<br/>
    /// 邻近蜂蜜液体或身处丛林时蜂群更活跃,产率x1.5(环境检查节流缓存)。<br/>
    /// 结算仅权威端执行(主线程经 Defer),灌装演出经修订号搭全量包广播
    /// </summary>
    internal class ApiaryTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ApiaryTile>();
        public override int TargetItem => ModContent.ItemType<Apiary>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 300;
        /// <summary>全量包携带6格物品数据,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量

        internal const int BottleSlotCount = 2;
        internal const int ProduceSlotCount = 4;
        /// <summary>每瓶蜂蜜的电力开销</summary>
        internal const float BrewCost = 5f;
        /// <summary>基础灌装周期(tick),60秒一瓶</summary>
        internal const int CycleTicks = 3600;
        /// <summary>环境加成下的进度倍率</summary>
        internal const float EnvRateBonus = 1.5f;
        private const int EnvCheckInterval = 300;
        //蜂蜜液体的邻接探测外扩(格)
        private const int HoneyProbeExpand = 3;
        //丛林判定的采样半径(格)与达标数
        private const int JungleProbeRadius = 25;
        private const int JungleTileThreshold = 4;
        //账本合批同步节流
        private const int NetInterval = 30;

        #endregion

        #region 字段

        internal Item[] Bottles = new Item[BottleSlotCount];
        internal Item[] Produce = new Item[ProduceSlotCount];
        internal bool Enabled = true;

        /// <summary>灌装进度(tick 计),UI 进度条用</summary>
        internal float BrewProgress;
        /// <summary>环境加成生效中(邻蜜或丛林)</summary>
        internal bool EnvBonus;

        internal bool IsWorking { get; private set; }
        internal float GlowIntensity;

        private int envCheckTimer;
        private int textIdleTime;
        private byte brewRevision;
        private bool netDirty;
        private int netCooldown;

        #endregion

        #region 客户端视觉字段(蜂群/蜜滴,不入存档不入网络包)

        /// <summary>单只环绕蜂:李萨如双频轨道+高频抖振+徘徊锚点,轨道永不闭合成圆规圆</summary>
        private struct BeeSim
        {
            /// <summary>个体时间,忙碌度控制推进速度</summary>
            public float T;
            /// <summary>0~1 出巢程度,0=在巢里,渐变期间蜂向箱口收拢</summary>
            public float Presence;
            public Vector2 Anchor;
            public Vector2 NextAnchor;
            public float AnchorLerp;
            //两对近似不可通约的频率,叠出不重复的空间轨迹
            public float FreqA, FreqB, FreqC, FreqD;
            public float AmpX, AmpY;
            public float JitterSeed;
            public int ReseedTimer;
            public Vector2 LastPos;
            public Vector2 DrawPos;
        }

        private readonly BeeSim[] bees = new BeeSim[4];
        private bool beesInit;
        /// <summary>蜂群忙碌度 0~1:作业加速,环境加成更忙,产出拍短暂兴奋</summary>
        private float beeBusy;
        /// <summary>派生作业强度,由已同步字段推出,MP 客户端也成立</summary>
        private float visualWork;
        private int dripTimer;
        /// <summary>产出拍瓶口蜜光剩余帧</summary>
        private int brewFlashTimer;

        #endregion

        #region 属性

        internal bool HasBottle {
            get {
                foreach (Item item in Bottles) {
                    if (item != null && !item.IsAir && item.type == ItemID.Bottle) {
                        return true;
                    }
                }
                return false;
            }
        }

        internal bool ProduceHasSpace {
            get {
                foreach (Item item in Produce) {
                    if (item == null || item.IsAir) {
                        return true;
                    }
                    if (item.type == ItemID.BottledHoney && item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        internal static bool IsEmptyBottle(Item item) => item != null && !item.IsAir && item.type == ItemID.Bottle;

        #endregion

        public override void SetBattery() {
            EnsureSlots();
        }

        public override void Initialize() {
            EnsureSlots();
        }

        private void EnsureSlots() {
            Bottles ??= new Item[BottleSlotCount];
            Produce ??= new Item[ProduceSlotCount];
            for (int i = 0; i < BottleSlotCount; i++) {
                Bottles[i] ??= new Item();
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] ??= new Item();
            }
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            data.Write(BrewProgress);
            data.Write(EnvBonus);
            data.Write(brewRevision);
            for (int i = 0; i < BottleSlotCount; i++) {
                ItemIO.Send(Bottles[i] ?? new Item(), data, true);
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                ItemIO.Send(Produce[i] ?? new Item(), data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            EnsureSlots();
            Enabled = reader.ReadBoolean();
            BrewProgress = reader.ReadSingle();
            EnvBonus = reader.ReadBoolean();
            byte newRevision = reader.ReadByte();
            for (int i = 0; i < BottleSlotCount; i++) {
                Bottles[i] = ItemIO.Receive(reader, true);
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] = ItemIO.Receive(reader, true);
            }

            //修订号推进才播灌装演出,入世快照不播
            if (!TileProcessorNetWork.InitializeWorld && newRevision != brewRevision) {
                PlayBrewEffect();
            }
            brewRevision = newRevision;
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_Enabled"] = Enabled;
                tag["_BrewProgress"] = BrewProgress;
                List<TagCompound> bottleTags = [];
                for (int i = 0; i < BottleSlotCount; i++) {
                    bottleTags.Add(ItemIO.Save(Bottles[i] ?? new Item()));
                }
                tag["_Bottles"] = bottleTags;
                List<TagCompound> produceTags = [];
                for (int i = 0; i < ProduceSlotCount; i++) {
                    produceTags.Add(ItemIO.Save(Produce[i] ?? new Item()));
                }
                tag["_Produce"] = produceTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"ApiaryTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_BrewProgress", out float progress)) {
                    BrewProgress = Math.Clamp(progress, 0f, CycleTicks);
                }
                if (tag.TryGet("_Bottles", out List<TagCompound> bottleTags)) {
                    for (int i = 0; i < BottleSlotCount && i < bottleTags.Count; i++) {
                        Bottles[i] = CWRSaveData.LoadItemTag(bottleTags[i], $"{nameof(ApiaryTP)}:_Bottles");
                    }
                }
                if (tag.TryGet("_Produce", out List<TagCompound> produceTags)) {
                    for (int i = 0; i < ProduceSlotCount && i < produceTags.Count; i++) {
                        Produce[i] = CWRSaveData.LoadItemTag(produceTags[i], $"{nameof(ApiaryTP)}:_Produce");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"ApiaryTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位被UI/管道改动后调用:权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 环境检查

        /// <summary>
        /// 邻蜜或丛林判定,只读物块,并行阶段安全。
        /// 蜂蜜:机身外扩数格内任一蜂蜜液体格;丛林:采样半径内丛林草达标
        /// </summary>
        private void CheckEnvironment() {
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;

            //蜂蜜液体邻接
            for (int x = Position.X - HoneyProbeExpand; x <= Position.X + tileWidth + HoneyProbeExpand; x++) {
                for (int y = Position.Y - HoneyProbeExpand; y <= Position.Y + tileHeight + HoneyProbeExpand; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Honey) {
                        EnvBonus = true;
                        return;
                    }
                }
            }

            //丛林草采样
            int centerX = Position.X + tileWidth / 2;
            int centerY = Position.Y + tileHeight / 2;
            int jungleCount = 0;
            for (int x = centerX - JungleProbeRadius; x <= centerX + JungleProbeRadius; x += 2) {
                for (int y = centerY - JungleProbeRadius; y <= centerY + JungleProbeRadius; y += 2) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.JungleGrass) {
                        if (++jungleCount >= JungleTileThreshold) {
                            EnvBonus = true;
                            return;
                        }
                    }
                }
            }

            EnvBonus = false;
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

            //客户端视觉:蜂群与蜜滴,状态全部由已同步数据派生,零网络
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

            //环境加成节流复查
            if (++envCheckTimer >= EnvCheckInterval) {
                envCheckTimer = 0;
                bool old = EnvBonus;
                CheckEnvironment();
                if (old != EnvBonus) {
                    netDirty = true;
                }
            }

            //原料齐备才推进酿造进度:缺瓶/缺电/满仓时蜂群歇工
            bool canWork = HasBottle && ProduceHasSpace && MachineData.UEvalue >= BrewCost;
            if (!canWork) {
                IsWorking = false;
                if (!HasBottle) {
                    Prompt(Apiary.NoBottleText.Value);
                }
                else if (!ProduceHasSpace) {
                    Prompt(Apiary.FullText.Value);
                }
                else {
                    Prompt(Apiary.NoEnergyText.Value);
                }
                return;
            }

            IsWorking = true;
            BrewProgress += EnvBonus ? EnvRateBonus : 1f;
            if (BrewProgress < CycleTicks) {
                return;
            }

            BrewProgress = 0f;
            netDirty = true;

            //结算在主线程做,与UI编辑无竞争
            Defer(() => {
                if (MachineData.UEvalue < BrewCost || !ProduceHasSpace) {
                    return;
                }

                //消耗一只空瓶
                bool consumed = false;
                for (int i = 0; i < BottleSlotCount; i++) {
                    Item bottle = Bottles[i];
                    if (!IsEmptyBottle(bottle)) {
                        continue;
                    }
                    bottle.stack--;
                    if (bottle.stack <= 0) {
                        bottle.TurnToAir();
                    }
                    consumed = true;
                    break;
                }
                if (!consumed) {
                    return;
                }

                int remain = InsertItem(Produce, ItemID.BottledHoney, 1);
                if (remain > 0) {
                    DropItem(new Item(ItemID.BottledHoney, remain));
                }

                MachineData.UEvalue -= BrewCost;
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
            Defer(() => CombatText.NewText(HitBox, Apiary.Tint, text));
        }

        /// <summary>灌装演出:瓶口蜜光+高光蜜滴上抛回落+原版蜂蜜尘打底;主线程调用,服务器跳过</summary>
        internal void PlayBrewEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            //瓶口蜜光与蜂群兴奋共用这个拍
            brewFlashTimer = 18;

            Vector2 spout = CenterInWorld + BeeMouthOffset;
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_SHPCHoneyDrop>(spout + Main.rand.NextVector2Circular(6f, 3f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(1.2f, 3.2f)),
                    new Color(255, 205, 95), Main.rand.NextFloat(0.9f, 1.5f)).Configure(Main.rand.Next(30, 48));
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(spout + Main.rand.NextVector2Circular(8f, 4f),
                    DustID.Honey, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.8f)),
                    60, default, 1.1f);
                dust.noGravity = false;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = 0.3f }, spout);
        }

        #endregion

        #region 客户端视觉

        /// <summary>
        /// 客户端视觉总更新:派生作业状态,推进蜂群模拟,渗蜜滴。
        /// 运行在并行更新阶段:粒子生成经 Defer,随机数走 Rand
        /// </summary>
        private void UpdateClientVisual() {
            //派生作业状态:瓶/仓/电全部随全量包同步,MP 客户端也能推出正确结果
            bool canWork = Enabled && HasBottle && ProduceHasSpace && MachineData.UEvalue >= BrewCost;
            visualWork = MathHelper.Lerp(visualWork, canWork ? 1f : 0f, 0.04f);

            float busyTarget = canWork ? (EnvBonus ? 1f : 0.7f) : 0f;
            if (brewFlashTimer > 0) {
                brewFlashTimer--;
                busyTarget = 1f;    //产出拍蜂群短暂兴奋
            }
            beeBusy = MathHelper.Lerp(beeBusy, busyTarget, 0.05f);

            if (!beesInit) {
                beesInit = true;
                for (int i = 0; i < bees.Length; i++) {
                    InitBee(ref bees[i]);
                    bees[i].Presence = 0f;
                }
            }

            //蜂数:环境加成时倾巢而出,歇工时逐只归巢
            int flyCount = !canWork ? 0 : (EnvBonus ? 4 : 3);
            for (int i = 0; i < bees.Length; i++) {
                UpdateBee(ref bees[i], i < flyCount);
            }

            //蜜滴渗出:作业中箱底缝隙偶发琥珀高光滴珠(复用 SHPC 蜜滴粒子)
            if (canWork && InScreen && --dripTimer <= 0) {
                dripTimer = Rand.Next(80, 150);
                Vector2 seep = PosInWorld + new Vector2(Rand.NextFloat(5f, Width - 5f), Height - 4f);
                Defer(() => PRTLoader.NewParticle<PRT_SHPCHoneyDrop>(seep, new Vector2(0f, 0.3f),
                    new Color(255, 200, 90), Rand.NextFloat(0.8f, 1.25f)).Configure(Rand.Next(26, 40)));
            }
        }

        /// <summary>箱口(顶面中心)相对箱心的偏移:蜂群出入巢与产出蜜光共用</summary>
        private Vector2 BeeMouthOffset => new(0f, -Height * 0.5f + 3f);

        private Vector2 RandomBeeAnchor() => new(Rand.NextFloat(-20f, 20f), Rand.NextFloat(-22f, 6f));

        private void InitBee(ref BeeSim bee) {
            bee.T = Rand.NextFloat(100f);
            bee.Anchor = RandomBeeAnchor();
            bee.NextAnchor = RandomBeeAnchor();
            bee.AnchorLerp = Rand.NextFloat();
            //频率对刻意取非整数倍,轨道不闭合、不重复、不成圆
            bee.FreqA = Rand.NextFloat(0.043f, 0.075f);
            bee.FreqB = bee.FreqA * Rand.NextFloat(1.53f, 1.97f);
            bee.FreqC = Rand.NextFloat(0.037f, 0.066f);
            bee.FreqD = bee.FreqC * Rand.NextFloat(1.31f, 1.83f);
            bee.AmpX = Rand.NextFloat(14f, 26f);
            bee.AmpY = Rand.NextFloat(8f, 15f);
            bee.JitterSeed = Rand.NextFloat(MathHelper.TwoPi);
            bee.ReseedTimer = Rand.Next(150, 320);
            bee.DrawPos = bee.LastPos = CenterInWorld + BeeMouthOffset;
        }

        private void UpdateBee(ref BeeSim bee, bool shouldFly) {
            if (bee.Presence <= 0f) {
                if (!shouldFly) {
                    return;
                }
                //出巢:重洗轨道,从箱口飞出
                InitBee(ref bee);
                bee.Presence = 0.01f;
            }

            bee.T += 0.55f + beeBusy * 1.05f;
            bee.Presence = MathHelper.Clamp(bee.Presence + (shouldFly ? 0.016f : -0.014f), 0f, 1f);

            //徘徊锚点缓移:蜂换工位,不永远绕同一个中心打转
            if (--bee.ReseedTimer <= 0) {
                bee.Anchor = Vector2.SmoothStep(bee.Anchor, bee.NextAnchor, bee.AnchorLerp);
                bee.NextAnchor = RandomBeeAnchor();
                bee.AnchorLerp = 0f;
                bee.ReseedTimer = Rand.Next(150, 320);
            }
            bee.AnchorLerp = MathF.Min(bee.AnchorLerp + 0.008f, 1f);
            Vector2 anchor = Vector2.SmoothStep(bee.Anchor, bee.NextAnchor, bee.AnchorLerp);

            //李萨如双频叠加+高频小抖振(忙碌时抖得更凶),这是"活蜂"和"绕圈贴图"的分界
            Vector2 orbit = new(
                MathF.Sin(bee.T * bee.FreqA) * bee.AmpX + MathF.Sin(bee.T * bee.FreqB) * bee.AmpX * 0.35f,
                MathF.Sin(bee.T * bee.FreqC) * bee.AmpY + MathF.Cos(bee.T * bee.FreqD) * bee.AmpY * 0.4f);
            float jitterAmp = 0.7f + beeBusy * 1.3f;
            Vector2 jitter = new(
                MathF.Sin(bee.T * 0.9f + bee.JitterSeed) * jitterAmp,
                MathF.Cos(bee.T * 1.17f + bee.JitterSeed * 2f) * jitterAmp * 0.8f);

            //presence 低时向箱口收拢:出巢从箱口涌出,归巢钻回箱口
            float outFrac = MathF.Pow(bee.Presence, 0.8f);
            bee.LastPos = bee.DrawPos;
            bee.DrawPos = CenterInWorld + Vector2.Lerp(BeeMouthOffset, anchor + orbit + jitter, outFrac);
        }

        /// <summary>环绕蜂群:原版蜜蜂弹幕贴图与飞行帧序,朝向与倾斜随飞行方向</summary>
        private void DrawBees(SpriteBatch spriteBatch) {
            if (!beesInit) {
                return;
            }
            Main.instance.LoadProjectile(ProjectileID.Bee);
            Texture2D beeTex = TextureAssets.Projectile[ProjectileID.Bee].Value;
            int frameCount = Main.projFrames[ProjectileID.Bee];
            Color light = Lighting.GetColor(CenterInWorld.ToTileCoordinates());

            for (int i = 0; i < bees.Length; i++) {
                BeeSim bee = bees[i];
                if (bee.Presence <= 0.02f) {
                    continue;
                }
                //翼拍循环走前 3 帧,与原版飞行帧序一致
                int frame = (int)(bee.T * 0.6f + i * 1.7f) % 3;
                Rectangle src = beeTex.Frame(1, frameCount, 0, frame);
                Vector2 motion = bee.DrawPos - bee.LastPos;
                //原版约定:贴图原生朝右,向左飞水平翻转;身体随横速轻微倾斜
                SpriteEffects fxs = motion.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                float rot = MathHelper.Clamp(motion.X * 0.16f, -0.4f, 0.4f);
                float scale = 0.74f * (0.6f + 0.4f * bee.Presence);
                spriteBatch.Draw(beeTex, bee.DrawPos - Main.screenPosition, src, light * bee.Presence,
                    rot, src.Size() * 0.5f, scale, fxs, 0f);
            }
        }

        /// <summary>产出拍瓶口蜜光:金橙暖辉快起慢落,首拍带一记白芯过曝(≤3帧)</summary>
        private void DrawBrewFlash(SpriteBatch spriteBatch) {
            if (brewFlashTimer <= 0) {
                return;
            }
            float p = brewFlashTimer / 18f;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = CenterInWorld + BeeMouthOffset - Main.screenPosition;
            Vector2 origin = glowTex.Size() * 0.5f;
            spriteBatch.Draw(glowTex, drawPos, null, new Color(255, 170, 60, 0) * (p * 0.5f), 0f, origin, 0.55f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glowTex, drawPos, null, new Color(255, 225, 140, 0) * (p * 0.8f), 0f, origin, 0.26f, SpriteEffects.None, 0f);
            if (brewFlashTimer > 15) {
                spriteBatch.Draw(glowTex, drawPos, null, new Color(255, 255, 255, 0) * 0.7f, 0f, origin, 0.14f, SpriteEffects.None, 0f);
            }
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
            else if (!HasBottle || !ProduceHasSpace) {
                state = FarmLampState.MissingResource;
            }
            else {
                state = FarmLampState.Working;
            }
            FarmStatusLamp.Draw(spriteBatch, PosInWorld + new Vector2(Width - 5f, 5f), state, Apiary.Tint, WhoAmI);
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<ApiaryUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //倒出全部空瓶与产出
            for (int i = 0; i < BottleSlotCount; i++) {
                if (Bottles[i] != null && !Bottles[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Bottles[i]);
                    Bottles[i] = new Item();
                }
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                if (Produce[i] != null && !Produce[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Produce[i]);
                    Produce[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item apiaryItem = new Item(ModContent.ItemType<Apiary>());
            apiaryItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, apiaryItem);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //待机冻结时不画悬空定格的蜂
            if (!Disabled) {
                DrawBees(spriteBatch);
                DrawBrewFlash(spriteBatch);
            }
            DrawStatusLamp(spriteBatch);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        #endregion
    }
}
