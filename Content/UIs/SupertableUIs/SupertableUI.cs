using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using CalamityOverhaul.Content.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    public class SupertableUI : UIHandle, ICWRLoader
    {
        #region Data
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MainValue");
        public static SupertableUI Instance => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        private static RecipeSidebarListViewUI RecipeSidebarListView;
        public static string[][] RpsDataStringArrays { get; set; } = [];
        public static List<string[]> OtherRpsData_ZenithWorld_StringList { get; set; } = [];
        public static List<string[]> ModCall_OtherRpsData_StringList { get; set; } = [];
        public static List<RecipeData> AllRecipes { get; set; } = [];//全部配方
        public static readonly float[] itemHoverSengses = new float[maxCellNumX * maxCellNumY];//格子悬停感应插值
        public const int cellWid = 48;
        public const int cellHig = 46;
        public const int maxCellNumX = 9;
        public const int maxCellNumY = 9;
        public const int SlotCount = maxCellNumX * maxCellNumY; //81
        private const int RecipeDataLength = 82; //材料81+结果1
        public static int AllRecipesVanillaContentCount { get; private set; }//基础配方数量
        public static TramModuleTP TramTP { get; set; }
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
            Type type = typeof(SupertableRecipeData);
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

        #region 状态更新
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

        #region 配方匹配与结果
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

        #region TramModule同步
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
            if (Main.keyState.PressingShift()) {
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
            //如果输入格和鼠标上的物品都为空，无需处理
            if (onitem.type == ItemID.None && holdItem.type == ItemID.None) {
                return;
            }
            //捡起物品逻辑
            if (onitem.type != ItemID.None && holdItem.type == ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                holdItem = onitem;
                onitem = new Item();
                return;
            }
            //同种物品堆叠逻辑
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
            //不同种物品交换逻辑
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
            //如果目标格和鼠标上的物品都为空，无需处理
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
            //同种物品右键增加逻辑
            if (onitem.type == holdItem.type && holdItem.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                //如果物品堆叠上限为1，则不进行右键增加操作
                if (onitem.maxStack == 1) {
                    return;
                }
                onitem.stack += 1;
                holdItem.stack -= 1;
                //如果鼠标上的物品数量为0，则清空鼠标上的物品
                if (holdItem.stack == 0) {
                    holdItem = new Item();
                }
                return;
            }
            //不同种物品交换逻辑
            if (onitem.type != holdItem.type && onitem.type != ItemID.None && holdItem.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Utils.Swap(ref holdItem, ref onitem);
                return;
            }
            //鼠标上有物品且目标格为空物品，进行右键放置逻辑
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
}