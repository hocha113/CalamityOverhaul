using CalamityOverhaul.Content.HackTimes.PvP.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Chat;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 信道乱码的防守方本机渲染钩子。三个拦截面：<br/>
    /// · 聊天行：挂 <c>RemadeChatMonitor.AddNewMessage / DrawChat</c>——
    ///   效果期间新到的行只做<b>标记</b>，绘制时按标记临时换成乱码字形；
    ///   消息存储原文一字不改（§7.7 表达边界），到期后连乱码期的旧行都按原文渲染；<br/>
    /// · 队友抬头名牌："Vanilla: MP Player Names" 图层整层熄灭
    ///   （原版只对同队成员显示名牌，它就是战场内的队伍位置信标）；<br/>
    /// · 地图队伍图钉：<c>PreDrawMapIconOverlay</c> 里隐藏 <see cref="PingMapLayer"/>。<br/>
    /// 判定只读本机帐本，其他玩家与服务端上这些钩子全部原样放行
    /// </summary>
    internal sealed class ChannelScrambleChatHook : ModSystem
    {
        /// <summary>字符替换率（设计值 40%）</summary>
        private const float GarbleRatio = 0.4f;
        /// <summary>故障字形池：全部取自默认字体必有的 ASCII 符号，不赌字形回退</summary>
        private const string GlitchGlyphs = "#%&$@*+=<>/\\|~^;:_";
        /// <summary>乱码重掷周期（帧），读作闪变的损坏而不是频闪噪声</summary>
        private const int RerollFrames = 12;

        private static FieldInfo messagesField;
        private static FieldInfo startChatLineField;
        private static FieldInfo showCountField;

        /// <summary>
        /// 乱码期到达的聊天行标记 + 乱码渲染缓存。键是消息容器本身，随聊天表淘汰自回收；
        /// 这是"每条聊天行"的本机表现状态，不是协议 per-effect 状态
        /// （那份在 effect.ProtocolState），不违反"不开协议侧静态字典"
        /// </summary>
        private static readonly ConditionalWeakTable<ChatMessageContainer, GarbleMark>
            marks = new();

        private sealed class GarbleMark
        {
            public float Seed;
            public int Step = -1;
            public TextSnippet[][] Lines;
        }

        public override void Load() {
            if (Main.dedServ) {
                return;   //服务端没有聊天渲染，三个拦截面都是纯客户端的
            }
            messagesField = typeof(RemadeChatMonitor).GetField("_messages",
                BindingFlags.NonPublic | BindingFlags.Instance);
            startChatLineField = typeof(RemadeChatMonitor).GetField("_startChatLine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            showCountField = typeof(RemadeChatMonitor).GetField("_showCount",
                BindingFlags.NonPublic | BindingFlags.Instance);
            //私有字段缺位（上游改名）时 HookDrawChat 直接走原版，协议退化为
            //HUD 条目 + 名牌/图钉隐藏，不炸
            On_RemadeChatMonitor.AddNewMessage += HookAddNewMessage;
            On_RemadeChatMonitor.DrawChat += HookDrawChat;
        }

        //On_ 钩子由 tML 随模组卸载自动摘除；只清反射缓存
        public override void Unload() {
            messagesField = null;
            startChatLineField = null;
            showCountField = null;
        }

        private static void HookAddNewMessage(
            On_RemadeChatMonitor.orig_AddNewMessage orig, RemadeChatMonitor self,
            string text, Color color, int widthLimitInPixels) {
            orig(self, text, color, widthLimitInPixels);
            if (!PvPDefenderLocal.HasEffect<ChannelScramble>()
                || messagesField?.GetValue(self)
                    is not List<ChatMessageContainer> list
                || list.Count == 0) {
                return;
            }
            //只标记，不动内容：新行插在表头（原版 Insert(0, ...)）
            marks.GetValue(list[0],
                static _ => new GarbleMark { Seed = Main.rand.NextFloat(1000f) });
        }

        private static void HookDrawChat(On_RemadeChatMonitor.orig_DrawChat orig,
            RemadeChatMonitor self, bool drawingPlayerChat) {
            if (!PvPDefenderLocal.HasEffect<ChannelScramble>()
                || messagesField?.GetValue(self)
                    is not List<ChatMessageContainer> list
                || startChatLineField == null || showCountField == null) {
                orig(self, drawingPlayerChat);
                return;
            }

            //复刻原版遍历（滚动偏移 + 逐行下移），仅把标记行的字形换成乱码。
            //乱码期悬停/点击整体停用：损坏的信道没有可交互物
            int remaining = (int)startChatLineField.GetValue(self);
            int showCount = (int)showCountField.GetValue(self);
            int msgIndex = 0;
            int lineInMsg = 0;
            while (remaining > 0 && msgIndex < list.Count) {
                int consumed = Math.Min(remaining, list[msgIndex].LineCount);
                remaining -= consumed;
                lineInMsg += consumed;
                if (lineInMsg == list[msgIndex].LineCount) {
                    lineInMsg = 0;
                    msgIndex++;
                }
            }

            int shown = 0;
            while (shown < showCount && msgIndex < list.Count) {
                ChatMessageContainer container = list[msgIndex];
                if (!container.Prepared
                    || !(drawingPlayerChat | container.CanBeShownWhenChatIsClosed)) {
                    break;
                }
                TextSnippet[] snippets = container.GetSnippetWithInversedIndex(lineInMsg);
                if (marks.TryGetValue(container, out GarbleMark mark)) {
                    snippets = GarbleLine(mark, container, lineInMsg, snippets);
                }
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch,
                    FontAssets.MouseText.Value, snippets,
                    new Vector2(88f, Main.screenHeight - 30 - 28 - shown * 21),
                    0f, Vector2.Zero, Vector2.One, out _);
                shown++;
                lineInMsg++;
                if (lineInMsg >= container.LineCount) {
                    lineInMsg = 0;
                    msgIndex++;
                }
            }
        }

        /// <summary>取该行的乱码渲染（按重掷周期缓存，避免逐帧重建字符串）</summary>
        private static TextSnippet[] GarbleLine(GarbleMark mark,
            ChatMessageContainer container, int lineIndex, TextSnippet[] source) {
            int step = (int)(Main.GameUpdateCount / RerollFrames);
            if (mark.Step != step || mark.Lines == null
                || mark.Lines.Length != container.LineCount) {
                mark.Step = step;
                mark.Lines = new TextSnippet[container.LineCount][];
            }
            if (lineIndex < 0 || lineIndex >= mark.Lines.Length) {
                return source;
            }
            return mark.Lines[lineIndex] ??= BuildGarbled(source,
                mark.Seed + lineIndex * 31.7f + step * 7.31f);
        }

        private static TextSnippet[] BuildGarbled(TextSnippet[] source, float seed) {
            var result = new TextSnippet[source.Length];
            for (int i = 0; i < source.Length; i++) {
                string text = source[i].Text ?? string.Empty;
                char[] chars = text.ToCharArray();
                for (int c = 0; c < chars.Length; c++) {
                    if (char.IsWhiteSpace(chars[c])) {
                        continue;   //保留断词节奏，乱码读作"损坏的话"而不是色块
                    }
                    float h = Hash(seed + i * 97.3f + c * 3.31f);
                    if (h < GarbleRatio) {
                        chars[c] = GlitchGlyphs[
                            (int)(Hash(h * 251.7f + c) * GlitchGlyphs.Length)
                            % GlitchGlyphs.Length];
                    }
                }
                //拍平成纯文本片段：物品图标等富标签一并乱码化（信道整体损坏）
                result[i] = new TextSnippet(new string(chars),
                    Color.Lerp(source[i].Color, PvPTheme.Hostile, 0.35f));
            }
            return result;
        }

        #region 队伍层信号：抬头名牌 + 地图图钉

        private const string PlayerNamesLayer = "Vanilla: MP Player Names";

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!PvPDefenderLocal.HasEffect<ChannelScramble>()) {
                return;
            }
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i].Name == PlayerNamesLayer) {
                    layers[i].Active = false;
                    return;
                }
            }
        }

        public override void PreDrawMapIconOverlay(IReadOnlyList<IMapLayer> layers,
            MapOverlayDrawContext mapOverlayDrawContext) {
            if (!PvPDefenderLocal.HasEffect<ChannelScramble>()) {
                return;
            }
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i] is PingMapLayer) {
                    layers[i].Hide();
                }
            }
        }

        #endregion

        private static float Hash(float p) {
            p = MathF.Abs(p * 0.1031f % 1f);
            p *= p + 33.33f;
            p *= p + p;
            return MathF.Abs(p % 1f);
        }
    }
}
