using CalamityOverhaul.Content.HackTimes;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.Architectures.SignalTowers.Decryption
{
    /// <summary>
    /// 破译消息片段：表示一条可以逐字符显示的日志行
    /// 源（Source）显示在行首方括号内
    /// </summary>
    internal class DecryptedLine
    {
        public string Source;
        public string Body;
        public Color Tint;
        public bool IsHeader;
    }

    /// <summary>
    /// 解码消息阶段
    /// 打字机方式逐字符把多行日志吐到屏幕上，所有行显示完毕允许玩家手动关闭
    /// 具体条目内容预留给本地化/外部脚本注入
    /// </summary>
    internal class MessageRevealStage
    {
        private const float CharsPerSecond = 60f;

        public List<DecryptedLine> Lines { get; } = [];
        public float RevealedChars { get; private set; }
        public int TotalChars { get; private set; }
        public bool FullyRevealed => RevealedChars >= TotalChars;
        public float IdleTimer { get; private set; }

        public void SetContent(IEnumerable<DecryptedLine> lines) {
            Lines.Clear();
            Lines.AddRange(lines);
            RevealedChars = 0f;
            TotalChars = 0;
            foreach (var ln in Lines) TotalChars += (ln.Body?.Length ?? 0);
            IdleTimer = 0f;
        }

        public void Clear() {
            Lines.Clear();
            RevealedChars = 0f;
            TotalChars = 0;
            IdleTimer = 0f;
        }

        public void Update(float dt) {
            if (!FullyRevealed) {
                RevealedChars += CharsPerSecond * dt;
                if (RevealedChars > TotalChars) RevealedChars = TotalChars;
                //每8字符嘀一声
                int before = (int)(RevealedChars - CharsPerSecond * dt);
                int after = (int)RevealedChars;
                if (after / 8 > before / 8 && after <= TotalChars) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.25f, Pitch = 0.3f });
                }
            }
            else {
                IdleTimer += dt;
            }
        }

        public void SkipToEnd() {
            RevealedChars = TotalChars;
        }
    }

    /// <summary>
    /// 消息展示本地化
    /// 零号基地中破译到的文本：嘉登关于亚空间越迁事件与虚空部落建立的示警记录
    /// </summary>
    internal class MessageStrings : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.VoidColony";
        public static LocalizedText Header { get; private set; }
        public static LocalizedText AccessLine { get; private set; }
        public static LocalizedText StatusLine { get; private set; }
        public static LocalizedText SourceSystem { get; private set; }
        public static LocalizedText SourceSignal { get; private set; }
        public static LocalizedText SourceArchive { get; private set; }
        public static LocalizedText FragmentTag { get; private set; }
        public static LocalizedText SignatureTag { get; private set; }
        //五个记录段落：每个段包含Title + 多条Body
        public static LocalizedText Record01Title { get; private set; }
        public static LocalizedText[] Record01Body { get; private set; }
        public static LocalizedText Record02Title { get; private set; }
        public static LocalizedText[] Record02Body { get; private set; }
        public static LocalizedText Record03Title { get; private set; }
        public static LocalizedText[] Record03Body { get; private set; }
        public static LocalizedText Record04Title { get; private set; }
        public static LocalizedText[] Record04Body { get; private set; }
        public static LocalizedText SkipHint { get; private set; }
        public static LocalizedText CloseHint { get; private set; }

        public override void SetStaticDefaults() {
            Header = this.GetLocalization("MsgHeader", () => "【零号基地档案】");
            AccessLine = this.GetLocalization("MsgAccess", () => "访问权限： Draedon");
            StatusLine = this.GetLocalization("MsgStatus", () => "状态： 局部解密");
            SourceSystem = this.GetLocalization("MsgSrcSystem", () => "SYS");
            SourceSignal = this.GetLocalization("MsgSrcSignal", () => "SIG");
            SourceArchive = this.GetLocalization("MsgSrcArchive", () => "ARC");
            FragmentTag = this.GetLocalization("MsgFragment", () => "FRAGMENT");
            SignatureTag = this.GetLocalization("MsgSignature", () => "SIGN   ▸ {0}");

            Record01Title = this.GetLocalization("MsgRec01Title", () => "[记录-01]");
            Record01Body = new[] {
                this.GetLocalization("MsgRec01_0", () => "大尖啸。无法解析波段，像是整个宇宙都在尖叫。"),
                this.GetLocalization("MsgRec01_1", () => "空间曲率永久锁死。折跃引擎彻底报废。强行跃迁的结果只有物质结构的解体。"),
                this.GetLocalization("MsgRec01_2", () => "常规星际航行终结。旧物理统一模型崩溃。"),
            };
            Record02Title = this.GetLocalization("MsgRec02Title", () => "[记录-02]");
            Record02Body = new[] {
                this.GetLocalization("MsgRec02_0", () => "七万三千次探测器损毁后，锁定当前虚空坐标。"),
                this.GetLocalization("MsgRec02_1", () => "建立零号基地。铺设配套工业群。"),
                this.GetLocalization("MsgRec02_2", () => "此处的现实壁垒已被拉扯至极限，可以直接目视那片无序的亚空间风暴。"),
                this.GetLocalization("MsgRec02_3", () => "绝佳的观测点。"),
            };
            Record03Title = this.GetLocalization("MsgRec03Title", () => "[记录-03]");
            Record03Body = new[] {
                this.GetLocalization("MsgRec03_0", () => "风暴正在渗漏。"),
                this.GetLocalization("MsgRec03_1", () => "物理学退位。绝对的唯心规则开始接管这片工业区。"),
                this.GetLocalization("MsgRec03_2", () => "时间线出现重叠。尸体、过往残影与亚空间能量开始结合。"),
                this.GetLocalization("MsgRec03_3", () => "工业区中开始频繁观察到怪异现象。"),
            };
            Record04Title = this.GetLocalization("MsgRec04Title", () => "[记录-04]");
            Record04Body = new[] {
                this.GetLocalization("MsgRec04_0", () => "清理这些灵异对象属于徒劳，它们无法被杀死。留作基地的外部屏障。"),
                this.GetLocalization("MsgRec04_1", () => "若该日志被触发，意味着我已经放弃此地，所有向外界的通道已经关闭。"),
                this.GetLocalization("MsgRec04_2", () => "后来者，自行在那些脏东西的规律里寻找机会。"),
            };
            SkipHint = this.GetLocalization("MsgSkipHint", () => "[空格/左键] 跳过逐字显示");
            CloseHint = this.GetLocalization("MsgCloseHint", () => "[空格/ESC] 断开连接");
        }

        /// <summary>生成零号基地嘉登档案的完整逐行脚本</summary>
        public static List<DecryptedLine> BuildDefaultPayload(SignalTowerActor tower) {
            int id = tower?.WhoAmI ?? 0;
            //预设配色：档案标题青、权限黄、状态绿、每段记录不同色调
            Color titleCol = new(120, 230, 255);
            Color accessCol = new(255, 220, 140);
            Color statusCol = new(160, 255, 200);
            Color recordTitleCol = new(220, 200, 255);
            Color bodyColA = new(210, 235, 255);
            Color bodyColB = new(230, 220, 195);
            Color sigCol = new(180, 200, 220);

            var list = new List<DecryptedLine>();
            //首部三行：档案标题 + 权限 + 状态
            list.Add(new DecryptedLine { Source = "", Body = Header.Value, Tint = titleCol, IsHeader = true });
            list.Add(new DecryptedLine { Source = "", Body = AccessLine.Value, Tint = accessCol, IsHeader = true });
            list.Add(new DecryptedLine { Source = "", Body = StatusLine.Value, Tint = statusCol, IsHeader = true });

            //四段记录：每段一个黄紫色档案标题 + 多行Body交替色
            void AppendRecord(LocalizedText title, LocalizedText[] body, string src) {
                list.Add(new DecryptedLine { Source = "", Body = title.Value, Tint = recordTitleCol, IsHeader = true });
                for (int i = 0; i < body.Length; i++) {
                    list.Add(new DecryptedLine {
                        Source = src,
                        Body = body[i].Value,
                        Tint = (i % 2 == 0) ? bodyColA : bodyColB,
                        IsHeader = false,
                    });
                }
            }
            AppendRecord(Record01Title, Record01Body, SourceSystem.Value);
            AppendRecord(Record02Title, Record02Body, SourceArchive.Value);
            AppendRecord(Record03Title, Record03Body, SourceSignal.Value);
            AppendRecord(Record04Title, Record04Body, SourceArchive.Value);

            //签名行：以Draedon哈希收尾
            list.Add(new DecryptedLine {
                Source = "",
                Body = SignatureTag.Format($"DRAEDON-{unchecked((uint)HashCode.Combine(id, 0xD12AED0Eu)):X8}"),
                Tint = sigCol,
                IsHeader = true,
            });
            return list;
        }
    }

    /// <summary>消息阶段渲染</summary>
    internal static class MessageRevealRenderer
    {
        public static void Draw(SpriteBatch sb, MessageRevealStage stage, Rectangle body, float eased, Color accent) {
            if (stage == null) return;
            Texture2D px = TextureAssets.MagicPixel.Value;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //整体背景板
            sb.Draw(px, body, Color.Black * (0.55f * eased));
            sb.Draw(px, new Rectangle(body.X, body.Y, body.Width, 1), accent * (eased * 0.65f));
            sb.Draw(px, new Rectangle(body.X, body.Bottom - 1, body.Width, 1), accent * (eased * 0.45f));

            //底部hint栏：先预留出独立区域，避免与正文叠绘
            int hintAreaH = 22;
            int textBottom = body.Bottom - hintAreaH;
            //hint区背景，吸住下方的文本，与正文形成视觉分隔
            Rectangle hintArea = new Rectangle(body.X, textBottom, body.Width, hintAreaH);
            sb.Draw(px, hintArea, Color.Black * (0.42f * eased));
            sb.Draw(px, new Rectangle(hintArea.X, hintArea.Y, hintArea.Width, 1), accent * (eased * 0.35f));

            int lineHeight = 18;
            int padX = 14;
            float textScale = 0.6f;
            int viewTop = body.Y + 10;
            int viewBottom = textBottom - 4;
            int viewH = viewBottom - viewTop;
            int totalH = stage.Lines.Count * lineHeight;

            //定位当前正在打字的行索引，用于滚动锚点
            int activeIdx = stage.Lines.Count - 1;
            {
                int acc = 0;
                int reveal = (int)stage.RevealedChars;
                for (int i = 0; i < stage.Lines.Count; i++) {
                    int len = stage.Lines[i].Body?.Length ?? 0;
                    if (acc + len >= reveal) { activeIdx = i; break; }
                    acc += len;
                }
            }

            //滚动偏移：当总高超过可视区时，让活动行保持在底部的1/3区域
            int scroll = 0;
            if (totalH > viewH) {
                int desired = (activeIdx + 1) * lineHeight - (viewH - lineHeight);
                scroll = Math.Clamp(desired, 0, totalH - viewH);
            }

            //逐行绘制，计算字符预算
            int budget = (int)stage.RevealedChars;
            int yLogical = 0;
            foreach (var ln in stage.Lines) {
                int y = viewTop + yLogical - scroll;
                yLogical += lineHeight;
                int want = ln.Body?.Length ?? 0;
                int show = Math.Min(want, budget);
                budget -= show;
                //超出可视区直接跳过，绝不绘制到hint条上
                if (y + lineHeight > viewBottom + 1) continue;
                if (y < viewTop - lineHeight) continue;
                string shownText = ln.Body == null ? string.Empty : ln.Body.Substring(0, show);

                //源tag
                int textX = body.X + padX;
                if (!string.IsNullOrEmpty(ln.Source)) {
                    string tag = $"[{ln.Source}]";
                    Vector2 ts = font.MeasureString(tag) * 0.6f;
                    Rectangle tb = new Rectangle(textX - 2, y - 1, (int)ts.X + 6, (int)ts.Y + 2);
                    sb.Draw(px, tb, Color.Black * (0.55f * eased));
                    sb.Draw(px, new Rectangle(tb.X, tb.Y, tb.Width, 1), ln.Tint * (eased * 0.9f));
                    sb.Draw(px, new Rectangle(tb.X, tb.Bottom - 1, tb.Width, 1), ln.Tint * (eased * 0.5f));
                    Utils.DrawBorderString(sb, tag, new Vector2(textX + 2, y), ln.Tint * eased, 0.6f);
                    textX += (int)ts.X + 12;
                }

                Color bodyCol = ln.IsHeader
                    ? ln.Tint * (eased * 0.95f)
                    : HackTheme.TextBright * (eased * 0.92f);
                //阴影
                Utils.DrawBorderString(sb, shownText,
                    new Vector2(textX, y), bodyCol, textScale);

                //未完的光标
                if (show < want) {
                    Vector2 ms = font.MeasureString(shownText) * textScale;
                    float blink = 0.5f + 0.5f * MathF.Sin(DecryptionSession.SessionTime * 10f);
                    sb.Draw(px, new Rectangle((int)(textX + ms.X + 2), y + 2, 2, lineHeight - 4),
                        accent * (eased * blink));
                    break;
                }
            }

            //底部状态栏：定位在已预留的hintArea中央
            string hintStr = stage.FullyRevealed
                ? MessageStrings.CloseHint.Value
                : MessageStrings.SkipHint.Value;
            Vector2 hs = font.MeasureString(hintStr) * 0.6f;
            Utils.DrawBorderString(sb, hintStr,
                new Vector2(body.X + (body.Width - hs.X) / 2, textBottom + (hintAreaH - hs.Y) / 2),
                new Color(180, 220, 255) * (eased * 0.9f), 0.6f);
        }
    }
}
