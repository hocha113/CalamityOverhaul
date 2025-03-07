using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
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

        public static string[][] RpsDataStringArrays;

        public static List<string[]> OtherRpsData_ZenithWorld_StringList = [];

        public static List<string[]> ModCall_OtherRpsData_StringList = [];

        public static List<RecipeData> AllRecipes = [];

        public string[] StaticFullItemNames;

        public int[] StaticFullItemTypes;

        private int[] fullItemTypes;

        public Item[] items;

        public Item[] previewItems;

        public Item inputItem;
        /// <summary>
        /// 物品放置格子的面板矩形
        /// </summary>
        private Rectangle PutItemCellRec;

        public Rectangle inputRec;

        public Rectangle closeRec;

        public Vector2 topLeft => DrawPosition + new Vector2(16, 30);

        public const int cellWid = 48;

        public const int cellHig = 46;

        public const int maxCellNumX = 9;

        public const int maxCellNumY = 9;
        /// <summary>
        /// 配方表最大数量，不包括天顶世界的特殊配方内容
        /// </summary>
        public static int AllRecipesVanillaContentCount;

        public static TramModuleTP tramModuleEntity;

        private Point mouseInCellCoord;

        private int inCoordIndex => (mouseInCellCoord.Y * maxCellNumX) + mouseInCellCoord.X;

        internal float _sengs;

        internal int downSengsTime;

        public override bool Active {
            get => player.CWR().SupertableUIStartBool || _sengs > 0;
            set {
                player.CWR().SupertableUIStartBool = value;
                tpEntityLoadenItems();
            }
        }

        public bool hoverInPutItemCellPage;

        public bool onInputP;

        public bool onCloseP;
        #endregion

        internal void tpEntityLoadenItems() {
            if (tramModuleEntity != null && tramModuleEntity.Active) {
                if (tramModuleEntity.items == null) {
                    tramModuleEntity.items = items;
                }
                else {
                    items = tramModuleEntity.items;
                }

                if (!VaultUtils.isSinglePlayer) {
                    tramModuleEntity.SendData();
                }
            }
        }

        void ICWRLoader.SetupData() {
            LoadRecipe();
            RecipeUI.LoadAllRecipes();
        }

        void ICWRLoader.UnLoadData() {
            tramModuleEntity = null;
            RpsDataStringArrays = null;
            ModCall_OtherRpsData_StringList = [];
        }

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

            //不要新new一个实例，提醒自己，使用UIHandleLoader的实例
            RecipeSidebarListView = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
            RecipeSidebarListView.recipeTargetElmts = new List<RecipeTargetElmt>();

            foreach (string[] value in RpsDataStringArrays) {
                // 检查数组长度
                if (value.Length != 82) {
                    string pag = string.Join(", ", value);
                    throw new InvalidOperationException($"Invalid length: {pag} Length is not 82.");
                }

                string targetItemFullName = value[^1];
                int targetItemID = VaultUtils.GetItemTypeFromFullName(targetItemFullName);

                // 检查目标合成结果是否有效
                if (targetItemFullName != "Null/Null" && targetItemID == ItemID.None) {
                    string pag = string.Join(", ", value);
                    throw new InvalidOperationException($"Invalid target item: {pag} The result of the Target " +
                        $"synthesis is a None item, but the key you gave is not a \"Null/Null\" placeholder.");
                }

                // 处理 Recipe 数据
                RecipeData recipeData = new RecipeData {
                    Values = value,
                    Target = targetItemID,
                };
                AllRecipes.Add(recipeData);

                RecipeTargetElmt recipeTargetElmt = new RecipeTargetElmt { recipeData = recipeData };
                RecipeSidebarListView.recipeTargetElmts.Add(recipeTargetElmt);
            }

            AllRecipesVanillaContentCount = AllRecipes.Count;
            CWRMod.Instance.Logger.Info($"Get the recipe table capacity: {AllRecipesVanillaContentCount}");
        }

        public void UpdateUIElementPos() {
            if (player.CWR().SupertableUIStartBool && downSengsTime <= 0) {
                if (_sengs < 1f) {
                    _sengs = 1f;
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

            Vector2 inUIMousePos = MousePosition - topLeft;
            int mouseXGrid = (int)(inUIMousePos.X / cellWid);
            int mouseYGrid = (int)(inUIMousePos.Y / cellHig);
            mouseInCellCoord = new Point(mouseXGrid, mouseYGrid);
            UIHitBox = new Rectangle((int)topLeft.X, (int)topLeft.Y, cellWid * maxCellNumX + 200, cellHig * maxCellNumY);
            PutItemCellRec = new Rectangle((int)topLeft.X, (int)topLeft.Y, cellWid * maxCellNumX, cellHig * maxCellNumY);
            inputRec = new Rectangle((int)(DrawPosition.X + 555), (int)(DrawPosition.Y + 215), 92, 90);
            closeRec = new Rectangle((int)(DrawPosition.X), (int)(DrawPosition.Y), 30, 30);
            Rectangle mouseRec = MouseHitBox;
            hoverInMainPage = UIHitBox.Intersects(mouseRec);
            hoverInPutItemCellPage = PutItemCellRec.Intersects(mouseRec);
            onInputP = inputRec.Intersects(mouseRec);
            onCloseP = closeRec.Intersects(mouseRec);
        }

        public void Initialize() {
            UpdateUIElementPos();

            if (items == null) {
                items = new Item[maxCellNumX * maxCellNumY];
                for (int i = 0; i < items.Length; i++) {
                    items[i] = new Item();
                }
            }

            if (fullItemTypes == null || fullItemTypes?.Length != items.Length) {
                fullItemTypes = new int[items.Length];
                FullItem(SupertableRecipeDate.FullItems_Null);
            }

            inputItem ??= new Item();
        }

        public static void LoadenWorld() {
            if (AllRecipes.Count > AllRecipesVanillaContentCount) {
                AllRecipes.RemoveRange(AllRecipesVanillaContentCount, AllRecipes.Count - AllRecipesVanillaContentCount);
            }
            SetZenithWorldRecipesData();
            RecipeUI.LoadAllRecipes();
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

        public static void SetZenithWorldRecipesData() {
            if (Main.zenithWorld) {
                foreach (var recipes in OtherRpsData_ZenithWorld_StringList) {
                    RecipeData recipeData = new RecipeData {
                        Values = recipes,
                        Target = VaultUtils.GetItemTypeFromFullName(recipes[recipes.Length - 1])
                    };
                    AllRecipes.Add(recipeData);
                }
            }
        }

        public override void Update() {
            Initialize();

            int museS = (int)keyLeftPressState;
            int museSR = (int)keyRightPressState;
            if (onCloseP) {
                player.mouseInterface = true;
                if (museS == 1) {
                    SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
                    Active = false;
                }
            }
            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (hoverInPutItemCellPage) {
                    if (museS == 1) {
                        if (items[inCoordIndex] == null) {
                            items[inCoordIndex] = new Item();
                        }
                        KeyboardState state = Keyboard.GetState();
                        if (state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift)) {
                            GatheringItem2(inCoordIndex, ref Main.mouseItem);
                        }
                        else {
                            HandleItemClick(ref items[inCoordIndex], ref Main.mouseItem);
                        }
                        FinalizeCraftingResult();
                    }

                    if (CWRKeySystem.TOM_GatheringItem.Current) {
                        GatheringItem(inCoordIndex, ref Main.mouseItem);
                        FinalizeCraftingResult();
                    }

                    if (museSR == 1) {
                        HandleRightClick(ref items[inCoordIndex], ref Main.mouseItem);
                        FinalizeCraftingResult();
                    }

                    if (museSR == 3) {
                        DragDorg(ref items[inCoordIndex], ref Main.mouseItem);
                        FinalizeCraftingResult();
                    }
                }
            }

            if (onInputP) {
                player.mouseInterface = true;
                if (inputItem != null && inputItem.type > ItemID.None) {
                    DragButton.DontDragTime = 2;
                }
                if (museS == 3 || museS == 1) {
                    GetResult(ref inputItem, ref Main.mouseItem, ref items);
                    FinalizeCraftingResult();
                }
            }

            RecipeSidebarListView.Update();
        }

        /// <summary>
        /// 解析字符串键并获取对应的物品实例
        /// </summary>
        /// <param name="key">用于解析的字符串键，可以是整数类型或模组/物品名称的组合</param>
        /// <returns>解析后得到的物品类型</returns>
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
            else {
                string[] fruits = key.Split('/');
                return ModLoader.GetMod(fruits[0]).Find<ModItem>(fruits[1]).Item;
            }
        }

        /// <summary>
        /// 将字符串数组中的每个键转换为对应的物品类型，并返回结果数组
        /// </summary>
        /// <param name="arg">要转换的字符串数组，每个元素可以是整数类型或模组/物品名称的组合</param>
        /// <returns>包含每个字符串键对应的物品类型的数组</returns>
        public static int[] FullItem(string[] arg) {
            int[] toValueTypes = new int[arg.Length];
            for (int i = 0; i < arg.Length; i++) {
                string value = arg[i];
                toValueTypes[i] = VaultUtils.GetItemTypeFromFullName(value);
            }
            return toValueTypes;
        }

        /// <summary>
        /// 在只利用一个数字索引的情况下反向计算出对应的格坐标
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vector2 ArcCellPos(int index) {
            int y = index / maxCellNumX;
            int x = index - (y * maxCellNumX);
            return (new Vector2(x, y) * new Vector2(cellWid, cellHig)) + topLeft;
        }

        /// <summary>
        /// 重置输入物品
        /// </summary>
        private void ResetInputItem() {
            if (inputItem == null || inputItem.type != ItemID.None) {
                inputItem = new Item();
            }
        }

        /// <summary>
        /// 进行输出制作结果的操作，应当注意他的使用方式防止造成不必要的性能浪费
        /// </summary>
        public void FinalizeCraftingResult(bool netWork = true) {
            foreach (RecipeData data in AllRecipes) {
                string[] arg = data.Values;
                fullItemTypes = FullItem(arg);
                if (items.Length != fullItemTypes.Length - 1) {//如果预装填的物品ID集合的长度对不上物品集合，那么就直接重置
                    ResetInputItem();
                    goto End;
                }

                for (int i = 0; i < fullItemTypes.Length - 1; i++) {//进行一次装填检测，检测材料是否摆放正确
                    if (items?[i]?.type != fullItemTypes[i]) {
                        ResetInputItem();
                        goto End;
                    }
                }

                Item item = new Item(fullItemTypes[fullItemTypes.Length - 1]);//获取预装填集合的末尾物品，末尾物品就是输出结果
                if (item != null && item.type != ItemID.None && item.CWR().isInfiniteItem) {//如果这个物品是会湮灭的无尽物品，将其稳定性设置为稳定，即不发生湮灭
                    item.CWR().NoDestruct = true;
                    item.CWR().destructTime = 10;
                }

                int minNum = int.MaxValue;
                foreach (var value in items) {
                    if (value.type == ItemID.None) {
                        continue;
                    }

                    if (value.stack < minNum) {
                        minNum = value.stack;
                    }
                }

                item.stack = minNum;
                if (item.stack > item.maxStack) {
                    item.stack = item.maxStack;
                }

                inputItem = item;

                string[] names = new string[fullItemTypes.Length];
                for (int i = 0; i < fullItemTypes.Length; i++) {
                    Item fullItem = new Item(fullItemTypes[i]);
                    names[i] = fullItem.ModItem == null ? fullItem.type.ToString() : fullItem.ModItem.FullName;
                }
                StaticFullItemNames = names;
                StaticFullItemTypes = fullItemTypes;

                break;
End:;
            }
            if (netWork) {
                tpEntityLoadenItems();
            }
        }

        /// <summary>
        /// 一键拿取所有合成UI中的物品
        /// </summary>
        public void TakeAllItem() {
            foreach (var item in items) {
                if (item == null)
                    continue;
                Item item1 = item.Clone();
                player.QuickSpawnItem(player.FromObjectGetParent(), item1, item1.stack);
                item.TurnToAir();
            }
        }

        /// <summary>
        /// 一键放置配方物品
        /// </summary>
        public void OneClickPFunc() {
            bool onSound = false;
            if (previewItems != null && previewItems?.Length == items.Length) {
                for (int i = 0; i < items.Length; i++) {
                    Item preItem2 = items[i];
                    if (preItem2 == null) {
                        preItem2 = new Item();
                    }
                }

                for (int i = 0; i < previewItems.Length; i++) {
                    Item preItem = previewItems[i];
                    //此处加上对玩家鼠标上的物品的检测和放置
                    if (preItem.type == Main.mouseItem.type && preItem.type != ItemID.None) {
                        Item targetItem = Main.mouseItem.Clone();
                        targetItem.stack = 1;
                        items[i] = targetItem;
                        onSound = true;
                        Main.mouseItem.stack -= 1;
                        if (Main.mouseItem.stack == 0) {
                            Main.mouseItem.TurnToAir();
                        }
                        goto End;
                    }
                    //接着，如果玩家鼠标上是空或者鼠标上没有目标物品，那么再遍历玩家背包内容
                    foreach (var backItem in player.inventory) {
                        if (preItem.type == backItem.type && preItem.type != ItemID.None) {
                            Item targetItem = backItem.Clone();

                            if (items[i].type == ItemID.None) {
                                targetItem.stack = 1;
                                items[i] = targetItem;
                            }
                            else {
                                items[i].stack++;
                                if (items[i].stack > items[i].maxStack) {
                                    items[i].stack = items[i].maxStack;
                                    goto End;
                                }
                            }
                            onSound = true;
                            backItem.stack -= 1;
                            if (backItem.stack == 0) {
                                backItem.TurnToAir();
                            }
                            goto End;
                        }
                    }
End:;
                }

                if (onSound) {
                    SoundEngine.PlaySound(SoundID.Grab);
                }
            }
        }

        /// <summary>
        /// 处理获取结果的逻辑，将 onitem 转移到 holdItem 中，并根据配方数据更新物品槽
        /// </summary>
        /// <param name="onitem">被点击的物品槽</param>
        /// <param name="holdItem">正在拖拽的物品</param>
        /// <param name="arg">用于配方的字符串数组</param>
        private void GetResult(ref Item onitem, ref Item holdItem, ref Item[] arg) {
            if (onitem.type != ItemID.None && StaticFullItemTypes != null) {
                if (holdItem.type == ItemID.None) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    SoundEngine.PlaySound(SoundID.Research);
                    for (int i = 0; i < items.Length; i++) {
                        if (items[i].type == StaticFullItemTypes[i]) {
                            items[i].stack -= inputItem.stack;
                            if (items[i].stack <= 0) {
                                items[i] = new Item();
                            }
                        }
                    }

                    holdItem = onitem;
                    onitem = new Item();
                }
                else {
                    if (holdItem.type == onitem.type && holdItem.stack < holdItem.maxStack) {
                        SoundEngine.PlaySound(SoundID.Grab);
                        SoundEngine.PlaySound(SoundID.Research);
                        for (int i = 0; i < items.Length; i++) {
                            if (items[i].type == StaticFullItemTypes[i]) {
                                items[i].stack -= inputItem.stack;
                                if (items[i].stack <= 0) {
                                    items[i] = new Item();
                                }
                            }
                        }

                        holdItem.stack++;
                        onitem = new Item();
                    }
                }
            }
        }

        /// <summary>
        /// 处理输入格子的点击事件，负责处理物品的交互和堆叠逻辑
        /// </summary>
        /// <param name="onitem">输入格的物品状态</param>
        /// <param name="holdItem">鼠标上的物品</param>
        /// <param name="mouseS">点击状态</param>
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
                // 不同种物品交换逻辑
                SoundEngine.PlaySound(SoundID.Grab);
                (holdItem, onitem) = (onitem, holdItem);
            }
        }

        /// <summary>
        /// 处理右键点击事件，用于在物品格之间进行右键交互
        /// </summary>
        /// <param name="onitem">目标物品格的物品状态</param>
        /// <param name="holdItem">鼠标上的物品</param>
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

        /// <summary>
        /// 实现拖拽功能，将 holdItem 拖拽到 onitem 上
        /// </summary>
        /// <param name="onitem">被拖拽的目标物品槽</param>
        /// <param name="holdItem">正在拖拽的物品</param>
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

        /// <summary>
        /// 对指定索引处的物品进行合并操作，将相同类型的物品堆叠到一起
        /// </summary>
        /// <param name="index">要进行合并操作的物品槽索引</param>
        /// <param name="holdItem">正在拖拽的物品</param>
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

        /// <summary>
        /// 进行快捷拿取
        /// </summary>
        /// <param name="inCoordIndex"></param>
        /// <param name="item"></param>
        private void GatheringItem2(int inCoordIndex, ref Item item) {
            if (item.type == ItemID.None && items[inCoordIndex].type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Item item1 = items[inCoordIndex].Clone();
                player.QuickSpawnItem(player.FromObjectGetParent(), item1, item1.stack);
                items[inCoordIndex] = new Item();
            }
        }

        /// <summary>
        /// 在目标物品格上放置物品
        /// </summary>
        /// <param name="onitem">目标物品格的物品状态</param>
        /// <param name="holdItem">鼠标上的物品</param>
        private void PlaceItemOnGrid(ref Item onitem, ref Item holdItem) {
            Item inToItem = holdItem.Clone();
            inToItem.stack = 1;
            onitem = inToItem;
            holdItem.stack -= 1;

            // 如果鼠标上的物品数量为0，则清空鼠标上的物品
            if (holdItem.stack == 0) {
                holdItem = new Item();
            }

            FinalizeCraftingResult();
        }

        /// <summary>
        /// 用于绘制格子中的目标物品
        /// </summary>
        /// <param name="spriteBatch">批处理对象</param>
        /// <param name="item">物品</param>
        /// <param name="drawpos">绘制位置</param>
        public static void DrawItemIcons(SpriteBatch spriteBatch, Item item, Vector2 drawpos, Vector2 offset = default, Color drawColor = default, float alp = 1, float overSlp = 1) {
            if (item != null && item.type != ItemID.None) {
                Rectangle rectangle = Main.itemAnimations[item.type] != null ? Main.itemAnimations[item.type].GetFrame(TextureAssets.Item[item.type].Value) : TextureAssets.Item[item.type].Value.Frame(1, 1, 0, 0);
                Vector2 vector = rectangle.Size();
                Vector2 size = TextureAssets.Item[item.type].Value.Size();
                if (offset == default) {
                    offset = new Vector2(cellWid, cellHig) / 2;
                }
                float slp = item.GetDrawItemSize(36) * overSlp;
                if (item.type == CWRLoad.DarkMatterBall) {
                    DarkMatterBall.DrawItemIcon(spriteBatch, drawpos + offset, item.type, alp);
                }
                else {
                    Texture2D itemValue = TextureAssets.Item[item.type].Value;
                    Color doDrawColor = (drawColor == default ? Color.White : drawColor) * alp;
                    spriteBatch.Draw(itemValue, drawpos + offset, new Rectangle?(rectangle), doDrawColor, 0f, vector / 2, slp, 0, 0f);
                }

                if (item.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, item.stack.ToString(), drawpos.X, drawpos.Y + 25, Color.White, Color.Black, new Vector2(0.3f), 1f);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            RecipeSidebarListView.Draw(spriteBatch);

            spriteBatch.Draw(Texture, DrawPosition, null, Color.White * _sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);//绘制出UI主体
            spriteBatch.Draw(CWRUtils.GetT2DValue("CalamityMod/UI/DraedonSummoning/DecryptCancelIcon"), DrawPosition, null, Color.White * _sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);//绘制出关闭按键
            if (onCloseP && _sengs >= 1) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRLocText.GetTextValue("SupertableUI_Text1"), DrawPosition.X, DrawPosition.Y, Color.Gold, Color.Black, new Vector2(0.3f), 1.1f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.1f));
            }

            if (previewItems != null) {
                for (int i = 0; i < items.Length; i++) {//遍历绘制出UI格中的所有预览物品
                    if (previewItems[i] != null) {
                        Item item = previewItems[i];
                        if (item != null) {
                            DrawItemIcons(spriteBatch, item, ArcCellPos(i), alp: 0.25f * _sengs);
                            //Main.DrawItemIcon(spriteBatch, item, ArcCellPos(i), Color.White * 0.25f, 1);
                        }
                    }
                }
            }
            if (items != null) {
                for (int i = 0; i < items.Length; i++) {//遍历绘制出UI格中的所有物品
                    if (items[i] != null) {
                        Item item = items[i];
                        if (item != null) {
                            DrawItemIcons(spriteBatch, item, ArcCellPos(i), alp: _sengs);
                        }
                    }
                }
            }

            Texture2D arrow = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/InputArrow2");
            if (inputItem != null && inputItem?.type != 0) {//如果输出格有物品，那么将它画出来
                DrawItemIcons(spriteBatch, inputItem, DrawPosition + new Vector2(552, 215), alp: _sengs, overSlp: 1.5f * _sengs);
                arrow = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/InputArrow");
            }
            spriteBatch.Draw(arrow, DrawPosition + new Vector2(460, 225), null, Color.White * _sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);//绘制出输出箭头

            if (hoverInPutItemCellPage && inCoordIndex >= 0 && inCoordIndex <= 80) { //处理鼠标在UI格中查看物品的事情
                Item overItem = items[inCoordIndex];
                if (overItem == null)
                    overItem = new Item();
                Main.HoverItem = overItem.Clone();
                Main.hoverItemName = overItem.Name;
                if (Main.mouseItem.type == ItemID.None && items[inCoordIndex]?.type == ItemID.None && previewItems != null) {
                    Item previewItem = previewItems[inCoordIndex];
                    Main.HoverItem = previewItem.Clone();
                    Main.hoverItemName = previewItem.Name;
                }
            }
            if (onInputP && inputItem != null && inputItem?.type != 0) {//处理查看输出结果物品的事情
                Main.HoverItem = inputItem.Clone();
                Main.hoverItemName = inputItem.Name;
            }
        }
    }
}
