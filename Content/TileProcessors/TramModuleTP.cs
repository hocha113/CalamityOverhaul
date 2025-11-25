using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.TileProcessors;
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

namespace CalamityOverhaul.Content.TileProcessors
{
    /// <summary>
    /// 转化物质工作台的 TileProcessor
    /// 负责管理工作台的物品存储、网络同步和UI交互
    /// </summary>
    public class TramModuleTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<TransmutationOfMatter>();

        #region 常量定义

        private const int MAX_INTERACTION_DISTANCE = 120;
        private const int ITEM_COUNT = 81;
        private const int ANIMATION_FRAME_COUNT = 10;
        private const int ANIMATION_FRAME_DURATION = 6;

        #endregion

        #region 字段

        //物品存储
        public Item[] items;

        //动画和视觉效果
        private float _uiPromptProgress;
        private int _animationTimer;
        private bool _isMouseOver;
        internal bool drawGlow;
        internal Color gloaColor;
        private int _glowTimer;
        internal int frame;

        //资源
        [VaultLoaden(CWRConstant.Asset + "Tiles/TransmutationOfMatter")]
        internal static Asset<Texture2D> modeuleBodyAsset = null;
        [VaultLoaden(CWRConstant.UI + "SupertableUIs/TexturePackButtons")]
        internal static Asset<Texture2D> truesFromeAsset = null;

        #endregion

        #region 属性

        internal Vector2 Center => PosInWorld + new Vector2(TransmutationOfMatter.Width, TransmutationOfMatter.Height) * 8;

        /// <summary>
        /// 检查是否有玩家正在使用此工作台
        /// </summary>
        public bool IsInUse => GetActivePlayer() != null;

        #endregion

        #region 生命周期

        public override void SetProperty() {
            InitializeItems();
        }

        private void InitializeItems() {
            items = new Item[ITEM_COUNT];
            for (int i = 0; i < items.Length; i++) {
                items[i] = new Item();
            }
        }

        #endregion

        #region 数据同步

