using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.UIs.SupertableUIs {
    public class SupertableUI : UIHandle, ICWRLoader {
        #region 常量&静态数据
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MainValue");
        public static SupertableUI Instance => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        private static RecipeSidebarListViewUI RecipeSidebarListView;
        public static string[][] RpsDataStringArrays { get; set; } = [];
        public static List<string[]> OtherRpsData_ZenithWorld_StringList { get; set; } = [];
        public static List<string[]> ModCall_OtherRpsData_StringList { get; set; } = [];
        public static List<RecipeData> AllRecipes { get; set; } = [];//全部配方
        public readonly static float[] itemHoverSengses = new float[maxCellNumX * maxCellNumY];//格子悬停感应插值
        public const int cellWid = 48;
        public const int cellHig = 46;
        public const int maxCellNumX = 9;
        public const int maxCellNumY = 9;
        public const int SlotCount = maxCellNumX * maxCellNumY; //81
        private const int RecipeDataLength = 82; //材料81+结果1
        public static int AllRecipesVanillaContentCount { get; private set; }//基础配方数量
        public static TramModuleTP TramTP { get; set; }
        #endregion

        #region 实例字段
        public string[] StaticFullItemNames;//最近一次匹配成功的材料及结果名字缓存
        public int[] StaticFullItemTypes;//最近一次匹配成功的材料及结果类型缓存
        private int[] fullItemTypesTemp;//临时复用缓冲(可能被移除但先保留兼容)
        public Item[] items; //实际放置材料
        public Item[] previewItems;//当前预览的配方材料
        public Item inputItem;//输出物品
        private Rectangle PutItemCellRec;//材料区域矩形
        public Rectangle inputRec;//输出格矩形
        public Rectangle closeRec;//关闭按钮矩形
        private Point mouseInCellCoord;//鼠标所在格子坐标
        private int InCoordIndex => (mouseInCellCoord.Y * maxCellNumX) + mouseInCellCoord.X;//索引
        internal float _sengs;//UI开合插值
        internal int downSengsTime;//延迟关闭计时
        public bool hoverInPutItemCellPage;//鼠标是否在材料区域
        public bool onInputP;//鼠标是否在输出格
        public bool onCloseP;//鼠标是否在关闭按钮
        #endregion

        #region 属性
        public Vector2 TopLeft => DrawPosition + new Vector2(16, 30);
        public override bool Active {
            get {
                return player.CWR().SupertableUIStartBool || _sengs > 0;
            }
            set {
                player.CWR().SupertableUIStartBool = value;
                SyncTramModuleItemsIfNeed();
            }
        }
        #endregion

        #region 生命周期接口实现
        void ICWRLoader.SetupData() {
            LoadRecipe();
            RecipeUI.LoadAllRecipes();
        }
        void ICWRLoader.UnLoadData() {
            TramTP = null;
            RecipeSidebarListView = null;
            RpsDataStringArrays = [];
            OtherRpsData_ZenithWorld_StringList = [];
            ModCall_OtherRpsData_StringList = [];
            AllRecipes = [];
        }
        public override void OnEnterWorld() {
            _sengs = 0f;
        }
        public override void SaveUIData(TagCompound tag) {
            tag["SupertableUI_DrawPos_X"] = DrawPosition.X;
            tag["SupertableUI_DrawPos_Y"] = DrawPosition.Y;
        }
        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("SupertableUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = 500;
            }
            if (tag.TryGet("SupertableUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = 300;
            }
        }
        #endregion

        #region 静态加载逻辑
        public static void LoadRecipe() {
            Type type = typeof(SupertableRecipeDate);
            FieldInfo[] stringArrayFields = type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(string[])).ToArray();
            PropertyInfo[] stringArrayProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(string[])).ToArray();
            var allMembers = stringArrayFields.Concat<MemberInfo>(stringArrayProperties).ToArray();
            RpsDataStringArrays = allMembers.Select(member => {
                if (member is FieldInfo field) {
                    return (string[])field.GetValue(null);
                }
                else if (member is PropertyInfo property) {
                    return (string[])property.GetValue(null);
                }
                return null;
            }).Where(array => array != null).ToArray();
            if (ModCall_OtherRpsData_StringList?.Count > 0) {
                RpsDataStringArrays = RpsDataStringArrays.Concat(ModCall_OtherRpsData_StringList).ToArray();
            }
            RecipeSidebarListView = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
            RecipeSidebarListView.recipeTargetElmts = new List<RecipeTargetElmt>();
            AllRecipes.Clear();
            foreach (string[] value in RpsDataStringArrays) {
                if (value.Length != RecipeDataLength) {
                    string pag = string.Join(", ", value);
                    throw new InvalidOperationException($"Invalid length: {pag} Length is not {RecipeDataLength}.");
                }
                string targetItemFullName = value[^1];
                int targetItemID = VaultUtils.GetItemTypeFromFullName(targetItemFullName);
                if (targetItemFullName != "Null/Null" && targetItemID == ItemID.None) {
                    string pag = string.Join(", ", value);
                    throw new InvalidOperationException($"Invalid target item: {pag} The result of the Target synthesis is a None item, but the key you gave is not a \"Null/Null\" placeholder.");
                }
                RecipeData recipeData = new RecipeData {
                    Values = value,
                    Target = targetItemID
                };
                recipeData.BuildMaterialTypesCache();//缓存材料类型
                AllRecipes.Add(recipeData);
                RecipeSidebarListView.recipeTargetElmts.Add(new RecipeTargetElmt { recipeData = recipeData });
            }
            AllRecipesVanillaContentCount = AllRecipes.Count;
            CWRMod.Instance.Logger.Info($"Get the recipe table capacity: {AllRecipesVanillaContentCount}");
        }
        public static void LoadenWorld() {
            if (AllRecipes.Count > AllRecipesVanillaContentCount) {
                AllRecipes.RemoveRange(AllRecipesVanillaContentCount, AllRecipes.Count - AllRecipesVanillaContentCount);
            }
            SetZenithWorldRecipesData();
            RecipeUI.LoadAllRecipes();
        }
        public static void SetZenithWorldRecipesData() {
            if (Main.zenithWorld) {
                foreach (var recipes in OtherRpsData_ZenithWorld_StringList) {
                    RecipeData recipeData = new RecipeData {
                        Values = recipes,
                        Target = VaultUtils.GetItemTypeFromFullName(recipes[^1])
                    };
                    recipeData.BuildMaterialTypesCache();
                    AllRecipes.Add(recipeData);
                }
            }
        }
        #endregion

        #region 初始化&状态更新
        private void EnsureInitialized() {
            if (items == null) {
                items = new Item[SlotCount];
                for (int i = 0; i < items.Length; i++) {
                    items[i] = new Item();
                }
            }
            if (previewItems == null || previewItems.Length != SlotCount) {
                previewItems = new Item[SlotCount];
                for (int i = 0; i < previewItems.Length; i++) {
                    previewItems[i] = new Item();
                }
            }
            if (fullItemTypesTemp == null || fullItemTypesTemp.Length != SlotCount + 1) {
                fullItemTypesTemp = new int[SlotCount + 1];
            }
            if (inputItem == null) {
                inputItem = new Item();
            }
        }
        private void UpdateUIElementPos() {
            if (player.CWR().SupertableUIStartBool && downSengsTime <= 0) {
                if (_sengs < 1f) {
                    _sengs += 0.2f;
                }
            }
            else {
                if (_sengs > 0f) {
                    _sengs -= 0.14f;
                }
            }
            _sengs = MathHelper.Clamp(_sengs, 0, 1);
            if (downSengsTime > 0) {
                downSengsTime--;
            }
            Vector2 inUIMousePos = MousePosition - TopLeft;
            int mouseXGrid = (int)(inUIMousePos.X / cellWid);
            int mouseYGrid = (int)(inUIMousePos.Y / cellHig);
            mouseInCellCoord = new Point(mouseXGrid, mouseYGrid);
            UIHitBox = new Rectangle((int)TopLeft.X, (int)TopLeft.Y, cellWid * maxCellNumX + 200, cellHig * maxCellNumY);
            PutItemCellRec = new Rectangle((int)TopLeft.X, (int)TopLeft.Y, cellWid * maxCellNumX, cellHig * maxCellNumY);
            inputRec = new Rectangle((int)(DrawPosition.X + 555), (int)(DrawPosition.Y + 215), 92, 90);
            closeRec = new Rectangle((int)(DrawPosition.X), (int)(DrawPosition.Y), 30, 30);
            Rectangle mouseRec = MouseHitBox;
            hoverInMainPage = UIHitBox.Intersects(mouseRec);
            hoverInPutItemCellPage = PutItemCellRec.Intersects(mouseRec);
            onInputP = inputRec.Intersects(mouseRec);
            onCloseP = closeRec.Intersects(mouseRec);
        }
        #endregion

        #region 对外工具方法
        public static Item InStrGetItem(string key, bool loadVanillaItem = false) {
            if (key == "Null/Null") {
                return new Item();
            }
            if (int.TryParse(key, out int intValue)) {
                if (loadVanillaItem && !VaultUtils.isServer) {
                    Main.instance.LoadItem(intValue);
                }
                return new Item(intValue);
            }
            string[] fruits = key.Split('/');
            return ModLoader.GetMod(fruits[0]).Find<ModItem>(fruits[1]).Item;
        }
        public static int[] FullItem(string[] arg) {
            int[] toValueTypes = new int[arg.Length];
            for (int i = 0; i < arg.Length; i++) {
                string value = arg[i];
                toValueTypes[i] = VaultUtils.GetItemTypeFromFullName(value);
            }
            return toValueTypes;
        }
        public Vector2 ArcCellPos(int index) {
            int y = index / maxCellNumX;
            int x = index - (y * maxCellNumX);
            return (new Vector2(x, y) * new Vector2(cellWid, cellHig)) + TopLeft;
        }
        public void TakeAllItem() {
            foreach (var item in items) {
                if (item == null) {
                    continue;
                }
                Item item1 = item.Clone();
                player.QuickSpawnItem(player.FromObjectGetParent(), item1, item1.stack);
                item.TurnToAir();
            }
        }
        public void OneClickPFunc() {
            bool onSound = false;
            if (previewItems != null && previewItems.Length == items.Length) {
                for (int i = 0; i < previewItems.Length; i++) {
                    Item preItem = previewItems[i];
                    if (preItem == null || preItem.type == ItemID.None) {
                        continue;
                    }
                    if (preItem.type == Main.mouseItem.type && preItem.type != ItemID.None) {
                        Item targetItem = Main.mouseItem.Clone();
                        targetItem.stack = 1;
                        items[i] = targetItem;
                        onSound = true;
                        Main.mouseItem.stack -= 1;
                        if (Main.mouseItem.stack == 0) {
                            Main.mouseItem.TurnToAir();
                        }
                        continue;
                    }
                    foreach (var backItem in player.inventory) {
                        if (preItem.type == backItem.type && preItem.type != ItemID.None) {
                            if (items[i].type == ItemID.None) {
                                Item targetItem = backItem.Clone();
                                targetItem.stack = 1;
                                items[i] = targetItem;
                            }
                            else {
                                items[i].stack++;
                                if (items[i].stack > items[i].maxStack) {
                                    items[i].stack = items[i].maxStack;
                                    break;
                                }
                            }
                            onSound = true;
                            backItem.stack -= 1;
                            if (backItem.stack == 0) {
                                backItem.TurnToAir();
                            }
                            break;
                        }
                    }
                }
                if (onSound) {
                    SoundEngine.PlaySound(SoundID.Grab);
                }
            }
        }
        #endregion

        #region 内部逻辑-配方匹配与结果
        private void ResetInputItem() {
            if (inputItem == null || inputItem.type != ItemID.None) {
                inputItem = new Item();
            }
        }
        private bool ItemsMatch(int[] materials) {
            for (int i = 0; i < SlotCount; i++) {
                if (items[i].type != materials[i]) {
                    return false;
                }
            }
            return true;
        }
        private void AssignResultItem(int[] materialTypes) {
            Item item = new Item(materialTypes[^1]);
            int minNum = int.MaxValue;
            foreach (var value in items) {
                if (value.type == ItemID.None) {
                    continue;
                }
                if (value.stack < minNum) {
                    minNum = value.stack;
                }
            }
            if (minNum == int.MaxValue) {
                minNum = 1;
            }
            item.stack = Math.Min(minNum, item.maxStack);
            inputItem = item;
            StaticFullItemTypes = materialTypes;
            string[] names = new string[materialTypes.Length];
            for (int i = 0; i < materialTypes.Length; i++) {
                Item fullItem = new Item(materialTypes[i]);
                names[i] = fullItem.ModItem == null ? fullItem.type.ToString() : fullItem.ModItem.FullName;
            }
            StaticFullItemNames = names;
        }
        public void FinalizeCraftingResult(bool netWork = true) {
            bool matched = false;
            foreach (RecipeData data in AllRecipes) {
                if (data.MaterialTypesCache == null || data.MaterialTypesCache.Length != RecipeDataLength) {
                    data.BuildMaterialTypesCache();
                }
                if (!ItemsMatch(data.MaterialTypesCache)) {
                    continue;
                }
                AssignResultItem(data.MaterialTypesCache);
                matched = true;
                break;
            }
            if (!matched) {
                ResetInputItem();
            }
            if (netWork) {
                SyncTramModuleItemsIfNeed();
            }
        }
        private void ConsumeMaterialsForOutput() {
            if (StaticFullItemTypes == null || inputItem == null || inputItem.type == ItemID.None) {
                return;
            }
            for (int i = 0; i < SlotCount; i++) {
                if (items[i].type == StaticFullItemTypes[i]) {
                    items[i].stack -= inputItem.stack;
                    if (items[i].stack <= 0) {
                        items[i] = new Item();
                    }
                }
            }
        }
        #endregion

        #region 内部逻辑-与TramModule同步
        internal void SyncTramModuleItemsIfNeed() {
            if (TramTP != null && TramTP.Active) {
                if (TramTP.items == null) {
                    TramTP.items = items;
                }
                else {
                    items = TramTP.items;
                }
                if (!VaultUtils.isSinglePlayer) {
                    TramTP.SendData();
                }
            }
        }
        #endregion

        #region 物品交互核心
        private void HandleLeftClickOnSlot(int index) {
            if (items[index] == null) {
                items[index] = new Item();
            }
            KeyboardState state = Keyboard.GetState();
            if (state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift)) {
                GatheringItem2(index, ref Main.mouseItem);
            }
            else {
                HandleItemClick(ref items[index], ref Main.mouseItem);
            }
            FinalizeCraftingResult();
        }
        private void HandleContinuousGather(int index) {
            if (CWRKeySystem.TOM_GatheringItem.Current) {
                GatheringItem(index, ref Main.mouseItem);
                FinalizeCraftingResult();
            }
        }
        private void HandleRightClickOnSlot(int index, int museSR) {
            if (museSR == 1) {
                HandleRightClick(ref items[index], ref Main.mouseItem);
                FinalizeCraftingResult();
            }
            if (museSR == 3) {
                DragDorg(ref items[index], ref Main.mouseItem);
                FinalizeCraftingResult();
            }
        }
        private void UpdateHoverHighlight() {
            if (InCoordIndex < 0 || InCoordIndex >= itemHoverSengses.Length) {
                return;
            }
            for (int i = 0; i < itemHoverSengses.Length; i++) {
                if (i == InCoordIndex) {
                    if (itemHoverSengses[i] < 1f) {
                        itemHoverSengses[i] += 0.1f;
                    }
                }
                else {
                    if (itemHoverSengses[i] > 0f) {
                        itemHoverSengses[i] -= 0.1f;
                    }
                }
                itemHoverSengses[i] = MathHelper.Clamp(itemHoverSengses[i], 0, 1f);
            }
        }
        private void TryTakeResult(int museS) {
            if (!onInputP) {
                return;
            }
            player.mouseInterface = true;
            if (inputItem != null && inputItem.type > ItemID.None) {
                DragButton.DontDragTime = 2;
            }
            if (museS == 3 || museS == 1) {
                GetResult(ref inputItem, ref Main.mouseItem, ref items);
                FinalizeCraftingResult();
            }
        }
        private void TryClose(int museS) {
            if (!onCloseP) {
                return;
            }
            player.mouseInterface = true;
            if (museS == 1) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f });
                Active = false;
            }
        }
        private void ProcessSlotsInteraction(int museS, int museSR) {
            if (!hoverInMainPage) {
                return;
            }
            player.mouseInterface = true;
            if (!hoverInPutItemCellPage) {
                return;
            }
            if (museS == 1) {
                HandleLeftClickOnSlot(InCoordIndex);
            }
            HandleContinuousGather(InCoordIndex);
            HandleRightClickOnSlot(InCoordIndex, museSR);
            UpdateHoverHighlight();
        }
        #endregion

        #region 交互公用函数
        private void GetResult(ref Item onitem, ref Item holdItem, ref Item[] arg) {
            if (onitem.type != ItemID.None && StaticFullItemTypes != null) {
                if (holdItem.type == ItemID.None) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    SoundEngine.PlaySound(SoundID.Research);
                    ConsumeMaterialsForOutput();
                    holdItem = onitem;
                    onitem = new Item();
                }
                else {
                    if (holdItem.type == onitem.type && holdItem.stack < holdItem.maxStack) {
                        SoundEngine.PlaySound(SoundID.Grab);
                        SoundEngine.PlaySound(SoundID.Research);
                        ConsumeMaterialsForOutput();
                        holdItem.stack++;
                        onitem = new Item();
                    }
                }
            }
        }
        public void HandleItemClick(ref Item onitem, ref Item holdItem) {
            // 如果输入格和鼠标上的物品都为空，无需处理
            if (onitem.type == ItemID.None && holdItem.type == ItemID.None) {
                return;
            }
            // 捡起物品逻辑
            if (onitem.type != ItemID.None && holdItem.type == ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                holdItem = onitem;
                onitem = new Item();
                return;
            }
            // 同种物品堆叠逻辑
            if (onitem.type == holdItem.type && holdItem.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                //也需要注意物品的最大堆叠上限
                if (onitem.stack + holdItem.stack <= onitem.maxStack) {
                    onitem.stack += holdItem.stack;
                    holdItem = new Item();
                }
                else {
                    int fillUpNum = onitem.maxStack - onitem.stack;
                    onitem.stack = onitem.maxStack;
                    holdItem.stack -= fillUpNum;
                }
                return;
            }
            // 不同种物品交换逻辑
            if (onitem.type == ItemID.None && holdItem.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Utils.Swap(ref holdItem, ref onitem);
            }
            else {
                SoundEngine.PlaySound(SoundID.Grab);
                (holdItem, onitem) = (onitem, holdItem);
            }
        }
        public void HandleRightClick(ref Item onitem, ref Item holdItem) {
            if (onitem == null) {
                onitem = new Item();
            }
            // 如果目标格和鼠标上的物品都为空，无需处理
            if (onitem.type == ItemID.None && holdItem.type == ItemID.None) {
                return;
            }
            //如果鼠标上的物品为空但目标格上不为空，那么执行一次右键拿取的操作
            if (onitem.type != ItemID.None && holdItem.type == ItemID.None && onitem.stack > 1) {
                SoundEngine.PlaySound(SoundID.Grab);
                Item item = onitem.Clone();
                onitem.stack -= 1;
                if (onitem.stack <= 0) {
                    onitem.TurnToAir();
                }
                item.stack = 1;
                holdItem = item;
                return;
            }
            // 同种物品右键增加逻辑
            if (onitem.type == holdItem.type && holdItem.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                // 如果物品堆叠上限为1，则不进行右键增加操作
                if (onitem.maxStack == 1) {
                    return;
                }
                onitem.stack += 1;
                holdItem.stack -= 1;
                // 如果鼠标上的物品数量为0，则清空鼠标上的物品
                if (holdItem.stack == 0) {
                    holdItem = new Item();
                }
                return;
            }
            // 不同种物品交换逻辑
            if (onitem.type != holdItem.type && onitem.type != ItemID.None && holdItem.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Utils.Swap(ref holdItem, ref onitem);
                return;
            }
            // 鼠标上有物品且目标格为空物品，进行右键放置逻辑
            if (onitem.type == ItemID.None && holdItem.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                PlaceItemOnGrid(ref onitem, ref holdItem);
            }
        }
        public void DragDorg(ref Item onitem, ref Item holdItem) {
            if (onitem == null) {
                onitem = new Item();
            }
            if (onitem.type == ItemID.None && holdItem.type != ItemID.None) {
                holdItem.stack -= 1;
                Item intoItem = holdItem.Clone();
                intoItem.stack = 1;
                onitem = intoItem;
            }
        }
        private void GatheringItem(int index, ref Item holdItem) {
            if (holdItem.type == ItemID.None && items[index].type != ItemID.None) {
                for (int i = 0; i < items.Length; i++) {
                    if (index == i) {
                        continue;
                    }
                    Item value = items[i].Clone();
                    if (value.type == items[index].type) {
                        items[index].stack += value.stack;
                        items[i] = new Item();
                    }
                }
            }
        }
        private void GatheringItem2(int inCoordIndex, ref Item item) {
            if (item.type == ItemID.None && items[inCoordIndex].type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Item item1 = items[inCoordIndex].Clone();
                player.QuickSpawnItem(player.FromObjectGetParent(), item1, item1.stack);
                items[inCoordIndex] = new Item();
            }
        }
        private void PlaceItemOnGrid(ref Item onitem, ref Item holdItem) {
            Item inToItem = holdItem.Clone();
            inToItem.stack = 1;
            onitem = inToItem;
            holdItem.stack -= 1;
            if (holdItem.stack == 0) {
                holdItem = new Item();
            }
            FinalizeCraftingResult();
        }
        #endregion

        #region Update入口
        public override void Update() {
            EnsureInitialized();
            UpdateUIElementPos();
            int museS = (int)keyLeftPressState;
            int museSR = (int)keyRightPressState;
            TryClose(museS);
            ProcessSlotsInteraction(museS, museSR);
            TryTakeResult(museS);
            RecipeSidebarListView.Update();
        }
        #endregion

        #region 绘制
        public static void DrawItemIcons(SpriteBatch spriteBatch, Item item, Vector2 drawpos, Vector2 offset = default, Color drawColor = default, float alp = 1, float overSlp = 1) {
            if (item != null && item.type != ItemID.None) {
                int dyeItemID = item.CWR().DyeItemID;
                if (dyeItemID > ItemID.None) {
                    player.BeginDyeEffectForUI(dyeItemID);
                }
                Rectangle rectangle = Main.itemAnimations[item.type] != null ? Main.itemAnimations[item.type].GetFrame(TextureAssets.Item[item.type].Value) : TextureAssets.Item[item.type].Value.Frame(1, 1, 0, 0);
                Vector2 vector = rectangle.Size();
                if (offset == default) {
                    offset = new Vector2(cellWid, cellHig) / 2;
                }
                float slp = item.GetDrawItemSize(36) * overSlp;
                if (item.type == DarkMatterBall.ID) {
                    DarkMatterBall.DrawItemIcon(spriteBatch, drawpos + offset, item.type, alp);
                }
                else {
                    Texture2D itemValue = TextureAssets.Item[item.type].Value;
                    Color doDrawColor = (drawColor == default ? Color.White : drawColor) * alp;
                    spriteBatch.Draw(itemValue, drawpos + offset, rectangle, doDrawColor, 0f, vector / 2, slp, 0, 0f);
                }
                if (dyeItemID > ItemID.None) {
                    player.EndDyeEffectForUI();
                }
                if (item.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, item.stack.ToString(), drawpos.X, drawpos.Y + 25, Color.White, Color.Black, new Vector2(0.3f), overSlp);
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            RecipeSidebarListView.Draw(spriteBatch);
            spriteBatch.Draw(Texture, DrawPosition, null, Color.White * _sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(CWRUtils.GetT2DValue("CalamityMod/UI/DraedonSummoning/DecryptCancelIcon"), DrawPosition, null, Color.White * _sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (onCloseP && _sengs >= 1) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRLocText.GetTextValue("SupertableUI_Text1"), DrawPosition.X, DrawPosition.Y, Color.Gold, Color.Black, new Vector2(0.3f), 1.1f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.1f));
            }
            if (previewItems != null) {
                for (int i = 0; i < items.Length; i++) {
                    if (previewItems[i] != null) {
                        Item item = previewItems[i];
                        if (item != null) {
                            DrawItemIcons(spriteBatch, item, ArcCellPos(i), alp: 0.25f * _sengs, overSlp: 1f + itemHoverSengses[i] * 0.2f);
                        }
                    }
                }
            }
            if (items != null) {
                for (int i = 0; i < items.Length; i++) {
                    if (items[i] != null) {
                        Item item = items[i];
                        if (item != null) {
                            DrawItemIcons(spriteBatch, item, ArcCellPos(i), alp: _sengs, overSlp: 1f + itemHoverSengses[i] * 0.2f);
                        }
                    }
                }
            }
            Texture2D arrow = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/InputArrow2");
            if (inputItem != null && inputItem.type != ItemID.None) {
                DrawItemIcons(spriteBatch, inputItem, DrawPosition + new Vector2(552, 215), alp: _sengs, overSlp: 1.5f * _sengs);
                arrow = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/InputArrow");
            }
            spriteBatch.Draw(arrow, DrawPosition + new Vector2(460, 225), null, Color.White * _sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (hoverInPutItemCellPage && InCoordIndex >= 0 && InCoordIndex <= 80) {
                Item overItem = items[InCoordIndex];
                if (overItem == null) {
                    overItem = new Item();
                }
                Main.HoverItem = overItem.Clone();
                Main.hoverItemName = overItem.Name;
                if (Main.mouseItem.type == ItemID.None && items[InCoordIndex].type == ItemID.None && previewItems != null) {
                    Item previewItem = previewItems[InCoordIndex];
                    Main.HoverItem = previewItem.Clone();
                    Main.hoverItemName = previewItem.Name;
                }
            }
            if (onInputP && inputItem != null && inputItem.type != ItemID.None) {
                Main.HoverItem = inputItem.Clone();
                Main.hoverItemName = inputItem.Name;
            }
        }
        #endregion
    }

    //高亮差异显示
    internal class CraftingSlotHighlighter : UIHandle {
        public static CraftingSlotHighlighter Instance => UIHandleLoader.GetUIHandleOfType<CraftingSlotHighlighter>();
        private static SupertableUI MainUI => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/CallFull");
        [VaultLoaden("@CalamityOverhaul/Assets/UIs/SupertableUIs/Eye")] public static Asset<Texture2D> eyeAsset = null;
        public override float RenderPriority => 2;
        public override bool Active {
            get {
                if (SupertableUI.Instance == null) {
                    return false;
                }
                return SupertableUI.Instance.Active;
            }
        }
        public bool eyEBool;
        public override void Update() {
            DrawPosition = MainUI.DrawPosition + new Vector2(460, 420);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 30, 30);
            hoverInMainPage = UIHitBox.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            if (hoverInMainPage) {
                int mouseS = (int)keyLeftPressState;
                if (mouseS == 1) {
                    eyEBool = !eyEBool;
                    if (eyEBool) {
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.5f });
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.5f });
                    }
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(eyeAsset.Value, DrawPosition, eyeAsset.Value.GetRectangle(eyEBool ? 1 : 0, 2), Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (MainUI.items != null) {
                for (int i = 0; i < MainUI.items.Length; i++) {
                    if (MainUI.items[i] == null) {
                        MainUI.items[i] = new Item();
                    }
                    if (MainUI.previewItems[i] == null) {
                        MainUI.previewItems[i] = new Item();
                    }
                    if (MainUI.items[i].type != MainUI.previewItems[i].type && MainUI.items[i].type != ItemID.None && eyEBool) {
                        Vector2 pos = MainUI.ArcCellPos(i) + new Vector2(-1, 0);
                        spriteBatch.Draw(Texture, pos, null, Color.White * 0.6f * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    }
                }
            }
            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, eyEBool ? CWRLocText.GetTextValue("SupertableUI_Text4") : CWRLocText.GetTextValue("SupertableUI_Text5"), DrawPosition.X - 30, DrawPosition.Y + 30, Color.White * SupertableUI.Instance._sengs, Color.Black * SupertableUI.Instance._sengs, new Vector2(0.3f), 0.8f);
            }
        }
    }

    //拖拽按钮
    internal class DragButton : UIHandle {
        public override Texture2D Texture => CWRAsset.Placeholder_ERROR.Value;
        public static DragButton Instance => UIHandleLoader.GetUIHandleOfType<DragButton>();
        public override float RenderPriority => 0.5f;
        public override bool Active {
            get {
                if (SupertableUI.Instance == null) {
                    return false;
                }
                return SupertableUI.Instance.Active;
            }
        }
        public Vector2 SupPos => SupertableUI.Instance.DrawPosition;
        public Vector2 InSupPosOffset => new Vector2(554, 380);
        public Vector2 InPosOffsetDragToPos;
        public Vector2 DragVelocity;
        public static int DontDragTime;
        public static bool OnDrag;
        public void Initialize() {
            if (DontDragTime > 0) {
                DontDragTime--;
            }
            DrawPosition = SupertableUI.Instance.DrawPosition + InSupPosOffset;
            hoverInMainPage = SupertableUI.Instance.hoverInMainPage && DontDragTime <= 0;
            if (Main.mouseItem.type > ItemID.None && SupertableUI.Instance.hoverInPutItemCellPage) {
                DontDragTime = 2;
                OnDrag = false;
                hoverInMainPage = false;
            }
        }
        public override void Update() {
            if (SupertableUI.Instance == null) {
                return;
            }
            Initialize();
            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed && !OnDrag) {
                    OnDrag = true;
                    InPosOffsetDragToPos = DrawPosition.To(MousePosition);
                }
            }
            if (OnDrag) {
                if (keyLeftPressState == KeyPressState.Released) {
                    OnDrag = false;
                }
                DragVelocity = (DrawPosition + InPosOffsetDragToPos).To(MousePosition);
                SupertableUI.Instance.DrawPosition += DragVelocity;
            }
            else {
                DragVelocity = Vector2.Zero;
            }
            Prevention();
        }
        public void Prevention() {
            if (SupertableUI.Instance.DrawPosition.X < 0) {
                SupertableUI.Instance.DrawPosition.X = 0;
            }
            if (SupertableUI.Instance.DrawPosition.X + SupertableUI.Instance.Texture.Width > Main.screenWidth) {
                SupertableUI.Instance.DrawPosition.X = Main.screenWidth - SupertableUI.Instance.Texture.Width;
            }
            if (SupertableUI.Instance.DrawPosition.Y < 0) {
                SupertableUI.Instance.DrawPosition.Y = 0;
            }
            if (SupertableUI.Instance.DrawPosition.Y + SupertableUI.Instance.Texture.Height > Main.screenHeight) {
                SupertableUI.Instance.DrawPosition.Y = Main.screenHeight - SupertableUI.Instance.Texture.Height;
            }
        }
    }

    //一键放置/一键取出
    internal class MaterialOrganizer : UIHandle {
        protected SupertableUI mainUI => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/OneClick");
        public override float RenderPriority => 2;
        public override bool Active {
            get {
                if (SupertableUI.Instance == null) {
                    return false;
                }
                return SupertableUI.Instance.Active;
            }
        }
        protected virtual Vector2 offsetDraw => new Vector2(574, 330);
        private int useTimeCoolding;
        private int useMuse3AddCount;
        private bool checkSetO => GetType() != typeof(MaterialOrganizer);
        public override void Update() {
            DrawPosition = mainUI.DrawPosition + offsetDraw;
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 30, 30);
            hoverInMainPage = UIHitBox.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            int mouseState = (int)keyLeftPressState;
            if (mouseState != 1 && mouseState != 3) {
                useMuse3AddCount = 30;
            }
            if (hoverInMainPage) {
                if (mouseState == 1 || mouseState == 3) {
                    HandleClickEvents(mouseState);
                }
                DragButton.DontDragTime = 2;
            }
            if (useTimeCoolding > 0) {
                useTimeCoolding--;
            }
        }
        private void HandleClickEvents(int mouseState) {
            if (checkSetO) {
                bool isItemInUse = mainUI.items.Any(item => item.type != ItemID.None);
                if (isItemInUse) {
                    ClickEvent();
                }
            }
            else if (useTimeCoolding <= 0 || mouseState == 1) {
                ClickEvent();
                useTimeCoolding = useMuse3AddCount;
                AdjustMouseClickSpeed();
            }
        }
        private void AdjustMouseClickSpeed() {
            if (useMuse3AddCount == 30) {
                useMuse3AddCount = 12;
            }
            else {
                useMuse3AddCount = Math.Max(useMuse3AddCount - 1, 1);
            }
        }
        protected virtual void ClickEvent() {
            mainUI.OneClickPFunc();
            mainUI.FinalizeCraftingResult();
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Color color = Color.White;
            if (hoverInMainPage) {
                color = Color.Gold;
            }
            spriteBatch.Draw(Texture, DrawPosition, null, color * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, checkSetO ? CWRLocText.GetTextValue("SupMUI_OneClick_Text2") : CWRLocText.GetTextValue("SupMUI_OneClick_Text1"), DrawPosition.X - 30, DrawPosition.Y + 30, Color.White * SupertableUI.Instance._sengs, Color.Black * SupertableUI.Instance._sengs, new Vector2(0.3f), 0.8f);
            }
        }
    }
    internal class MaterialOrganizerLeft : MaterialOrganizer {
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/TwoClick");
        protected override Vector2 offsetDraw => new Vector2(540, 330);
        protected override void ClickEvent() {
            SoundEngine.PlaySound(SoundID.Grab);
            mainUI.TakeAllItem();
            mainUI.FinalizeCraftingResult();
        }
    }

    //RecipeData
    public class RecipeData {
        public int Target { get; set; }
        public string[] Values { get; set; }
        private int? _cachedHashCode;
        public int[] MaterialTypesCache;//缓存
        public void BuildMaterialTypesCache() {
            if (Values == null) {
                return;
            }
            if (MaterialTypesCache != null && MaterialTypesCache.Length == Values.Length) {
                return;
            }
            MaterialTypesCache = new int[Values.Length];
            for (int i = 0; i < Values.Length; i++) {
                MaterialTypesCache[i] = VaultUtils.GetItemTypeFromFullName(Values[i]);
            }
        }
        public static bool operator ==(RecipeData left, RecipeData right) {
            if (ReferenceEquals(left, right)) {
                return true;
            }
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) {
                return false;
            }
            return left.Equals(right);
        }
        public static bool operator !=(RecipeData left, RecipeData right) => !(left == right);
        public override bool Equals(object obj) {
            if (obj is not RecipeData other) {
                return false;
            }
            if (Target != other.Target || Values.Length != other.Values.Length) {
                return false;
            }
            for (int i = 0; i < Values.Length; i++) {
                if (!string.Equals(Values[i], other.Values[i])) {
                    return false;
                }
            }
            return true;
        }
        public override int GetHashCode() {
            if (_cachedHashCode.HasValue) {
                return _cachedHashCode.Value;
            }
            int hash = Target.GetHashCode();
            const int MaxHashElements = 5;
            for (int i = 0; i < Math.Min(Values.Length, MaxHashElements); i++) {
                hash = hash * 31 + (Values[i]?.GetHashCode() ?? 0);
            }
            _cachedHashCode = hash;
            return hash;
        }
    }

    //RecipeSidebarListViewUI 副栏
    internal class RecipeSidebarListViewUI : UIHandle {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public List<RecipeTargetElmt> recipeTargetElmts = [];
        private static SupertableUI supertableUI => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        internal RecipeTargetElmt TargetPecipePointer;
        internal RecipeTargetElmt PreviewTargetPecipePointer;
        internal MouseState oldMouseState;
        internal float rollerValue;
        internal float rollerSengs;
        internal int siderHeight;
        public override void Update() {
            DrawPosition = supertableUI.DrawPosition + new Vector2(supertableUI.UIHitBox.Width + 18, 8);
            for (int i = 0; i < recipeTargetElmts.Count; i++) {
                RecipeTargetElmt targetElmt = recipeTargetElmts[i];
                targetElmt.DrawPosition = DrawPosition + new Vector2(4, i * targetElmt.UIHitBox.Height - rollerValue);
                targetElmt.Update();
            }
            siderHeight = recipeTargetElmts.Count * 64 / (recipeTargetElmts.Count == 0 ? 1 : recipeTargetElmts.Count) * 7;
            MouseState currentMouseState = Mouse.GetState();
            int scrollWheelDelta = currentMouseState.ScrollWheelValue - oldMouseState.ScrollWheelValue;
            rollerValue -= scrollWheelDelta;
            rollerValue = MathHelper.Clamp(rollerValue, 64, Math.Max(64, recipeTargetElmts.Count * 64 - 64 * 4));
            rollerValue = ((int)rollerValue / 64) * 64;
            oldMouseState = currentMouseState;
            rollerSengs = (rollerValue / Math.Max(1, recipeTargetElmts.Count * 64)) * siderHeight;
            UIHitBox = new Rectangle((int)DrawPosition.X - 4, (int)DrawPosition.Y, 72, siderHeight);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            if (hoverInMainPage) {
                Terraria.GameInput.PlayerInput.LockVanillaMouseScroll(Mod.Name + "/" + GetType().Name);
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, 70, siderHeight, Color.AliceBlue * 0.8f * SupertableUI.Instance._sengs, Color.Azure * 0, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, 70, siderHeight, Color.AliceBlue * 0, Color.Azure * 1 * SupertableUI.Instance._sengs, 1);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);
            Rectangle originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle newScissorRect = VaultUtils.GetClippingRectangle(spriteBatch, UIHitBox);
            spriteBatch.GraphicsDevice.ScissorRectangle = newScissorRect;
            for (int i = 0; i < recipeTargetElmts.Count; i++) {
                recipeTargetElmts[i].Draw(spriteBatch);
            }
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);
        }
    }

    //配方元素
    internal class RecipeTargetElmt : UIHandle {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal RecipeData recipeData;
        private int borderedWidth;
        private int borderedHeight;
        private float borderedSize;
        private Color backColor = Color.Azure * 0.2f;
        private static RecipeSidebarListViewUI recipeSidebarListView => UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 64, 64);
            borderedHeight = borderedWidth = 64;
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, borderedWidth, borderedHeight);
            Rectangle mouseRec = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = mouseRec.Intersects(UIHitBox) && mouseRec.Intersects(UIHandleLoader.GetUIHandleInstance<RecipeSidebarListViewUI>().UIHitBox);
            float targetSize = 1f;
            Color targetColor = Color.Azure * 0.2f;
            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (recipeSidebarListView.PreviewTargetPecipePointer != this) {
                    SoundStyle sound = SoundID.Grab with { Pitch = -0.6f, Volume = 0.4f };
                    SoundEngine.PlaySound(sound);
                    recipeSidebarListView.PreviewTargetPecipePointer = this;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (recipeSidebarListView.TargetPecipePointer != this) {
                        recipeSidebarListView.TargetPecipePointer = this;
                        SoundStyle sound = SoundID.Grab with { Pitch = 0.6f, Volume = 0.8f };
                        SoundEngine.PlaySound(sound);
                        for (int i = 0; i < SupertableUI.AllRecipes.Count; i++) {
                            if (recipeData == SupertableUI.AllRecipes[i]) {
                                RecipeUI.Instance.index = i;
                            }
                        }
                    }
                }
                Item item = new Item(recipeData.Target);
                if (item != null && item.type > ItemID.None) {
                    CWRUI.HoverItem = item;
                    CWRUI.DontSetHoverItem = true;
                }
                targetSize = 1.2f;
                targetColor = Color.LightGoldenrodYellow;
            }
            if (recipeSidebarListView.TargetPecipePointer == this) {
                targetSize = 1.2f;
                targetColor = Color.Gold;
            }
            backColor = Color.Lerp(backColor, targetColor, 0.1f);
            borderedSize = MathHelper.Lerp(borderedSize, targetSize, 0.1f);
            borderedSize = MathHelper.Clamp(borderedSize, 1, 1.2f);
        }
        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, borderedWidth, borderedHeight, Color.AliceBlue * 0.8f * SupertableUI.Instance._sengs, backColor * SupertableUI.Instance._sengs, borderedSize);
            Item item = new Item(recipeData.Target);
            if (item.type > ItemID.None) {
                float drawSize = VaultUtils.GetDrawItemSize(item, borderedWidth) * borderedSize;
                Vector2 drawPos = DrawPosition + new Vector2(borderedWidth, borderedHeight) / 2f;
                VaultUtils.SimpleDrawItem(spriteBatch, item.type, drawPos, drawSize, 0, Color.White * SupertableUI.Instance._sengs);
            }
        }
    }

    //配方翻页UI
    internal class RecipeUI : UIHandle, ICWRLoader {
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/RecPBook");
        public override float RenderPriority => 2;
        public override bool Active {
            get {
                if (SupertableUI.Instance == null) {
                    return false;
                }
                return SupertableUI.Instance.Active;
            }
        }
        public static RecipeUI Instance { get; private set; }
        private static List<Item> itemTarget = [];
        private static List<string[]> itemNameString_FormulaContent_Values = [];
        private Rectangle mainRec;
        private Rectangle rAow;
        private Rectangle lAow;
        public int index;
        private bool onM;
        private bool onR;
        private bool onL;
        public override void Load() {
            Instance = this;
        }
        void ICWRLoader.UnLoadData() {
            Instance = null;
        }
        public static void LoadAllRecipes() {
            itemTarget.Clear();
            itemNameString_FormulaContent_Values.Clear();
            for (int i = 0; i < SupertableUI.AllRecipes.Count; i++) {
                itemTarget.Add(new Item(SupertableUI.AllRecipes[i].Target));
                itemNameString_FormulaContent_Values.Add(SupertableUI.AllRecipes[i].Values);
            }
        }
        private void Initialize() {
            if (SupertableUI.Instance != null) {
                DrawPosition = SupertableUI.Instance.DrawPosition + new Vector2(545, 80);
            }
            mainRec = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Texture.Width, Texture.Height);
            rAow = new Rectangle((int)DrawPosition.X + 62, (int)DrawPosition.Y + 20, 25, 25);
            lAow = new Rectangle((int)DrawPosition.X - 30, (int)DrawPosition.Y + 20, 25, 25);
            onM = mainRec.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            onR = rAow.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            onL = lAow.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
        }
        public override void Update() {
            Initialize();
            if (onR || onL || onM) {
                DragButton.DontDragTime = 2;
            }
            int museS = (int)keyLeftPressState;
            if (museS == 1) {
                RecipeSidebarListViewUI recipeSidebarListView = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
                if (onR) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = 0.5f });
                    index += 1;
                    recipeSidebarListView.rollerValue += 64;
                    DragButton.DontDragTime = 2;
                }
                if (onL) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = -0.5f });
                    index -= 1;
                    recipeSidebarListView.rollerValue -= 64;
                    DragButton.DontDragTime = 2;
                }
                if (onM) {
                    DragButton.DontDragTime = 2;
                }
                if (index < 0) {
                    index = itemTarget.Count - 1;
                    recipeSidebarListView.rollerValue = recipeSidebarListView.recipeTargetElmts.Count * 64;
                }
                if (index > itemTarget.Count - 1) {
                    index = 0;
                    recipeSidebarListView.rollerValue = 0;
                }
                RecipeTargetElmt elmt = null;
                foreach (RecipeTargetElmt folwerElmt in recipeSidebarListView.recipeTargetElmts) {
                    if (folwerElmt.recipeData == SupertableUI.AllRecipes[index]) {
                        elmt = folwerElmt;
                    }
                }
                if (elmt != null) {
                    recipeSidebarListView.TargetPecipePointer = elmt;
                }
                LoadPreviewItems();
                if (SupertableUI.Instance.inputItem == null) {
                    SupertableUI.Instance.inputItem = new Item();
                }
                if (SupertableUI.Instance.inputItem.type != ItemID.None && SupertableUI.Instance.StaticFullItemNames != null) {
                    for (int i = 0; i < itemNameString_FormulaContent_Values.Count; i++) {
                        string[] formulaContent_Values = itemNameString_FormulaContent_Values[i];
                        bool match = true;
                        for (int j = 0; j < 80; j++) {
                            if (formulaContent_Values[j] != SupertableUI.Instance.StaticFullItemNames[j]) {
                                match = false;
                                break;
                            }
                        }
                        if (match) {
                            index = i;
                            LoadPreviewItems();
                            break;
                        }
                    }
                }
            }
        }
        public void LoadPreviewItems() {
            if (SupertableUI.Instance != null) {
                if (SupertableUI.Instance.previewItems == null) {
                    SupertableUI.Instance.previewItems = new Item[81];
                }
                if (SupertableUI.Instance.items == null) {
                    SupertableUI.Instance.items = new Item[81];
                }
                SupertableUI.Instance.previewItems = new Item[SupertableUI.Instance.items.Length];
                string[] names = itemNameString_FormulaContent_Values[index];
                if (names != null) {
                    for (int i = 0; i < 81; i++) {
                        string value = names[i];
                        SupertableUI.Instance.previewItems[i] = new Item(VaultUtils.GetItemTypeFromFullName(value, true));
                    }
                }
                else {
                    for (int i = 0; i < 81; i++) {
                        SupertableUI.Instance.previewItems[i] = new Item();
                    }
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D arow = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BlueArrow");
            Texture2D arow2 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BlueArrow2");
            spriteBatch.Draw(Texture, DrawPosition, null, Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(onR ? arow : arow2, DrawPosition + new Vector2(62, 20), null, Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(onL ? arow : arow2, DrawPosition + new Vector2(-30, 20), null, Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);
            string text2 = $"{index + 1} -:- {itemTarget.Count}";
            Vector2 text2Size = FontAssets.MouseText.Value.MeasureString(text2);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text2, DrawPosition.X - text2Size.X / 2 + Texture.Width / 2, DrawPosition.Y + 65, Color.White * SupertableUI.Instance._sengs, Color.Black * SupertableUI.Instance._sengs, new Vector2(0.3f), 0.8f);
            if (itemTarget != null && SupertableUI.Instance != null && index >= 0 && index < itemTarget.Count) {
                SupertableUI.DrawItemIcons(spriteBatch, itemTarget[index], DrawPosition + new Vector2(5, 5), alp: 0.6f * SupertableUI.Instance._sengs, overSlp: 1.5f * SupertableUI.Instance._sengs);
                string name = itemTarget[index].HoverName;
                string text = $"{CWRLocText.GetTextValue("SupertableUI_Text2")}：{(name == "" ? CWRLocText.GetTextValue("SupertableUI_Text3") : name)}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, DrawPosition.X - textSize.X / 2 + Texture.Width / 2, DrawPosition.Y - 25, Color.White * SupertableUI.Instance._sengs, Color.Black * SupertableUI.Instance._sengs, new Vector2(0.3f), 0.8f);
            }
            if (onM) {
                Item overItem = itemTarget[index];
                if (overItem != null && overItem.type != ItemID.None) {
                    Main.HoverItem = overItem.Clone();
                    Main.hoverItemName = overItem.Name;
                }
            }
        }
    }

    //预览浮出提示启动器
    internal class SynthesisPreviewStart : UIHandle {
        internal static SynthesisPreviewStart Instance { get; private set; }
        private bool oldLeftCtrlPressed;
        private static Vector2 origPos => SynthesisPreviewUI.Instance.DrawPosition;
        private Vector2 offset;
        internal float _sengs;
        internal bool uiIsActive => !SupertableUI.Instance.hoverInMainPage && CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type] != null;
        public override bool Active => _sengs > 0 || uiIsActive;
        public override void Load() {
            Instance = this;
            Instance.DrawPosition = new Vector2(700, 100);
        }
        public override void Update() {
            if (uiIsActive) {
                if (_sengs < 1f) {
                    _sengs += 0.1f;
                }
            }
            else {
                if (_sengs > 0f) {
                    _sengs -= 0.1f;
                }
            }
            _sengs = MathHelper.Clamp(_sengs, 0, 1);
            bool leftCtrlPressed = Main.keyState.IsKeyDown(Keys.L);
            if (leftCtrlPressed && !oldLeftCtrlPressed) {
                SoundEngine.PlaySound(SoundID.Chat);
                SynthesisPreviewUI.Instance.DrawBool = !SynthesisPreviewUI.Instance.DrawBool;
            }
            oldLeftCtrlPressed = leftCtrlPressed;
            if (!SynthesisPreviewUI.Instance.DrawBool) {
                SynthesisPreviewUI.Instance.SetPosition();
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (SynthesisPreviewUI.Instance.DrawPosition == Vector2.Zero) {
                SynthesisPreviewUI.Instance.DrawPosition = new Vector2(700, 100);
            }
            DrawPosition = origPos + offset;
            if (SynthesisPreviewUI.Instance.DrawBool) {
                if (offset.Y > -30) {
                    offset.Y -= 5;
                }
            }
            else if (offset.Y < 0) {
                offset.Y += 5;
            }
            string text = CWRLocText.GetTextValue("MouseTextContactPanel_TextContent");
            text = text.Replace("[KEY]", "L");
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, (int)(size.X + 22 * _sengs), (int)(size.Y * _sengs), Color.BlueViolet * 0.8f * _sengs, Color.Azure * 1.2f * _sengs, 0.8f + _sengs * 0.2f);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, DrawPosition.X + 3, DrawPosition.Y + 3, Color.White * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);
        }
    }

    //预览UI
    public class SynthesisPreviewUI : UIHandle, ICWRLoader {
        public static SynthesisPreviewUI Instance => UIHandleLoader.GetUIHandleOfType<SynthesisPreviewUI>();
        internal static int Width => 564;
        internal static int Height => 564;
        [VaultLoaden(CWRConstant.UI + "SupertableUIs/MainValueInCell")] internal static Asset<Texture2D> MainValueInCell = null;
        internal string[] OmigaSnyContent = [];
        internal float _sengs;
        internal bool humerdFrame;
        internal bool DrawBool;
        internal bool uiIsActive => DrawBool && !SupertableUI.Instance.hoverInMainPage && CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type] != null;
        public override bool Active => _sengs > 0 || uiIsActive;
        void ICWRLoader.UnLoadData() {
            OmigaSnyContent = [];
        }
        public Vector2 ArcCellPos(int index, Vector2 pos) {
            int y = index / 9;
            int x = index - (y * 9);
            return (new Vector2(x, y) * new Vector2(48, 46)) + pos;
        }
        public Vector2 Prevention(Vector2 pos) {
            float maxW = Width;
            float maxH = Height;
            if (pos.X < 0) { pos.X = 0; }
            if (pos.X + maxW > Main.screenWidth) { pos.X = Main.screenWidth - maxW; }
            if (pos.Y < 0) { pos.Y = 0; }
            if (pos.Y + maxH > Main.screenHeight) { pos.Y = Main.screenHeight - maxH; }
            return pos;
        }
        public void SetPosition() {
            DrawPosition = new Vector2(580, 100);
            DrawPosition = Prevention(DrawPosition);
            if (SupertableUI.Instance.Active) {
                RecipeSidebarListViewUI recipeSidebarListViewUI = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
                DrawPosition = recipeSidebarListViewUI.DrawPosition + new Vector2(64, 0);
            }
        }
        private void SetOmigaSnyContent(string[] omigaSnyContent) {
            if (omigaSnyContent == null) { return; }
            OmigaSnyContent = omigaSnyContent;
            if (Main.GameUpdateCount % 60 == 0) { humerdFrame = !humerdFrame; }
            if (!humerdFrame) { return; }
            if (CWRUI.HoverItem.type == ModContent.ItemType<DarkMatterBall>()) { OmigaSnyContent = SupertableRecipeDate.FullItems_DarkMatterBall2; }
        }
        public override void Update() {
            if (uiIsActive) { if (_sengs < 1f) { _sengs += 0.1f; } }
            else { if (_sengs > 0f) { _sengs -= 0.1f; } }
            _sengs = MathHelper.Clamp(_sengs, 0, 1);
            SetPosition();
            OmigaSnyContent = SupertableRecipeDate.FullItems_Null;
            if (CWRUI.HoverItem.type > ItemID.None) { SetOmigaSnyContent(CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type]); }
            RecipeSidebarListViewUI recipeSidebarListView = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
            if (recipeSidebarListView.hoverInMainPage && recipeSidebarListView.PreviewTargetPecipePointer != null) { OmigaSnyContent = recipeSidebarListView.PreviewTargetPecipePointer.recipeData.Values; }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (!SupertableUI.Instance.Active) {
                SupertableUI.Instance.hoverInMainPage = false;
                SupertableUI.Instance.hoverInPutItemCellPage = false;
                SupertableUI.Instance.onInputP = false;
                SupertableUI.Instance.onCloseP = false;
            }
            Vector2 offset = new Vector2(90, 100);
            Item[] items = new Item[OmigaSnyContent.Length];
            Item targetItem = SupertableUI.InStrGetItem(OmigaSnyContent[^1], true);
            for (int i = 0; i < OmigaSnyContent.Length - 1; i++) {
                string name = OmigaSnyContent[i];
                Item item = SupertableUI.InStrGetItem(name, true);
                items[i] = item;
            }
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, (int)(Width * _sengs), (int)(Height * _sengs), Color.BlueViolet * 0.8f * _sengs, Color.Azure * 0.2f * _sengs, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_White.Value, 4, DrawPosition, (int)(Width * _sengs), (int)(Height * _sengs), Color.BlueViolet * 0 * _sengs, Color.CadetBlue * 0.6f * _sengs, 1);
            spriteBatch.Draw(MainValueInCell.Value, DrawPosition + new Vector2(-25, -25) + offset * _sengs, null, Color.White * 0.8f * _sengs, 0, Vector2.Zero, _sengs, SpriteEffects.None, 0);
            Vector2 drawTOMItemIconPos = DrawPosition + new Vector2(-20 * _sengs, MainValueInCell.Value.Height * _sengs + 10) + offset;
            VaultUtils.SimpleDrawItem(spriteBatch, ModContent.ItemType<TransmutationOfMatterItem>(), drawTOMItemIconPos, 1 * _sengs, 0, Color.White * _sengs);
            Vector2 drawText1 = new Vector2(DrawPosition.X - 20 * _sengs, DrawPosition.Y - 60 * _sengs) + offset;
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, $"{CWRLocText.GetTextValue("SPU_Text0") + VaultUtils.GetLocalizedItemName<TransmutationOfMatterItem>() + CWRLocText.GetTextValue("SPU_Text1")}：", drawText1.X, drawText1.Y, Color.White * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);
            if (targetItem != null && targetItem.type > ItemID.None && targetItem.CWR().OmigaSnyContent != null && _sengs >= 1) {
                Vector2 drawText2 = new Vector2(DrawPosition.X + 16 * _sengs, DrawPosition.Y + 420 * _sengs) + offset;
                string text = $"{CWRLocText.GetTextValue("SPU_Text2") + VaultUtils.GetLocalizedItemName(targetItem.type)}";
                Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, drawText2.X, drawText2.Y, Color.White * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);
                Vector2 drawItemPos = drawText2 + new Vector2(size.X + 20 * _sengs, 8);
                SupertableUI.DrawItemIcons(spriteBatch, targetItem, drawItemPos, new Vector2(0.0001f, 0.0001f), Color.White * _sengs);
                if (targetItem.type == ModContent.ItemType<InfiniteToiletItem>()) {
                    text = CWRLocText.GetTextValue("OnlyZenith");
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, drawText2.X, drawText2.Y + size.Y, Color.Coral * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);
                }
            }
            if (_sengs < 1) { return; }
            for (int i = 0; i < items.Length - 1; i++) {
                if (items[i] == null) { continue; }
                Item item = items[i];
                if (item == null) { continue; }
                SupertableUI.DrawItemIcons(spriteBatch, item, ArcCellPos(i, DrawPosition + offset), new Vector2(0.0001f, 0.0001f), Color.White * 0.9f * _sengs);
            }
        }
    }
}