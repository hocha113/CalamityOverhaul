using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Rarities;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon
{
    /// <summary>传奇自绘 tooltip 的行分区结果</summary>
    internal sealed class LegendTooltipSections
    {
        /// <summary>题行文本(物品名,含前缀名)</summary>
        public string ItemName;
        /// <summary>题行颜色(行覆盖色优先,否则稀有度色)</summary>
        public Color NameColor;
        /// <summary>原版数据行(伤害/暴击/速度/前缀/价格等)</summary>
        public List<TooltipLine> Stats = [];
        /// <summary>正文行(CWRText 与本模组动态行)</summary>
        public List<TooltipLine> Body = [];
        /// <summary>未识别行(他模注入,照画保兼容)</summary>
        public List<TooltipLine> Extra = [];
    }

    /// <summary>键位区一行;有 fallback 键(右键/中键)的功能视作可用</summary>
    internal readonly struct LegendKeybindRow(string label, string keyText, bool bound)
    {
        public readonly string Label = label;
        public readonly string KeyText = keyText;
        public readonly bool Bound = bound;
    }

    /// <summary>试炼进度快照,面板试炼区数据源</summary>
    internal readonly struct LegendTrialInfo(bool valid, int done, int total, string nextNames, string worldName, int recordLevel)
    {
        public readonly bool Valid = valid;
        /// <summary>已过关数(前缀制)</summary>
        public readonly int Done = done;
        /// <summary>当前可用路线总关数(外部内容缺席时可短于满级)</summary>
        public readonly int Total = total;
        /// <summary>下一关目标显示名,通关后为 null</summary>
        public readonly string NextNames = nextNames;
        /// <summary>跨世界提示:上次升级世界名,无需提示时 null</summary>
        public readonly string WorldName = worldName;
        /// <summary>已确认的记录等级(跨世界提示用)</summary>
        public readonly int RecordLevel = recordLevel;
        public bool Passed => Valid && Done >= Total;
    }

    /// <summary>面板自定义区:皮肤方自绘的一段(SHPC 改件网格等),插在正文区与键位区之间</summary>
    internal abstract class LegendTooltipCustomSection
    {
        /// <summary>测量高度;返回 0 = 本帧无内容,整区连分隔线一起省略</summary>
        public abstract float Measure(float contentWidth);
        /// <summary>绘制,origin=区左上(UI 空间)</summary>
        public abstract void Draw(SpriteBatch sb, Vector2 origin, float contentWidth, float time);
    }

    /// <summary>一次自绘请求:皮肤+键位+试炼区文本,行数据引擎自行分区</summary>
    internal sealed class LegendTooltipRequest
    {
        public LegendTooltipSkin Skin;
        public List<LegendKeybindRow> KeyRows;
        public LegendTrialInfo Trial;
        /// <summary>试炼进度行("试炼: 3 / 24" 或通关行)</summary>
        public string TrialLine;
        /// <summary>下一目标行("下一席:XXX"),可空</summary>
        public string NextLine;
        /// <summary>跨世界升级提示行组,可空</summary>
        public string[] WorldLines;
        /// <summary>任务书入口提示({KEY} 已替换),可空</summary>
        public string QuestHint;
        /// <summary>自定义区(正文与键位之间),可空</summary>
        public LegendTooltipCustomSection Custom;
    }

    /// <summary>传奇 tooltip 皮肤:色板与背景/分隔/进度条画法,布局归 <see cref="LegendTooltipPanel"/></summary>
    internal abstract class LegendTooltipSkin
    {
        public abstract Color TextMain { get; }
        public abstract Color TextDim { get; }
        /// <summary>已绑定键位的键名色</summary>
        public abstract Color KeyLit { get; }
        /// <summary>未绑定警示色</summary>
        public abstract Color KeyWarn { get; }
        /// <summary>跨世界升级提示色</summary>
        public abstract Color WorldAccent { get; }
        public abstract void DrawPanel(SpriteBatch sb, Rectangle panel, float time);
        public abstract void DrawDivider(SpriteBatch sb, Vector2 left, Vector2 right, float time);
        public abstract void DrawProgressBar(SpriteBatch sb, Rectangle bar, float fill, bool passed, float time);
        /// <summary>面板级装饰(角章/垂珠),画在文字之后</summary>
        public virtual void DecoratePanel(SpriteBatch sb, Rectangle panel, float time) { }
    }

    /// <summary>
    /// 鬼伞/鬼切自绘物品面板的共享布局引擎。挂接点是 ModItem.PreDrawTooltip 返回 false
    /// (阻断原生行绘制,lines 仍是数据源)。「提示框背景不透明」的原生蓝框画在钩子之前拦不掉,
    /// 开启该设置时把面板并到盖得住它的矩形
    /// </summary>
    internal static class LegendTooltipPanel
    {
        private const int ScreenPad = 4;
        private const float PadX = 14f;
        private const float PadTop = 10f;
        private const float PadBottom = 12f;
        private const float MaxContentW = 430f;
        private const float MinContentW = 360f;
        private const float TitleScale = 1.0f;
        private const float StatScale = 0.85f;
        private const float BodyScale = 0.9f;
        private const float SubScale = 0.85f;
        private const float RowGap = 2f;
        private const float SectionGap = 6f;
        private const float ColGap = 12f;
        private const int BarH = 8;

        //前缀加成行沿用原版绿/红语义色,不随皮肤
        private static readonly Color PrefixGood = new(120, 190, 120);
        private static readonly Color PrefixBad = new(190, 120, 120);

        //原版数据行名单;未列出的原版行与他模行进 Extra 区照画
        private static readonly HashSet<string> StatNames = new(StringComparer.Ordinal) {
            "Damage", "CritChance", "Speed", "Knockback", "Defense", "Equipable",
            "Consumable", "Material", "Ammo", "UseMana", "HealLife", "HealMana",
            "TileBoost", "BuffTime", "WellFedExpert", "Price", "SpecialPrice",
            "Quest", "Expert", "Master", "JourneyResearch", "FishingPower",
            "NeedsBait", "BaitPower", "Placeable", "PickPower", "AxePower",
            "HammerPower", "SetBonus", "Vanity", "VanityLegal", "WandConsumes",
            "EtherianManaWarning", "Favorite", "FavoriteDesc", "NoTransfer",
        };

        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        //经本面板接管过绘制的物品类型；稀有度名称行渲染器据此跳过，避免在原生坐标重画名字
        private static readonly HashSet<int> panelItemTypes = [];

        /// <summary>该物品的 tooltip 是否由传奇面板整块自绘</summary>
        public static bool IsPanelItem(int itemType) => panelItemTypes.Contains(itemType);

        private enum OpKind { Text, Title, Divider, Bar, Custom }

        private struct DrawOp
        {
            public OpKind Kind;
            public string Text;
            public Color Color;
            public float Scale;
            public Vector2 Offset;
            public float Width;
            public float Fill;
            public bool Passed;
        }

        /// <summary>
        /// 把本模组正文行按面板宽预折行(1.0 测量口径)。原生蓝框按未折行原文测宽,
        /// 长句会把覆盖 Union 撑成巨宽面板;预折后蓝框与面板同宽。SetTooltip 阶段调用
        /// </summary>
        public static void WrapBodyText(List<TooltipLine> tooltips) {
            if (Main.dedServ || FontAssets.MouseText?.Value == null) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string cwrName = CWRMod.Instance.Name;
            foreach (TooltipLine line in tooltips) {
                if (line.Mod != cwrName || string.IsNullOrEmpty(line.Text)) {
                    continue;
                }
                if (line.Name != "CWRText" && !line.Name.StartsWith("CWR_OniMei", StringComparison.Ordinal)) {
                    continue;
                }
                List<string> wrapped = [];
                foreach (string seg in line.Text.Split('\n')) {
                    if (seg.Length == 0) {
                        continue;
                    }
                    if (font.MeasureString(seg).X <= MaxContentW) {
                        wrapped.Add(seg);
                        continue;
                    }
                    foreach (string piece in VaultUtils.WrapText(seg, font, MaxContentW, 1f)) {
                        string trimmed = piece.TrimEnd();
                        if (trimmed.Length > 0) {
                            wrapped.Add(trimmed);
                        }
                    }
                }
                line.Text = string.Join("\n", wrapped);
            }
        }

        /// <summary>按行名分区;隐藏行在 ModifyTooltips 后已被 tML 过滤,这里收到的全是可见行</summary>
        public static LegendTooltipSections Classify(Item item, ReadOnlyCollection<TooltipLine> lines) {
            LegendTooltipSections sections = new() {
                ItemName = item.HoverName,
                NameColor = GetRarityColor(item),
            };
            string cwrName = CWRMod.Instance.Name;
            foreach (TooltipLine line in lines) {
                if (line.Mod == "Terraria" && line.Name == "ItemName") {
                    sections.ItemName = line.Text;
                    if (line.OverrideColor.HasValue) {
                        sections.NameColor = line.OverrideColor.Value;
                    }
                    continue;
                }
                if (line.Mod == "Terraria" && (StatNames.Contains(line.Name)
                    || line.Name.StartsWith("Prefix", StringComparison.Ordinal))) {
                    sections.Stats.Add(line);
                    continue;
                }
                if (line.Mod == cwrName && (line.Name == "CWRText"
                    || line.Name == "CWR_KikasaGhosts"
                    || line.Name.StartsWith("CWR_OniMei", StringComparison.Ordinal))) {
                    sections.Body.Add(line);
                    continue;
                }
                sections.Extra.Add(line);
            }
            return sections;
        }

        /// <summary>物品名稀有度色,mod 稀有度走 RarityLoader</summary>
        public static Color GetRarityColor(Item item) {
            if (item.expert || item.rare == ItemRarityID.Expert) {
                return new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            }
            if (item.master || item.rare == ItemRarityID.Master) {
                return new Color(255, (int)(Main.masterColor * 200f), 0);
            }
            if (item.rare >= ItemRarityID.Count && RarityLoader.GetRarity(item.rare) is ModRarity modRarity) {
                return modRarity.RarityColor;
            }
            return ItemRarity.GetColor(item.rare);
        }

        /// <summary>取当前输入模式下的绑定;解绑且无 fallback 才算未绑定</summary>
        public static LegendKeybindRow BuildKeyRow(LocalizedText label, ModKeybind keybind, string fallback = null) {
            InputMode mode = PlayerInput.UsingGamepad ? InputMode.XBoxGamepad : InputMode.Keyboard;
            bool bound = !CWRKeySystem.IsKeybindUnbound(keybind, mode);
            string keyText = bound
                ? CWRKeySystem.GetKeybindText(keybind, CWRKeySystem.Notbound.Value, mode)
                : fallback ?? CWRKeySystem.Notbound.Value;
            return new LegendKeybindRow(label.Value, keyText, bound || fallback != null);
        }

        /// <summary>试炼进度行:「试炼: N / M」,通过=「试炼: 已通过」;零新键,复用 7 语言既有翻译</summary>
        public static string BuildTrialLine(LegendTrialInfo trial) {
            if (!trial.Valid) {
                return null;
            }
            string label = LegendUpgradeManagerSystem.Text_Lang_0.Value;
            return trial.Passed
                ? label + " " + LegendUpgradeManagerSystem.TrialPassed.Value
                : label + " " + trial.Done + " / " + trial.Total;
        }

        /// <summary>读传奇试炼进度;口径与旧 tooltip 一致(TargetLevel=前缀制已过数)</summary>
        public static LegendTrialInfo ReadTrial(Item item) {
            LegendData data = item?.CWR()?.LegendData;
            if (data == null) {
                return default;
            }
            IReadOnlyList<LegendTrialDefinition> route = LegendTrialRouteResolver.GetAvailableTrials(data.TrialDefinitions);
            if (route.Count == 0) {
                return default;
            }
            int done = Math.Clamp(data.TargetLevel, 0, route.Count);
            string next = null;
            if (done < route.Count) {
                next = string.Join(" / ", route[done].Target?.GetDisplayNames() ?? []);
            }
            string worldName = null;
            if (!data.UpgradeTagNameIsEmpty && !data.IsUpgradeWorld) {
                worldName = data.UpgradeWorldName;
            }
            return new LegendTrialInfo(true, done, route.Count, next, worldName, data.Level);
        }

        /// <summary>完整自绘:分区排版(数据/键位双列)+面板放置+皮肤绘制</summary>
        public static void Draw(SpriteBatch sb, Item item, ReadOnlyCollection<TooltipLine> lines,
            int x, int y, LegendTooltipRequest req) {
            LegendTooltipSkin skin = req.Skin;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            LegendTooltipSections sections = Classify(item, lines);
            float glyphH = font.MeasureString("A").Y;
            panelItemTypes.Add(item.type);

            //---- 内容宽:自然需求夹在 [Min, 屏宽余量] ----
            float limit = Math.Max(80f, Math.Min(MaxContentW, UIScreenW - ScreenPad * 2 - PadX * 2));
            float natural = MeasureWidth(font, sections.ItemName, TitleScale);
            foreach (TooltipLine line in sections.Stats) {
                natural = Math.Max(natural, MeasureWidth(font, line.Text, StatScale));
            }
            foreach (TooltipLine line in sections.Body) {
                foreach (string seg in (line.Text ?? "").Split('\n')) {
                    natural = Math.Max(natural, MeasureWidth(font, seg, BodyScale));
                }
            }
            if (req.KeyRows is { Count: > 0 }) {
                float cellNeed = 0f;
                foreach (LegendKeybindRow row in req.KeyRows) {
                    cellNeed = Math.Max(cellNeed, MeasureWidth(font, row.Label + "  " + row.KeyText, SubScale));
                }
                natural = Math.Max(natural, cellNeed * 2f + ColGap);
            }
            natural = Math.Max(natural, MeasureWidth(font, req.TrialLine, BodyScale));
            natural = Math.Max(natural, MeasureWidth(font, req.QuestHint, SubScale));
            float contentW = MathHelper.Clamp(natural, Math.Min(MinContentW, limit), limit);
            float colW = (contentW - ColGap) * 0.5f;

            //---- 排版:先生成绘制指令与总高,再统一落位 ----
            List<DrawOp> ops = [];
            float curY = 0f;

            void AddText(string text, Color color, float scale, float xOff, float maxW) {
                if (string.IsNullOrEmpty(text)) {
                    return;
                }
                float w = font.MeasureString(text).X;
                float drawScale = w > 0f && w * scale > maxW ? maxW / w : scale;
                ops.Add(new DrawOp {
                    Kind = OpKind.Text, Text = text, Color = color,
                    Scale = drawScale, Offset = new Vector2(xOff, curY),
                });
            }

            void AddDivider() {
                curY += SectionGap * 0.5f;
                ops.Add(new DrawOp { Kind = OpKind.Divider, Offset = new Vector2(0f, curY), Width = contentW });
                curY += SectionGap;
            }

            List<string> WrapAll(string text, float scale) {
                List<string> wrapped = [];
                foreach (string seg in (text ?? "").Split('\n')) {
                    if (seg.Length == 0) {
                        continue;
                    }
                    //实测已适宽的整段不进折行器：CJK 逐字累计宽略胖于整串测量，
                    //恰好定下面板宽的最长句会被折行器把末字挤成孤行（与 WrapBodyText 同判式，反馈 #8）
                    if (font.MeasureString(seg).X * scale <= contentW) {
                        wrapped.Add(seg);
                        continue;
                    }
                    foreach (string piece in VaultUtils.WrapText(seg, font, contentW, scale)) {
                        string trimmed = piece.TrimEnd();
                        if (trimmed.Length > 0) {
                            wrapped.Add(trimmed);
                        }
                    }
                }
                return wrapped;
            }

            //题行：走稀有度名称特效入口，与原生提示框同款
            if (!string.IsNullOrEmpty(sections.ItemName)) {
                float titleW = font.MeasureString(sections.ItemName).X;
                float titleScale = titleW > 0f && titleW * TitleScale > contentW ? contentW / titleW : TitleScale;
                ops.Add(new DrawOp {
                    Kind = OpKind.Title, Text = sections.ItemName, Color = sections.NameColor,
                    Scale = titleScale, Offset = new Vector2(0f, curY),
                });
            }
            curY += glyphH * TitleScale + 4f;

            //数据区:相邻短行并作双列
            for (int i = 0; i < sections.Stats.Count; i++) {
                TooltipLine a = sections.Stats[i];
                TooltipLine b = i + 1 < sections.Stats.Count ? sections.Stats[i + 1] : null;
                float wA = MeasureWidth(font, a.Text, StatScale);
                float wB = b != null ? MeasureWidth(font, b.Text, StatScale) : float.MaxValue;
                if (wA <= colW && wB <= colW) {
                    AddText(a.Text, StatColor(a, skin), StatScale, 0f, colW);
                    AddText(b.Text, StatColor(b, skin), StatScale, colW + ColGap, colW);
                    i++;
                }
                else {
                    AddText(a.Text, StatColor(a, skin), StatScale, 0f, contentW);
                }
                curY += glyphH * StatScale + RowGap;
            }

            //正文区
            if (sections.Body.Count > 0) {
                AddDivider();
                foreach (TooltipLine line in sections.Body) {
                    Color color = line.OverrideColor ?? skin.TextMain;
                    foreach (string seg in WrapAll(line.Text, BodyScale)) {
                        AddText(seg, color, BodyScale, 0f, contentW);
                        curY += glyphH * BodyScale + RowGap;
                    }
                }
            }

            //自定义区(SHPC 改件等):测得 0 高整区省略
            float customH = req.Custom?.Measure(contentW) ?? 0f;
            if (customH > 0f) {
                AddDivider();
                ops.Add(new DrawOp { Kind = OpKind.Custom, Offset = new Vector2(0f, curY), Width = contentW });
                curY += customH + RowGap;
            }

            //键位区:双列,功能名与键名分色
            if (req.KeyRows is { Count: > 0 }) {
                AddDivider();
                for (int i = 0; i < req.KeyRows.Count; i += 2) {
                    AddKeyCell(req.KeyRows[i], 0f);
                    if (i + 1 < req.KeyRows.Count) {
                        AddKeyCell(req.KeyRows[i + 1], colW + ColGap);
                    }
                    curY += glyphH * SubScale + RowGap + 1f;
                }
            }

            void AddKeyCell(LegendKeybindRow row, float xOff) {
                string label = row.Label + "  ";
                float labelW = font.MeasureString(label).X;
                float keyW = font.MeasureString(row.KeyText).X;
                float scale = SubScale;
                float total = (labelW + keyW) * scale;
                if (total > colW && total > 0f) {
                    scale *= colW / total;
                }
                ops.Add(new DrawOp {
                    Kind = OpKind.Text, Text = label, Color = skin.TextDim,
                    Scale = scale, Offset = new Vector2(xOff, curY),
                });
                ops.Add(new DrawOp {
                    Kind = OpKind.Text, Text = row.KeyText,
                    Color = row.Bound ? skin.KeyLit : skin.KeyWarn,
                    Scale = scale, Offset = new Vector2(xOff + labelW * scale, curY),
                });
            }

            //试炼区:进度行+进度条+下一目标+跨世界提示
            if (req.Trial.Valid) {
                AddDivider();
                AddText(req.TrialLine, skin.TextMain, BodyScale, 0f, contentW);
                curY += glyphH * BodyScale + 3f;
                ops.Add(new DrawOp {
                    Kind = OpKind.Bar, Offset = new Vector2(0f, curY), Width = contentW,
                    Fill = req.Trial.Total > 0 ? req.Trial.Done / (float)req.Trial.Total : 0f,
                    Passed = req.Trial.Passed,
                });
                curY += BarH + 5f;
                if (!string.IsNullOrEmpty(req.NextLine)) {
                    foreach (string seg in WrapAll(req.NextLine, SubScale)) {
                        AddText(seg, skin.TextDim, SubScale, 0f, contentW);
                        curY += glyphH * SubScale + RowGap;
                    }
                }
                if (req.WorldLines != null) {
                    foreach (string worldLine in req.WorldLines) {
                        foreach (string seg in WrapAll(worldLine, SubScale)) {
                            AddText(seg, skin.WorldAccent, SubScale, 0f, contentW);
                            curY += glyphH * SubScale + RowGap;
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(req.QuestHint)) {
                AddText(req.QuestHint, skin.TextDim, SubScale, 0f, contentW);
                curY += glyphH * SubScale + RowGap;
            }

            //其他区:他模注入行
            if (sections.Extra.Count > 0) {
                AddDivider();
                foreach (TooltipLine line in sections.Extra) {
                    Color color = line.OverrideColor ?? skin.TextDim;
                    foreach (string seg in WrapAll(line.Text, SubScale)) {
                        AddText(seg, color, SubScale, 0f, contentW);
                        curY += glyphH * SubScale + RowGap;
                    }
                }
            }

            //---- 面板放置:按面板全尺寸四边钳制(原生只按行区域钳过,不够) ----
            float panelW = contentW + PadX * 2f;
            float panelH = curY + PadTop + PadBottom;
            int screenW = Math.Max(1, (int)MathF.Floor(UIScreenW));
            int screenH = Math.Max(1, (int)MathF.Floor(UIScreenH));
            int panelX = x - (int)PadX;
            int panelY = y - (int)PadTop;
            if (panelX + panelW > screenW - ScreenPad) {
                panelX = screenW - ScreenPad - (int)MathF.Ceiling(panelW);
            }
            if (panelY + panelH > screenH - ScreenPad) {
                panelY = screenH - ScreenPad - (int)MathF.Ceiling(panelH);
            }
            panelX = Math.Max(panelX, ScreenPad);
            panelY = Math.Max(panelY, ScreenPad);
            Rectangle panel = new(panelX, panelY, (int)MathF.Ceiling(panelW), (int)MathF.Ceiling(panelH));

            if (Main.SettingsEnabled_OpaqueBoxBehindTooltips) {
                //原生蓝框以原 x/y 为锚,面板并上去盖住(Union 只向右下扩,不动内容原点);
                //高度余量给旅程研究行等图标行的原生 +24;
                //四周再放 4px:shader 蚀边会把面板边缘侵蚀成渐隐,蓝框边要留在蚀边之内
                Vector2 native = MeasureNativeLines(font, lines);
                Rectangle cover = new(x - 14, y - 9, (int)native.X + 28, (int)native.Y + 38);
                cover.Inflate(4, 4);
                panel = Rectangle.Union(panel, cover);
            }

            float time = Main.GlobalTimeWrappedHourly;
            skin.DrawPanel(sb, panel, time);
            Vector2 origin = new(panelX + PadX, panelY + PadTop);
            foreach (DrawOp op in ops) {
                switch (op.Kind) {
                    case OpKind.Text:
                        Utils.DrawBorderString(sb, op.Text, origin + op.Offset, op.Color, op.Scale);
                        break;
                    case OpKind.Title:
                        RarityNameEffects.DrawItemName(sb, item, op.Text, origin + op.Offset, op.Color, op.Scale);
                        break;
                    case OpKind.Divider:
                        skin.DrawDivider(sb, origin + op.Offset, origin + op.Offset + new Vector2(op.Width, 0f), time);
                        break;
                    case OpKind.Bar:
                        Vector2 barPos = origin + op.Offset;
                        skin.DrawProgressBar(sb, new Rectangle((int)barPos.X, (int)barPos.Y, (int)op.Width, BarH),
                            op.Fill, op.Passed, time);
                        break;
                    case OpKind.Custom:
                        req.Custom?.Draw(sb, origin + op.Offset, op.Width, time);
                        break;
                }
            }
            skin.DecoratePanel(sb, panel, time);
        }

        private static Color StatColor(TooltipLine line, LegendTooltipSkin skin) {
            if (line.IsModifier) {
                return line.IsModifierBad ? PrefixBad : PrefixGood;
            }
            return line.OverrideColor ?? skin.TextDim;
        }

        private static float MeasureWidth(DynamicSpriteFont font, string text, float scale)
            => string.IsNullOrEmpty(text) ? 0f : font.MeasureString(text).X * scale;

        /// <summary>近似原生行区域尺寸(蓝框覆盖用,只需保守不小于)</summary>
        private static Vector2 MeasureNativeLines(DynamicSpriteFont font, ReadOnlyCollection<TooltipLine> lines) {
            Vector2 size = Vector2.Zero;
            foreach (TooltipLine line in lines) {
                Vector2 measured = font.MeasureString(line.Text ?? "");
                size.X = Math.Max(size.X, measured.X);
                size.Y += measured.Y;
            }
            return size;
        }
    }
}