        public override void SendData(ModPacket data) {
            //发送物品数据
            for (int i = 0; i < ITEM_COUNT; i++) {
                if (items[i] == null) {
                    items[i] = new Item(0);
                }
                ItemIO.Send(items[i], data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            //接收物品数据
            for (int i = 0; i < ITEM_COUNT; i++) {
                items[i] = ItemIO.Receive(reader, true);
            }
            SyncItemsToUI();
        }

        public override void SaveData(TagCompound tag) {
            try {
                List<TagCompound> itemTags = new List<TagCompound>();
                for (int i = 0; i < items.Length; i++) {
                    if (items[i] == null) {
                        items[i] = new Item(0);
                    }
                    itemTags.Add(ItemIO.Save(items[i]));
                }
                tag["itemTags"] = itemTags;
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"TramModuleTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                //兼容旧版本数据
                LoadLegacyData(tag);

                if (!tag.TryGet("itemTags", out List<TagCompound> itemTags)) {
                    return;
                }

                List<Item> loadedItems = new List<Item>();
                for (int i = 0; i < itemTags.Count; i++) {
                    loadedItems.Add(ItemIO.Load(itemTags[i]));
                }

                items = loadedItems.ToArray();
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"TramModuleTP.LoadData Error: {ex.Message}");
            }
        }

        private void LoadLegacyData(TagCompound tag) {
            try {
                if (tag.TryGet("SupertableUI_ItemDate", out Item[] loadSupUIItems)) {
                    for (int i = 0; i < loadSupUIItems.Length; i++) {
                        if (loadSupUIItems[i] == null) {
                            loadSupUIItems[i] = new Item(0);
                        }
                    }
                    items = loadSupUIItems;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"TramModuleTP.LoadLegacyData Error: {ex.Message}");
            }
        }

        #endregion

        #region UI交互

        /// <summary>
        /// 打开UI并绑定此工作台
        /// </summary>
        public void OpenUI(Player player) {
            if (player == null || !player.active) return;

            CWRPlayer modPlayer = player.CWR();

            //如果已经在使用此工作台，切换UI状态
            if (modPlayer.TramTPContrType == WhoAmI) {
                SupertableUI.TramTP = this;
                //同步物品到UI
                SyncItemsToUI();
                SendData();
                SupertableUI.Instance.Active = !SupertableUI.Instance.Active;
            }
            else {
                //如果正在使用其他工作台，先保存那个工作台的数据
                if (modPlayer.TramTPContrType >= 0 && SupertableUI.TramTP != null) {
                    SupertableUI.TramTP.SaveItemsFromUI();
                }

                //绑定到新工作台
                modPlayer.TramTPContrType = WhoAmI;
                SupertableUI.TramTP = this;
                //同步物品到UI
                SyncItemsToUI();
                SendData();
                SupertableUI.Instance.Active = true;

                //打开背包UI
                if (!Main.playerInventory) {
                    Main.playerInventory = true;
                }
            }

            //预加载原版物品纹理
            PreloadItemTextures();
        }

        /// <summary>
        /// 关闭UI并保存数据
        /// </summary>
        public void CloseUI(Player player) {
            if (player == null) return;

            CWRPlayer modPlayer = player.CWR();
            if (modPlayer.TramTPContrType == WhoAmI) {
                SaveItemsFromUI();
                modPlayer.TramTPContrType = -1;
                SupertableUI.Instance.Active = false;
                SendData();
            }
        }

        /// <summary>
        /// 将物品数据同步到UI
        /// </summary>
        private void SyncItemsToUI() {
            if (SupertableUI.Instance == null) return;

            for (int i = 0; i < ITEM_COUNT; i++) {
                SupertableUI.Instance.Items[i] = items[i]?.Clone() ?? new Item();
            }
        }

        /// <summary>
        /// 从UI保存物品数据
        /// </summary>
        public void SaveItemsFromUI() {
            if (SupertableUI.Instance?.Items == null) return;

            //复制UI中的物品回TileProcessor
            for (int i = 0; i < ITEM_COUNT && i < SupertableUI.Instance.Items.Length; i++) {
                items[i] = SupertableUI.Instance.Items[i]?.Clone() ?? new Item();
            }
        }

        /// <summary>
        /// 预加载物品纹理
        /// </summary>
        private void PreloadItemTextures() {
            if (VaultUtils.isServer) return;

            foreach (var item in items) {
                if (item == null || item.type == ItemID.None || item.type >= ItemID.Count) {
                    continue;
                }
                Main.instance.LoadItem(item.type);
            }
        }

        #endregion

        #region 更新逻辑

        public override void Update() {
            //更新动画帧
            VaultUtils.ClockFrame(ref frame, ANIMATION_FRAME_DURATION, ANIMATION_FRAME_COUNT);
            _animationTimer++;

            Player player = Main.LocalPlayer;
            if (!player.active || Main.myPlayer != player.whoAmI) {
                return;
            }

            //更新光照效果
            if (_isMouseOver || _uiPromptProgress > 0) {
                Lighting.AddLight(Center, Color.White.ToVector3());
            }

            CWRPlayer modPlayer = player.CWR();

            //服务端不需要处理视觉效果
            if (VaultUtils.isServer) {
                return;
            }

            //更新鼠标悬停状态
            UpdateMouseOver();

            //更新发光效果
            UpdateGlowEffect(player);

            //更新UI提示动画
            UpdateUIPrompt(modPlayer);

            //检查距离和玩家状态，自动关闭UI
            CheckAutoClose(player, modPlayer);
        }

        private void UpdateMouseOver() {
            Rectangle tileRect = new Rectangle(
                Position.X * 16,
                Position.Y * 16,
                TransmutationOfMatter.Width * 16,
                TransmutationOfMatter.Height * 16
            );

            _isMouseOver = tileRect.Intersects(new Rectangle(
                (int)Main.MouseWorld.X,
                (int)Main.MouseWorld.Y,
                1, 1
            ));
        }

        private void UpdateGlowEffect(Player player) {
            float distance = PosInWorld.Distance(player.Center);
            drawGlow = distance < MAX_INTERACTION_DISTANCE &&
                      _isMouseOver &&
                      !SupertableUI.Instance.Active;

            if (drawGlow) {
                _glowTimer++;
                gloaColor = Color.AliceBlue * MathF.Abs(MathF.Sin(_glowTimer * 0.04f));
            }
            else {
                _glowTimer = 0;
            }
        }

        private void UpdateUIPrompt(CWRPlayer modPlayer) {
            if (modPlayer.InspectOmigaTime > 0) {
                if (_uiPromptProgress < 1f) {
                    _uiPromptProgress += 0.1f;
                }
            }
            else {
                if (_uiPromptProgress > 0f) {
                    _uiPromptProgress -= 0.1f;
                    _uiPromptProgress = Math.Max(0f, _uiPromptProgress);
                }
            }
        }

        private void CheckAutoClose(Player player, CWRPlayer modPlayer) {
            if (!modPlayer.SupertableUIStartBool) return;

            float distance = PosInWorld.Distance(player.Center);

            if ((distance >= MAX_INTERACTION_DISTANCE || player.dead) &&
                modPlayer.TramTPContrType == WhoAmI) {
                CloseUI(player);
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
            }
        }

        #endregion

        #region 销毁处理

        public override void OnKill() {
            //如果有玩家正在使用此工作台，关闭UI
            Player activePlayer = GetActivePlayer();
            if (activePlayer != null) {
                CloseUI(activePlayer);
            }

            //掉落物品
            if (!VaultUtils.isClient) {
                DropItems();
            }

            //重置物品数组
            InitializeItems();
        }

        /// <summary>
        /// 掉落所有物品
        /// </summary>
        private void DropItems() {
            foreach (var item in items) {
                if (item == null || item.IsAir) {
                    continue;
                }

                int itemIndex = Item.NewItem(
                    new EntitySource_WorldGen(),
                    Center,
                    item.type,
                    item.stack
                );

                if (VaultUtils.isServer && itemIndex >= 0) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取正在使用此工作台的玩家
        /// </summary>
        private Player GetActivePlayer() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p != null && p.active && p.CWR().TramTPContrType == WhoAmI) {
                    return p;
                }
            }
            return null;
        }

        #endregion

        #region 渲染

        public override void Draw(SpriteBatch spriteBatch) {
            if (_uiPromptProgress <= 0f || truesFromeAsset?.Value == null) {
                return;
            }

            float pulseTime = MathF.Sin(_animationTimer * 0.14f);
            Rectangle sourceRect = new Rectangle(
                0, 0,
                truesFromeAsset.Width() / 2,
                truesFromeAsset.Height() / 2
            );

            Vector2 drawPos = Center + new Vector2(0, -40) - new Vector2(2, pulseTime * 8);
            drawPos -= Main.screenPosition;

            spriteBatch.Draw(
                truesFromeAsset.Value,
                drawPos,
                sourceRect,
                Color.Gold * _uiPromptProgress,
                MathHelper.Pi,
                sourceRect.Size() / 2,
                1f + pulseTime * 0.1f,
                SpriteEffects.None,
                0f
            );
        }

        #endregion
    }
}
