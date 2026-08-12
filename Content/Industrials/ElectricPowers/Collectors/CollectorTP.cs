using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.Actors;
using InnoVault.Storages;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors
{
    /// <summary>收集器投放策略</summary>
    internal enum CollectorStorageMode : byte
    {
        /// <summary>就近，范围内最近容器</summary>
        Auto = 0,
        /// <summary>绑定优先，失败回退就近</summary>
        BoundFirst = 1,
        /// <summary>仅限绑定容器</summary>
        BoundOnly = 2
    }

    internal class CollectorTP : BaseBattery, IItemFilterHost
    {
        public override int TargetTileID => ModContent.TileType<CollectorTile>();
        public override int TargetItem => ModContent.ItemType<Collector>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        /// <summary>全量包可能携带整张过滤名单(至多500项)，放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;
        /// <summary>就近模式的存储搜索半径(像素)</summary>
        internal const int StorageSearchRange = 600;
        /// <summary>绑定最远距离(像素)</summary>
        internal const int MaxBindDistance = 2000;
        /// <summary>绑定上限</summary>
        internal const int MaxBindings = 6;
        /// <summary>单次抓取能耗</summary>
        internal const int consumeUE = 8;
        /// <summary>存储候选缓存(帧)</summary>
        private const int StorageCacheTicks = 30;
        /// <summary>全局臂上限</summary>
        private const int GlobalArmLimit = 300;
        public Vector2 ArmPos => CenterInWorld + new Vector2(0, 14);
        private int textIdleTime;
        internal int frame;
        internal bool workState;
        internal bool BatteryPrompt;
        internal ItemFilterSet Filter = new();
        internal int TagItemSign;
        internal int dontSpawnArmTime;
        internal List<int> ArmActorIndices = new List<int>();
        internal float hoverSengs;

        /// <summary>已装过滤卡</summary>
        internal bool FilterInstalled => TagItemSign == ModContent.ItemType<ItemFilter>();

        #region IItemFilterHost

        ItemFilterSet IItemFilterHost.Filter => Filter;
        public string FilterHostName => Lang.GetItemNameValue(TargetItem);
        public bool FilterHostAlive => Active;
        public Vector2? FilterHostWorldCenter => CenterInWorld;
        public void OnFilterChanged() => SendData();
        public bool CanUninstallFilter => true;

        public void UninstallFilter() {
            TagItemSign = ItemID.None;
            Filter.Clear();
            SendData();
        }

        #endregion

        //存储绑定数据，列表顺序即投放优先级
        internal List<Point16> BoundStorages = [];
        internal CollectorStorageMode StorageMode = CollectorStorageMode.Auto;

        //存储候选快照缓存
        private readonly List<IStorageProvider> storageCandidates = [];
        private bool storageCacheDirty = true;
        private uint storageCacheTick;

        /// <summary>上次发送时的名单修改版本，-1=从未发送；仅本端自比较，禁跨网比较。
        /// 机械臂每次抓取都会推送UE扣减，名单可达500项(约2KB)不能逐包搭载</summary>
        private int lastSentFilterRevision = -1;

        public override void SetBattery() {
            Filter = new ItemFilterSet();
            DrawExtendMode = 2200;
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            //名单只在有变化或全量场景(加入世界快照序列化)时搭载
            bool sendFilter = TileProcessorNetWork.InitializeWorld || lastSentFilterRevision != Filter.Revision;
            data.Write(sendFilter);
            if (sendFilter) {
                Filter.Write(data);
                if (!TileProcessorNetWork.InitializeWorld) {
                    lastSentFilterRevision = Filter.Revision;
                }
            }
            data.Write(TagItemSign);
            data.Write(BatteryPrompt);
            data.Write(workState);

            data.Write((byte)StorageMode);
            data.Write((byte)BoundStorages.Count);
            foreach (Point16 pos in BoundStorages) {
                data.Write(pos.X);
                data.Write(pos.Y);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            //名单按线上标志位读取，未搭载则保留当前名单
            if (reader.ReadBoolean()) {
                Filter.Read(reader);
            }
            TagItemSign = reader.ReadInt32();
            BatteryPrompt = reader.ReadBoolean();
            workState = reader.ReadBoolean();

            byte mode = reader.ReadByte();
            StorageMode = mode <= (byte)CollectorStorageMode.BoundOnly
                ? (CollectorStorageMode)mode
                : CollectorStorageMode.Auto;

            int count = reader.ReadByte();
            BoundStorages.Clear();
            for (int i = 0; i < count; i++) {
                Point16 pos = new Point16(reader.ReadInt16(), reader.ReadInt16());
                //上限与去重校验，防御异常数据
                if (BoundStorages.Count < MaxBindings && !BoundStorages.Contains(pos)) {
                    BoundStorages.Add(pos);
                }
            }

            InvalidateStorageCache();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);

            Filter.Save(tag, "_Filter");

            string result = TagItemSign < ItemID.Count
                ? TagItemSign.ToString()
                : ItemLoader.GetItem(TagItemSign).FullName;
            tag["_TagItemFullName"] = result;

            tag["_StorageMode"] = (byte)StorageMode;

            List<int> boundData = new List<int>(BoundStorages.Count * 2);
            foreach (Point16 pos in BoundStorages) {
                boundData.Add(pos.X);
                boundData.Add(pos.Y);
            }
            tag["_BoundStorages"] = boundData;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);

            //新格式优先；旧存档存的是整只过滤卡物品，迁移垫片会把旧名单回填进卡的 ModItem
            if (!Filter.TryLoad(tag, "_Filter")) {
                Item legacyCard = CWRSaveData.LoadItemFromTag(tag, "_ItemFilter", nameof(CollectorTP));
                if (legacyCard.ModItem is ItemFilter card) {
                    Filter.CopyFrom(card.Filter);
                }
            }

            if (tag.TryGet("_TagItemFullName", out string fullName)) {
                TagItemSign = VaultUtils.GetItemTypeFromFullName(fullName);
            }
            else {
                TagItemSign = ItemID.None;
            }

            if (tag.TryGet("_StorageMode", out byte mode) && mode <= (byte)CollectorStorageMode.BoundOnly) {
                StorageMode = (CollectorStorageMode)mode;
            }
            else {
                StorageMode = CollectorStorageMode.Auto;
            }

            BoundStorages.Clear();
            if (tag.TryGet("_BoundStorages", out List<int> boundData)) {
                for (int i = 0; i + 1 < boundData.Count; i += 2) {
                    Point16 pos = new Point16(boundData[i], boundData[i + 1]);
                    if (BoundStorages.Count < MaxBindings && !BoundStorages.Contains(pos)) {
                        BoundStorages.Add(pos);
                    }
                }
            }

            InvalidateStorageCache();
        }

        #endregion

        private void FindFrame() {
            int maxFrame = workState ? 7 : 24;
            if (!workState && frame == 23) {
                frame = 0;
                workState = true;
                if (!VaultUtils.isClient) {
                    SendData();
                }
                Defer(() => SoundEngine.PlaySound(CWRSound.CollectorStart, PosInWorld));
            }
            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        /// <summary>臂是否仍属本收集器(防Actor槽复用)</summary>
        internal bool IsOwnedArmValid(int actorIndex) {
            if (actorIndex < 0 || actorIndex >= ActorLoader.MaxActorCount) {
                return false;
            }
            Actor actor = ActorLoader.Actors[actorIndex];
            return actor != null && actor.Active && actor is CollectorArm arm && arm.collectorPos == Position;
        }

        /// <summary>本机活跃臂数(客户端可读)</summary>
        internal int CountOwnedArms() {
            int count = 0;
            foreach (CollectorArm arm in ActorLoader.GetActiveActors<CollectorArm>()) {
                if (arm.collectorPos == Position) {
                    count++;
                }
            }
            return count;
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            Item item = player.GetItem();

            //空手右键打开控制台
            if (!item.Alives()) {
                if (player.whoAmI == Main.myPlayer) {
                    CollectorUI.Instance?.Initialize(this);
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f, Volume = 0.5f });
                }
                return false;
            }

            //过滤卡右键，装/刷名单
            //TP右键经InnoVault总线在所有端各自执行(卡片名单已随物品NetSend同步)，
            //推送只留权威端一份，客户端不要再用本地状态顶回服务器
            if (item.ModItem is ItemFilter card) {
                TagItemSign = item.type;
                Filter.CopyFrom(card.Filter);

                SoundEngine.PlaySound(CWRSound.Select with { Pitch = -0.2f });
                if (!VaultUtils.isServer) {
                    CombatText.NewText(HitBox, ItemFilterTheme.Gold, ItemFilterEditorUI.InstalledText.Value);
                }

                if (!VaultUtils.isClient) {
                    SendData();
                }
                return false;
            }

            //普通物品右键，设/清单一标记
            if (TagItemSign > ItemID.None && TagItemSign == item.type) {
                TagItemSign = ItemID.None;
            }
            else {
                TagItemSign = item.type;
            }

            SoundEngine.PlaySound(CWRSound.Select with {
                Pitch = TagItemSign > ItemID.None ? -0.2f : 0.2f
            });

            if (!VaultUtils.isClient) {
                SendData();
            }
            return false;
        }

        #region 存储绑定与查找

        internal void InvalidateStorageCache() => storageCacheDirty = true;

        internal bool BindingInRange(Point16 pos)
            => CenterInWorld.Distance(pos.ToWorldCoordinates()) <= MaxBindDistance;

        /// <summary>绑定坐标解析，失效/超距返回null</summary>
        internal IStorageProvider ResolveBinding(Point16 pos) {
            if (!BindingInRange(pos)) {
                return null;
            }
            IStorageProvider provider = StorageLoader.GetStorageTargetByPoint(pos);
            return provider != null && provider.IsValid ? provider : null;
        }

        /// <summary>添加绑定；首次会切到绑定优先</summary>
        internal bool TryAddBinding(Point16 pos) {
            if (BoundStorages.Count >= MaxBindings || BoundStorages.Contains(pos)) {
                return false;
            }
            if (ResolveBinding(pos) == null) {
                return false;
            }
            BoundStorages.Add(pos);
            if (StorageMode == CollectorStorageMode.Auto) {
                StorageMode = CollectorStorageMode.BoundFirst;
            }
            InvalidateStorageCache();
            return true;
        }

        internal void RemoveBindingAt(int index) {
            if (index < 0 || index >= BoundStorages.Count) {
                return;
            }
            BoundStorages.RemoveAt(index);
            InvalidateStorageCache();
        }

        internal void MoveBindingUp(int index) {
            if (index <= 0 || index >= BoundStorages.Count) {
                return;
            }
            (BoundStorages[index - 1], BoundStorages[index]) = (BoundStorages[index], BoundStorages[index - 1]);
            InvalidateStorageCache();
        }

        /// <summary>存储候选(缓存)；绑定优先，非BoundOnly再追加就近</summary>
        internal IReadOnlyList<IStorageProvider> GetStorageCandidates() {
            if (!storageCacheDirty && Main.GameUpdateCount - storageCacheTick < StorageCacheTicks) {
                return storageCandidates;
            }
            storageCacheDirty = false;
            storageCacheTick = Main.GameUpdateCount;
            storageCandidates.Clear();

            //绑定目标按列表顺序最先进入候选
            foreach (Point16 pos in BoundStorages) {
                IStorageProvider provider = ResolveBinding(pos);
                if (provider != null) {
                    storageCandidates.Add(provider);
                }
            }

            //就近搜索补充候选(已按距离排序)，仅绑定模式跳过
            if (StorageMode != CollectorStorageMode.BoundOnly) {
                int autoCount = 0;
                foreach (IStorageProvider provider in StorageLoader.FindAllStorageTargets(Position, StorageSearchRange)) {
                    if (ContainsPosition(storageCandidates, provider.Position)) {
                        continue;
                    }
                    storageCandidates.Add(provider);
                    //箱阵候选上限
                    if (++autoCount >= 24) {
                        break;
                    }
                }
            }

            return storageCandidates;
        }

        private static bool ContainsPosition(List<IStorageProvider> providers, Point16 pos) {
            for (int i = 0; i < providers.Count; i++) {
                if (providers[i].Position == pos) {
                    return true;
                }
            }
            return false;
        }

        internal IStorageProvider FindStorageTarget(Item item) {
            var candidates = GetStorageCandidates();

            if (candidates.Count == 0) {
                PromptNoStorage();
                return null;
            }

            foreach (IStorageProvider provider in candidates) {
                if (provider.IsValid && provider.CanAcceptItem(item)) {
                    return provider;
                }
            }
            return null;
        }

        /// <summary>无存储提示(节流，仅权威端)</summary>
        internal void PromptNoStorage() {
            if (textIdleTime > 0 || VaultUtils.isClient) {
                return;
            }
            textIdleTime = 300;
            BroadcastPrompt(Collector.Text2);

            //生成视觉提示粒子(单人)
            if (Main.netMode != NetmodeID.Server) {
                int range = StorageMode == CollectorStorageMode.BoundOnly ? MaxBindDistance : StorageSearchRange;
                for (int i = 0; i < 220; i++) {
                    Vector2 spwanPos = PosInWorld + VaultUtils.RandVr(range, range + 1);
                    int dust = Dust.NewDust(spwanPos, 2, 2, DustID.OrangeTorch, 0, 0);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        /// <summary>提示文本(服务器广播 / 本地生成)；传LocalizedText使每个客户端按自己的语言渲染，
        /// FromLiteral会把服务器语言的成品字符串发给所有人</summary>
        internal void BroadcastPrompt(LocalizedText text) {
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.CombatTextString, -1, -1, text.ToNetworkText()
                    , (int)Color.YellowGreen.PackedValue, HitBox.Center.X, HitBox.Center.Y);
            }
            else {
                CombatText.NewText(HitBox, Color.YellowGreen, text.Value);
            }
        }

        #endregion

        /// <summary>生成机械臂(仅服务器)</summary>
        private void SpawnArmsIfNeeded() {
            if (VaultUtils.isClient) {
                return;
            }
            if (dontSpawnArmTime > 0) {
                return;
            }

            //清理失效或不属于本收集器的索引
            ArmActorIndices.RemoveAll(index => !IsOwnedArmValid(index));

            if (ArmActorIndices.Count >= 3) {
                return;
            }

            //检查机械臂总数限制
            if (ActorLoader.GetActiveActors<CollectorArm>().Count > GlobalArmLimit) {
                if (textIdleTime <= 0) {
                    Defer(() => BroadcastPrompt(Collector.Text1));
                    textIdleTime = 300;
                }
                return;
            }

            int armSlot = ArmActorIndices.Count;
            //并行阶段Actor生成与列表记账延迟到主线程执行(串行阶段立即执行)
            Defer(() => {
                int actorIndex = ActorLoader.NewActor<CollectorArm>(ArmPos, Vector2.Zero);
                ArmActorIndices.Add(actorIndex);
                if (actorIndex >= 0 && actorIndex < ActorLoader.MaxActorCount) {
                    ActorLoader.Actors[actorIndex].OnSpawn(Position, armSlot);
                }
            });
        }

        public override void UpdateMachine() {
            FindFrame();

            if (!workState) {
                return;
            }

            hoverSengs = HoverTP
                ? Math.Min(hoverSengs + 0.1f, 1f)
                : Math.Max(hoverSengs - 0.1f, 0f);

            if (textIdleTime > 0) {
                textIdleTime--;
            }
            if (dontSpawnArmTime > 0) {
                dontSpawnArmTime--;
            }

            SpawnArmsIfNeeded();

            //检查能量状态
            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt && textIdleTime <= 0 && !VaultUtils.isClient) {
                Defer(() => BroadcastPrompt(Collector.Text3));
                textIdleTime = 300;
            }
        }

        #region 绘制

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (TagItemSign > ItemID.None) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, TagItemSign
                    , CenterInWorld - Main.screenPosition + new Vector2(0, 32)
                    , itemWidth: 32, 0, 0, Lighting.GetColor(Position.ToPoint()));
            }

            if (FilterInstalled && hoverSengs > 0.01f) {
                IReadOnlyList<int> filterItems = Filter.OrderedItems;
                if (filterItems.Count > 0) {
                    const float maxRadius = 150f;
                    float currentRadius = maxRadius * hoverSengs;
                    float angleIncrement = MathHelper.TwoPi / filterItems.Count;

                    Vector2 drawCenter = CenterInWorld - Main.screenPosition + new Vector2(0, 32);

                    for (int i = 0; i < filterItems.Count; i++) {
                        int itemType = filterItems[i];
                        if (itemType <= ItemID.None) continue;

                        float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                        Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * currentRadius;
                        Vector2 itemPos = drawCenter + offset;

                        Color drawColor = VaultUtils.MultiStepColorLerp(hoverSengs, Lighting.GetColor(Position.ToPoint()), Color.White);
                        float scale = hoverSengs * 1.25f;

                        VaultUtils.SafeLoadItem(itemType);
                        VaultUtils.SimpleDrawItem(Main.spriteBatch, itemType, itemPos, itemWidth: 32, scale, 0, drawColor);
                    }
                }
            }

            DrawStorageLinks(spriteBatch);
            DrawChargeBar();
        }

        private void DrawStorageLinks(SpriteBatch spriteBatch) {
            CollectorUI ui = CollectorUI.Instance;
            bool uiFocus = ui != null && ui.Station == this && ui.Active;
            bool picking = uiFocus && ui.PickingStorage;
            float alpha = Math.Max(uiFocus ? 0.85f : 0f, hoverSengs * 0.85f);

            if (alpha <= 0.01f && !picking) {
                return;
            }

            //绑定连线
            for (int i = 0; i < BoundStorages.Count; i++) {
                Point16 pos = BoundStorages[i];
                IStorageProvider provider = ResolveBinding(pos);
                bool valid = provider != null;

                Vector2 endPoint = valid ? provider.WorldCenter : pos.ToWorldCoordinates();
                Rectangle endRect = valid ? provider.HitBox : new Rectangle(pos.X * 16, pos.Y * 16, 32, 32);
                Color linkColor = valid ? new Color(120, 220, 255) : new Color(255, 80, 60);

                DrawWorldLine(spriteBatch, ArmPos, endPoint, linkColor * (alpha * 0.45f));
                DrawWorldRectOutline(spriteBatch, endRect, linkColor * alpha);

                //沿连线流动的光点
                if (valid) {
                    for (int k = 0; k < 3; k++) {
                        float t = (Main.GlobalTimeWrappedHourly * 0.35f + k / 3f + i * 0.13f) % 1f;
                        Vector2 dotPos = Vector2.Lerp(ArmPos, endPoint, t);
                        spriteBatch.Draw(VaultAsset.placeholder2.Value, dotPos - Main.screenPosition
                            , new Rectangle(0, 0, 1, 1), linkColor * alpha, 0f
                            , new Vector2(0.5f), 3f, SpriteEffects.None, 0f);
                    }
                }
            }

            //选取模式，范围环+悬停高亮
            if (picking) {
                const int ringDots = 72;
                for (int i = 0; i < ringDots; i++) {
                    float angle = MathHelper.TwoPi * i / ringDots + Main.GlobalTimeWrappedHourly * 0.05f;
                    Vector2 dotPos = CenterInWorld + angle.ToRotationVector2() * MaxBindDistance;
                    spriteBatch.Draw(VaultAsset.placeholder2.Value, dotPos - Main.screenPosition
                        , new Rectangle(0, 0, 1, 1), new Color(255, 180, 90) * 0.55f, 0f
                        , new Vector2(0.5f), 3f, SpriteEffects.None, 0f);
                }

                Point16 mouseTile = Main.MouseWorld.ToTileCoordinates16();
                if (VaultUtils.SafeGetTopLeft(mouseTile.X, mouseTile.Y, out Point16 topLeft)
                    && StorageLoader.TryGetStorageTargetByPoint(topLeft, out IStorageProvider hoverProvider)) {
                    bool canBind = BoundStorages.Count < MaxBindings
                        && !BoundStorages.Contains(hoverProvider.Position)
                        && BindingInRange(hoverProvider.Position);
                    Color hoverColor = canBind ? Color.Gold : new Color(255, 80, 60);
                    DrawWorldRectOutline(spriteBatch, hoverProvider.HitBox, hoverColor);
                    DrawWorldLine(spriteBatch, ArmPos, hoverProvider.WorldCenter, hoverColor * 0.5f);
                }
            }
        }

        internal static void DrawWorldLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness = 2f) {
            Vector2 dir = end - start;
            float len = dir.Length();
            if (len < 1f) {
                return;
            }
            spriteBatch.Draw(VaultAsset.placeholder2.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1)
                , color, dir.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        internal static void DrawWorldRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness = 2) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 off = -Main.screenPosition;
            Rectangle src = new Rectangle(0, 0, 1, 1);
            spriteBatch.Draw(px, new Vector2(rect.X, rect.Y) + off, src, color, 0f, Vector2.Zero, new Vector2(rect.Width, thickness), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, new Vector2(rect.X, rect.Bottom - thickness) + off, src, color, 0f, Vector2.Zero, new Vector2(rect.Width, thickness), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, new Vector2(rect.X, rect.Y) + off, src, color, 0f, Vector2.Zero, new Vector2(thickness, rect.Height), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, new Vector2(rect.Right - thickness, rect.Y) + off, src, color, 0f, Vector2.Zero, new Vector2(thickness, rect.Height), SpriteEffects.None, 0f);
        }

        #endregion
    }
}
