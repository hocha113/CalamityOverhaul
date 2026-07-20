using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.Actors;
using InnoVault.Storages;
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
    /// <summary>
    /// 收集器的存储投放策略
    /// </summary>
    internal enum CollectorStorageMode : byte
    {
        /// <summary>就近存储：自动存入范围内最近的可用容器</summary>
        Auto = 0,
        /// <summary>绑定优先：先尝试绑定容器，失败后回退就近存储</summary>
        BoundFirst = 1,
        /// <summary>仅限绑定：只存入绑定的容器</summary>
        BoundOnly = 2
    }

    internal class CollectorTP : BaseBattery, IItemFilterHost
    {
        public override int TargetTileID => ModContent.TileType<CollectorTile>();
        public override int TargetItem => ModContent.ItemType<Collector>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        /// <summary>就近模式的存储搜索半径(像素)</summary>
        internal const int StorageSearchRange = 600;
        /// <summary>绑定容器允许的最大距离(像素)，超出后绑定视为失效</summary>
        internal const int MaxBindDistance = 2000;
        /// <summary>最大绑定数量</summary>
        internal const int MaxBindings = 6;
        /// <summary>每次抓取消耗的能量</summary>
        internal const int consumeUE = 8;
        /// <summary>存储候选快照的缓存时长(帧)</summary>
        private const int StorageCacheTicks = 30;
        /// <summary>全局机械臂总数上限</summary>
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

        /// <summary>过滤模式是否已装载(以过滤卡为标记物)</summary>
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

        //存储候选快照缓存，避免每次搜索都全量扫描箱子
        private readonly List<IStorageProvider> storageCandidates = [];
        private bool storageCacheDirty = true;
        private uint storageCacheTick;

        public override void SetBattery() {
            Filter = new ItemFilterSet();
            DrawExtendMode = 2200;
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            Filter.Write(data);
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
            Filter.Read(reader);
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

        /// <summary>
        /// 检查臂索引是否有效且归属于本收集器，防止Actor槽位复用后跨收集器认领
        /// </summary>
        internal bool IsOwnedArmValid(int actorIndex) {
            if (actorIndex < 0 || actorIndex >= ActorLoader.MaxActorCount) {
                return false;
            }
            Actor actor = ActorLoader.Actors[actorIndex];
            return actor != null && actor.Active && actor is CollectorArm arm && arm.collectorPos == Position;
        }

        /// <summary>
        /// 统计归属于本收集器的活跃机械臂数量，客户端也可用(归属坐标经同步)
        /// </summary>
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

            //手持过滤卡右键：安装或刷新名单副本(卸载入口在控制台/编辑器中)
            if (item.ModItem is ItemFilter card) {
                TagItemSign = item.type;
                Filter.CopyFrom(card.Filter);

                SoundEngine.PlaySound(CWRSound.Select with { Pitch = -0.2f });
                if (!VaultUtils.isServer) {
                    CombatText.NewText(HitBox, ItemFilterTheme.Gold, ItemFilterEditorUI.InstalledText.Value);
                }

                SendData();
                return false;
            }

            //手持普通物品右键：设定或取消单一收集标记
            if (TagItemSign > ItemID.None && TagItemSign == item.type) {
                TagItemSign = ItemID.None;
            }
            else {
                TagItemSign = item.type;
            }

            SoundEngine.PlaySound(CWRSound.Select with {
                Pitch = TagItemSign > ItemID.None ? -0.2f : 0.2f
            });

            SendData();
            return false;
        }

        #region 存储绑定与查找

        /// <summary>
        /// 使快照缓存失效，绑定数据变化后调用
        /// </summary>
        internal void InvalidateStorageCache() => storageCacheDirty = true;

        /// <summary>
        /// 绑定坐标是否在允许距离内
        /// </summary>
        internal bool BindingInRange(Point16 pos)
            => CenterInWorld.Distance(pos.ToWorldCoordinates()) <= MaxBindDistance;

        /// <summary>
        /// 解析一个绑定坐标为存储提供者，失效或超距返回null
        /// </summary>
        internal IStorageProvider ResolveBinding(Point16 pos) {
            if (!BindingInRange(pos)) {
                return null;
            }
            IStorageProvider provider = StorageLoader.GetStorageTargetByPoint(pos);
            return provider != null && provider.IsValid ? provider : null;
        }

        /// <summary>
        /// 尝试添加一个绑定，返回是否成功；首次绑定会把模式从就近切换为绑定优先
        /// </summary>
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

        /// <summary>
        /// 获取当前可用的存储候选列表(带缓存)，绑定目标按优先级排在最前，
        /// 非仅绑定模式下追加就近搜索的结果
        /// </summary>
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
                    //防御性上限，避免箱阵场景下候选爆炸
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

        /// <summary>
        /// 按当前模式与优先级查找可存放指定物品的存储目标
        /// </summary>
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

        /// <summary>
        /// 找不到任何存储目标时的提示(带节流)，仅服务器/单人逻辑侧调用
        /// </summary>
        internal void PromptNoStorage() {
            if (textIdleTime > 0 || VaultUtils.isClient) {
                return;
            }
            textIdleTime = 300;
            BroadcastPrompt(Collector.Text2.Value);

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

        /// <summary>
        /// 机器提示文本，专用服务器上通过网络广播到所有客户端，否则本地生成
        /// </summary>
        internal void BroadcastPrompt(string text) {
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.CombatTextString, -1, -1, NetworkText.FromLiteral(text)
                    , (int)Color.YellowGreen.PackedValue, HitBox.Center.X, HitBox.Center.Y);
            }
            else {
                CombatText.NewText(HitBox, Color.YellowGreen, text);
            }
        }

        #endregion

        ///<summary>
        ///检查并生成机械臂(仅服务器端)
        ///</summary>
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
                    Defer(() => BroadcastPrompt(Collector.Text1.Value));
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
                Defer(() => BroadcastPrompt(Collector.Text3.Value));
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

        /// <summary>
        /// 绘制收集器到绑定容器的连线与选取模式的辅助可视化
        /// </summary>
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

            //选取模式：范围环 + 鼠标悬停容器高亮
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
