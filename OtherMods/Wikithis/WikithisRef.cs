using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Wikithis
{
    internal class WikithisRef : ICWRLoader
    {
        private const string WikiBaseUrl = "https://calamity-overhaul.cc";
        private const string WikithisModName = "Wikithis";
        //与 Wikithis.WikithisItem.TooltipName 保持一致
        private const string WikithisTooltipName = WikithisModName + ":Wiki";

        public static bool Has => ModLoader.HasMod(WikithisModName);

        private static GlobalItem _wikithisGlobalItem;
        private static bool _wikithisGlobalItemResolved;

        //按物品 ID 强制 CWR wiki 图标
        private static readonly Dictionary<int, Asset<Texture2D>> _iconOverrides = new();

        private delegate bool On_WikithisItem_PreDrawTooltipLine_Delegate(object self, Item item, DrawableTooltipLine line, ref int yOffset);

        void ICWRLoader.SetupData() {
            if (Main.dedServ || !Has) {
                return;
            }

            if (!ModLoader.TryGetMod(WikithisModName, out Mod wikithis)) {
                return;
            }

            //CWR 归属用小图标
            if (CWRAsset.icon_small != null) {
                wikithis.Call("AddWikiTexture", CWRMod.Instance, CWRAsset.icon_small);
            }

            //传奇武器按 ID 强制图标
            HookWikithisPreDrawTooltipLine(wikithis);

            var englishIds = new List<int>();
            var englishUrls = new List<string>();
            var chineseIds = new List<int>();
            var chineseUrls = new List<string>();

            foreach (ModItem modItem in ModContent.GetContent<ModItem>()) {
                if (modItem is null || modItem.Mod is null) {
                    continue;
                }

                if (modItem.Mod != CWRMod.Instance || modItem.GetType().IsAbstract) {
                    continue;
                }

                string pathSegment = modItem switch {
                    SHPCModuleItem => $"legend/shpc/modules/{ToSHPCModuleSlug(modItem.GetType().Name)}",
                    _ => $"items/{modItem.Name.ToLowerInvariant()}",
                };

                englishIds.Add(modItem.Type);
                englishUrls.Add($"{WikiBaseUrl}/en/{pathSegment}/");
                chineseIds.Add(modItem.Type);
                chineseUrls.Add($"{WikiBaseUrl}/cn/{pathSegment}/");
            }

            AddLegendWeaponUrls(englishIds, englishUrls, chineseIds, chineseUrls, SHPCOverride.ID, "legend/shpc");
            AddLegendWeaponUrls(englishIds, englishUrls, chineseIds, chineseUrls, HalibutOverride.ID, "legend/halibut");

            if (englishIds.Count == 0) {
                return;
            }

            //ReplaceItem 用 TryAdd，外部已注册的 ID 顶不掉
            //反射写内部字典强制覆盖，失败再退回 Call
            int overwritten = ForceOverwriteUrls(wikithis, englishIds, englishUrls, GameCulture.CultureName.English)
                + ForceOverwriteUrls(wikithis, chineseIds, chineseUrls, GameCulture.CultureName.Chinese);

            if (overwritten <= 0) {
                wikithis.Call("ReplaceItem", englishIds, englishUrls);
                wikithis.Call("ReplaceItem", chineseIds, chineseUrls, GameCulture.CultureName.Chinese);
            }

            CWRMod.Instance.Logger.Info($"WikithisRef registered {englishIds.Count} Calamity Overhaul item wiki links.");

            //灾厄归属传奇武器改显示 CWR 图标
            if (CWRAsset.icon_small != null) {
                if (SHPCOverride.ID > ItemID.None) {
                    _iconOverrides[SHPCOverride.ID] = CWRAsset.icon_small;
                }
            }
        }

        private static bool _preDrawTooltipLineHooked;

        private static void HookWikithisPreDrawTooltipLine(Mod wikithis) {
            if (_preDrawTooltipLineHooked) {
                return;
            }
            try {
                Type wikithisItemType = wikithis.Code?.GetType("Wikithis.WikithisItem");
                MethodInfo preDrawMethod = wikithisItemType?.GetMethod("PreDrawTooltipLine",
                    BindingFlags.Public | BindingFlags.Instance);
                if (preDrawMethod != null) {
                    VaultHook.Add(preDrawMethod, OnWikithisItem_PreDrawTooltipLine);
                    _preDrawTooltipLineHooked = true;
                }
            } catch (Exception e) {
                CWRMod.Instance.Logger.Warn($"WikithisRef hook PreDrawTooltipLine failed: {e.Message}");
            }
        }

        /// <summary>拦截 PreDrawTooltipLine，画覆盖图标</summary>
        private static bool OnWikithisItem_PreDrawTooltipLine(
            On_WikithisItem_PreDrawTooltipLine_Delegate orig,
            object self, Item item, DrawableTooltipLine line, ref int yOffset) {
            if (item != null && line != null
                && line.Mod == WikithisModName && line.Name == WikithisTooltipName
                && _iconOverrides.TryGetValue(item.type, out Asset<Texture2D> asset)
                && asset?.Value != null) {
                DrawCustomWikiIcon(line, asset);
                return false;
            }

            return orig(self, item, line, ref yOffset);
        }

        //复刻 Wikithis DrawIcon 布局，换图源
        private static void DrawCustomWikiIcon(DrawableTooltipLine line, Asset<Texture2D> asset) {
            Texture2D texture = asset.Value;
            Vector2 position = new Vector2(line.X, line.Y);
            Rectangle sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 origin = Vector2.Zero;
            Vector2 scale = new Vector2(2f / 3f);

            int baseHeight = TextureAssets.BestiaryMenuButton?.Value?.Height ?? 30;
            origin.X = -((30f - texture.Width) / 2f);
            origin.Y = -((baseHeight - texture.Height) / 2f);

            Main.spriteBatch.Draw(texture, position, sourceRect, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            Utils.DrawBorderStringFourWay(Main.spriteBatch, line.Font, line.Text,
                position.X, position.Y, line.OverrideColor ?? line.Color, Color.Black, line.Origin);
        }

        /// <summary>
        /// 手动补 Wikithis ModifyTooltips（钩子被屏蔽时，如 SHPC）
        /// 已有 Wiki 行则跳过
        /// </summary>
        public static void TryAppendWikiTooltip(Item item, List<TooltipLine> tooltips) {
            if (Main.dedServ || item is null || tooltips is null || !Has) {
                return;
            }

            foreach (TooltipLine line in tooltips) {
                if (line is not null && line.Mod == WikithisModName && line.Name == WikithisTooltipName) {
                    return;
                }
            }

            GlobalItem wikithisItem = GetWikithisGlobalItem();
            if (wikithisItem is null) {
                return;
            }

            wikithisItem.ModifyTooltips(item, tooltips);
        }

        private static GlobalItem GetWikithisGlobalItem() {
            if (_wikithisGlobalItemResolved) {
                return _wikithisGlobalItem;
            }
            _wikithisGlobalItemResolved = true;

            if (ModLoader.TryGetMod(WikithisModName, out Mod wikithis)
                && wikithis.TryFind("WikithisItem", out GlobalItem found)) {
                _wikithisGlobalItem = found;
            }
            return _wikithisGlobalItem;
        }

        private static IDictionary _itemReplacementsDict;
        private static bool _itemReplacementsResolved;
        private static Type _keyTupleType;

        /// <summary>反射写 itemReplacements，返回覆盖数</summary>
        private static int ForceOverwriteUrls(Mod wikithis, List<int> ids, List<string> urls, GameCulture.CultureName language) {
            IDictionary dict = GetItemReplacementsDict(wikithis);
            if (dict is null || _keyTupleType is null) {
                return 0;
            }

            int count = 0;
            try {
                for (int i = 0; i < ids.Count; i++) {
                    object key = Activator.CreateInstance(_keyTupleType, (short)ids[i], language);
                    dict[key] = urls[i];
                    count++;
                }
            } catch (Exception e) {
                CWRMod.Instance.Logger.Warn($"WikithisRef ForceOverwriteUrls failed: {e.Message}");
                return 0;
            }
            return count;
        }

        private static IDictionary GetItemReplacementsDict(Mod wikithis) {
            if (_itemReplacementsResolved) {
                return _itemReplacementsDict;
            }
            _itemReplacementsResolved = true;

            try {
                Type wikithisType = wikithis.Code?.GetType("Wikithis.Wikithis");
                FieldInfo field = wikithisType?.GetField("itemReplacements", BindingFlags.NonPublic | BindingFlags.Static);
                if (field?.GetValue(null) is IDictionary dict) {
                    _itemReplacementsDict = dict;
                    Type[] gen = field.FieldType.GetGenericArguments();
                    if (gen.Length >= 1) {
                        _keyTupleType = gen[0];
                    }
                }
            } catch (Exception e) {
                CWRMod.Instance.Logger.Warn($"WikithisRef GetItemReplacementsDict failed: {e.Message}");
            }

            return _itemReplacementsDict;
        }

        private static void AddLegendWeaponUrls(
            List<int> englishIds, List<string> englishUrls,
            List<int> chineseIds, List<string> chineseUrls,
            int itemId, string pathSegment) {
            if (itemId <= ItemID.None) {
                return;
            }

            englishIds.Add(itemId);
            englishUrls.Add($"{WikiBaseUrl}/en/{pathSegment}/");
            chineseIds.Add(itemId);
            chineseUrls.Add($"{WikiBaseUrl}/cn/{pathSegment}/");
        }

        private static string ToSHPCModuleSlug(string className) {
            if (className.EndsWith("Module")) {
                className = className[..^"Module".Length];
            }

            var slug = new StringBuilder(className.Length + 8);
            for (int i = 0; i < className.Length; i++) {
                char c = className[i];
                if (char.IsUpper(c)) {
                    if (i > 0) {
                        slug.Append('-');
                    }
                    slug.Append(char.ToLowerInvariant(c));
                }
                else {
                    slug.Append(c);
                }
            }
            return slug.ToString();
        }
    }
}
