using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace CalamityOverhaul.Content.UIs
{
    internal class UISystem : ModSystem
    {
        public override void PostSetupContent() {
            //将自定义的UI放到最后加载，在这之前是确保物品、ID、生物等其他内容都加载完成后
            if (CWRServerConfig.Instance.AddExtrasContent) {
                new SupertableUI().Load();
                new RecipeUI().Load();
                new DragButton().Load();
                new RecipeErrorFullUI().Load();
                new InItemDrawRecipe().Load();
                new MouseTextContactPanel().Load();
                new OneClickUI().Load();
            }
            new ResetItemReminderUI().Load();
            new OverhaulTheBibleUI().Load();
            new CartridgeHolderUI().Load();
            new TungstenRiotUI().Load();

            OverhaulTheBibleUI.Instance.ecTypeItemList = new List<Item>();
            foreach (BaseRItem baseRItem in CWRMod.RItemInstances) {
                Item item = new Item(baseRItem.TargetID);
                if (item != null) {
                    if (item.type != ItemID.None) {
                        OverhaulTheBibleUI.Instance.ecTypeItemList.Add(item);
                    }
                }
            }
        }

        public override void SaveWorldData(TagCompound tag) {
            if (CWRServerConfig.Instance.AddExtrasContent) {
                if (SupertableUI.Instance != null && SupertableUI.Instance?.items != null) {
                    for (int i = 0; i < SupertableUI.Instance.items.Length; i++) {
                        if (SupertableUI.Instance.items[i] == null) {
                            SupertableUI.Instance.items[i] = new Item(0);
                        }
                    }
                    tag.Add("SupertableUI_ItemDate", SupertableUI.Instance.items);
                }
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            if (CWRServerConfig.Instance.AddExtrasContent) {
                if (SupertableUI.Instance != null && tag.ContainsKey("SupertableUI_ItemDate")) {
                    Item[] loadSupUIItems = tag.Get<Item[]>("SupertableUI_ItemDate");
                    for (int i = 0; i < loadSupUIItems.Length; i++) {
                        if (loadSupUIItems[i] == null) {
                            loadSupUIItems[i] = new Item(0);
                        }
                    }
                    SupertableUI.Instance.items = tag.Get<Item[]>("SupertableUI_ItemDate");
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int mouseIndex = layers.FindIndex((GameInterfaceLayer layer) => layer.Name == "Vanilla: Mouse Text");
            if (mouseIndex != -1) {
                layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Sp UI", delegate {
                    if (CWRServerConfig.Instance.AddExtrasContent && SupertableUI.Instance.Active) {
                        //上帝，必须得这么做？将逻辑更新和绘制更新放到同一个线程里面！这他妈的的逻辑更新将在绘制线程里面运行！这他妈的简直会是一场亵渎
                        SupertableUI.Instance.Update(Main.gameTimeCache);
                        RecipeUI.Instance.Update(Main.gameTimeCache);
                        DragButton.Instance.Update(Main.gameTimeCache);
                        RecipeErrorFullUI.Instance.Update(Main.gameTimeCache);
                        OneClickUI.Instance.Update(Main.gameTimeCache);
                        //绘制更新将放置在逻辑更新之后
                        SupertableUI.Instance.Draw(Main.spriteBatch);
                        RecipeUI.Instance.Draw(Main.spriteBatch);
                        DragButton.Instance.Draw(Main.spriteBatch);
                        RecipeErrorFullUI.Instance.Draw(Main.spriteBatch);
                        OneClickUI.Instance.Draw(Main.spriteBatch);
                    }
                    return true;
                }, InterfaceScaleType.UI));
                layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Ov UI", delegate {
                    if (OverhaulTheBibleUI.Instance.Active) {
                        OverhaulTheBibleUI.Instance.Update(Main.gameTimeCache);
                        OverhaulTheBibleUI.Instance.Draw(Main.spriteBatch);
                    }
                    return true;
                }, InterfaceScaleType.UI));
                layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("CH UI", delegate {
                    if (CartridgeHolderUI.Instance.Active && CWRServerConfig.Instance.MagazineSystem) {
                        CartridgeHolderUI.Instance.Update(Main.gameTimeCache);
                        CartridgeHolderUI.Instance.Draw(Main.spriteBatch);
                    }
                    return true;
                }, InterfaceScaleType.UI));
            }

            int invasionIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Diagnose Net");
            if (invasionIndex != -1) {
                layers.Insert(invasionIndex, new LegacyGameInterfaceLayer("TG UI", delegate {
                    if (TungstenRiotUI.Instance.Active) {
                        TungstenRiotUI.Instance.Update(Main.gameTimeCache);
                        TungstenRiotUI.Instance.Draw(Main.spriteBatch);
                    }
                    return true;
                }, InterfaceScaleType.UI));
            }
        }
    }
}
