using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 旧网遗物：纯视觉静态陈设 tile，零交互零收益（与可交互节点严格区分，防误点）。
    /// 六样式经 TileFrameX 编码，TileFrameY 存变体位与焦黑位；
    /// 撒布走 ctx.Scatter（P55），结构内由建造代码直调 TryWrite
    /// </summary>
    internal class OldNetRelicTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //════════ 样式编码（TileFrameX = 样式 ×18） ════════
        internal const int StyleScreens = 0;    //屏堆：叠放的熄灭显示屏
        internal const int StyleCableDrum = 1;  //缆盘：侧倒的线缆卷盘
        internal const int StyleChair = 2;      //倾倒座椅：向后翻倒的工位椅
        internal const int StylePlaque = 3;     //全息铭牌：唯一残留微光的样式
        internal const int StyleUrn = 4;        //数据瓮：陶瓮形存储罐
        internal const int StylePipeStub = 5;   //断管头：撕裂的立管残段
        internal const int StyleCount = 6;

        //死金属色板：遗物是"死物"，主体不发光（幸存冷青只留给铭牌微闪）
        private static readonly Color Steel = new(96, 104, 118);
        private static readonly Color SteelDark = new(58, 63, 74);
        private static readonly Color SteelEdge = new(150, 160, 175);
        private static readonly Color SurvivorCyan = new(140, 220, 235);
        private static readonly Color CharBlack = new(45, 34, 32);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //不可采掘：陈设层不进玩家物流
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(110, 72, 66), CreateMapEntryName());
        }

        /// <summary>
        /// 生成期写入口（契约 C2）：变体掷 + 节点让位检查在内部完成。
        /// 撒布引擎与结构建造代码共用；拒绝时调用方跳过即可（fail loud 由撒布日志兜底）。
        /// 焦黑变体按所在带自动判定（衰减区全焦黑）
        /// </summary>
        internal static bool TryWrite(int x, int y, int style) {
            if (style < 0 || style >= StyleCount || !WorldGen.InWorld(x, y, 4)) {
                return false;
            }
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            //让位检查（契约 C4：不碰 ScatterPass 的去重表，反向让位写在这里）——白名单反转：
            //附近 2 格内命中任意非本类 ModTile（节点/终端/装置，含各包新增与未来新增）一律让位。
            //旧网结构体全部原版砖（零 PNG 政策），不会误伤；遗物在 P55 条目序中恒最后落
            int relicType = ModContent.TileType<OldNetRelicTile>();
            for (int dx = -2; dx <= 2; dx++) {
                for (int dy = -2; dy <= 2; dy++) {
                    Tile near = Main.tile[x + dx, y + dy];
                    if (!near.HasTile) {
                        continue;
                    }
                    if (near.TileType >= TileID.Count && near.TileType != relicType) {
                        return false;
                    }
                    //遗物彼此贴脸也拒（防两件陈设视觉粘连）
                    if (near.TileType == relicType && Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) {
                        return false;
                    }
                }
            }

            //变体位：低 2 位=翻转/倾斜种子；第 3 位=焦黑（衰减区自动）
            int variant = WorldGen.genRand.Next(4);
            bool scorched = OldNetMetrics.BandIndexForColumn(x) == 3;
            Tile slot = Main.tile[x, y];
            slot.HasTile = true;
            slot.TileType = (ushort)relicType;
            slot.TileFrameX = (short)(style * 18);
            slot.TileFrameY = (short)((variant + (scorched ? 4 : 0)) * 18);
            return true;
        }

        /// <summary>带内加权掷样式：Z1 缆盘/座椅为主，Z2 屏堆/数据瓮为主，Z3 全样式均布</summary>
        internal static int RollStyle(int bandIndex) {
            int roll = WorldGen.genRand.Next(100);
            //权重表按带讲年代：整洁遗留 / 设施残骸 / 焦黑均布
            int[] weights = bandIndex switch {
                //屏堆/缆盘/座椅/铭牌/瓮/断管
                1 => [12, 28, 28, 16, 10, 6],
                2 => [30, 14, 10, 10, 24, 12],
                _ => [17, 17, 17, 17, 16, 16],
            };
            int acc = 0;
            for (int s = 0; s < StyleCount; s++) {
                acc += weights[s];
                if (roll < acc) {
                    return s;
                }
            }
            return StyleScreens;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //只有铭牌样式残留微光（0.05 级），其余样式是彻底的死物
            if (Main.tile[i, j].TileFrameX / 18 != StylePlaque) {
                return;
            }
            float flick = PlaqueFlicker(i, j);
            r = 0.03f * flick;
            g = 0.05f * flick;
            b = 0.06f * flick;
        }

        //铭牌 2 秒周期微闪 + 哈希断闪（濒死设备的呼吸）
        private static float PlaqueFlicker(int i, int j) {
            float seed = (i * 7 + j * 13) * 0.7f;
            float t = Main.GlobalTimeWrappedHourly;
            float wave = 0.55f + 0.45f * MathF.Sin(t * MathF.PI + seed);
            float stutter = (t * 0.5f + seed) % 1f;
            if (stutter < 0.08f) {
                wave *= 0.2f;
            }
            return wave;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //刻意不进 OldNetTileFX、不走 shader：遗物的材质身份是"死物"（回声节点同型决策）
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Tile tile = Main.tile[i, j];
            int style = Math.Clamp(tile.TileFrameX / 18, 0, StyleCount - 1);
            int packed = tile.TileFrameY / 18;
            int variant = packed & 3;
            bool scorched = (packed & 4) != 0;
            //variant 低位=水平镜像，高位=整体倾斜抖动
            float flip = (variant & 1) == 0 ? 1f : -1f;
            float lean = ((variant & 2) == 0 ? 1f : -1f) * 0.05f;

            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            //锚定格底中心：陈设坐在地上
            Vector2 ground = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            //死物吃环境光（有别于自发光节点），铭牌荧面单独豁免
            Color lightC = Lighting.GetColor(i, j);

            switch (style) {
                case StyleScreens: DrawScreens(spriteBatch, px, ground, flip, lean, scorched, lightC); break;
                case StyleCableDrum: DrawCableDrum(spriteBatch, px, ground, flip, lean, scorched, lightC); break;
                case StyleChair: DrawChair(spriteBatch, px, ground, flip, lean, scorched, lightC); break;
                case StylePlaque: DrawPlaque(spriteBatch, px, ground, i, j, flip, scorched, lightC); break;
                case StyleUrn: DrawUrn(spriteBatch, px, ground, flip, lean, scorched, lightC); break;
                default: DrawPipeStub(spriteBatch, px, ground, flip, lean, scorched, lightC); break;
            }
            return false;
        }

        //──────────── 绘制基元：1px 拉伸矩条（回声节点 DrawDiamondOutline 同技法） ────────────

        private static void Bar(SpriteBatch sb, Texture2D px, Vector2 pos, float w, float h,
            float rot, Color color) {
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), color, rot,
                new Vector2(0.5f, 0.5f), new Vector2(w, h), SpriteEffects.None, 0f);
        }

        //材质调色：焦黑压向炭色，再乘环境光
        private static Color Tone(Color c, bool scorched, Color lightC) {
            if (scorched) {
                c = Color.Lerp(c, CharBlack, 0.72f);
            }
            return new Color(c.R * lightC.R / 255, c.G * lightC.G / 255, c.B * lightC.B / 255, c.A);
        }

        //──────────── 六样式（剪影优先：外轮廓一眼分清） ────────────

        //屏堆：一块平躺、一块斜靠、一块立在后面的熄灭显示屏
        private static void DrawScreens(SpriteBatch sb, Texture2D px, Vector2 g,
            float flip, float lean, bool scorched, Color lc) {
            Color body = Tone(Steel, scorched, lc);
            Color dark = Tone(SteelDark, scorched, lc);
            Color edge = Tone(SteelEdge, scorched, lc);
            //平躺屏
            Bar(sb, px, g + new Vector2(0f, -2f), 14f, 4f, lean, body);
            Bar(sb, px, g + new Vector2(0f, -4.2f), 14f, 1f, lean, edge);
            //斜靠屏（屏面朝外的暗色面板）
            Bar(sb, px, g + new Vector2(-3f * flip, -7f), 12f, 3f, -0.55f * flip + lean, dark);
            Bar(sb, px, g + new Vector2(-4.4f * flip, -8.4f), 10f, 1f, -0.55f * flip + lean, edge);
            //后立屏
            Bar(sb, px, g + new Vector2(4f * flip, -9f), 10f, 3f, -1.15f * flip, dark);
        }

        //缆盘：侧倒卷盘，双法兰+轴芯+绕线，一截缆线拖在地上
        private static void DrawCableDrum(SpriteBatch sb, Texture2D px, Vector2 g,
            float flip, float lean, bool scorched, Color lc) {
            Color body = Tone(Steel, scorched, lc);
            Color dark = Tone(SteelDark, scorched, lc);
            Color edge = Tone(SteelEdge, scorched, lc);
            Vector2 c = g + new Vector2(0f, -6f);
            //双法兰
            Bar(sb, px, c + new Vector2(-5f, 0f), 2f, 12f, lean, edge);
            Bar(sb, px, c + new Vector2(5f, 0f), 2f, 12f, lean, edge);
            //轴芯
            Bar(sb, px, c, 9f, 2f, lean, body);
            //绕线三道
            Bar(sb, px, c + new Vector2(0f, -3.4f), 9f, 1.4f, lean, dark);
            Bar(sb, px, c + new Vector2(0f, 3.4f), 9f, 1.4f, lean, dark);
            Bar(sb, px, c + new Vector2(0f, 5.2f), 9f, 1f, lean, dark);
            //垂地拖缆
            Bar(sb, px, g + new Vector2(9f * flip, -0.8f), 8f, 1.2f, 0.12f * flip, dark);
        }

        //倾倒座椅：向后翻倒，座面朝天、椅腿翘起
        private static void DrawChair(SpriteBatch sb, Texture2D px, Vector2 g,
            float flip, float lean, bool scorched, Color lc) {
            Color body = Tone(Steel, scorched, lc);
            Color dark = Tone(SteelDark, scorched, lc);
            //躺平的靠背
            Bar(sb, px, g + new Vector2(-4f * flip, -1.6f), 10f, 2f, 0.1f * flip + lean, dark);
            //翘起的座面
            Bar(sb, px, g + new Vector2(2.5f * flip, -5f), 8f, 2f, -0.95f * flip, body);
            //两根朝天椅腿
            Bar(sb, px, g + new Vector2(5.5f * flip, -9f), 6f, 1.4f, -1.35f * flip, dark);
            Bar(sb, px, g + new Vector2(8f * flip, -7.5f), 5f, 1.2f, -1.1f * flip, dark);
        }

        //全息铭牌：基座立柱 + 悬浮荧板（唯一发光样式，2 秒微闪）
        private static void DrawPlaque(SpriteBatch sb, Texture2D px, Vector2 g,
            int i, int j, float flip, bool scorched, Color lc) {
            Color post = Tone(Steel, scorched, lc);
            Color baseC = Tone(SteelDark, scorched, lc);
            //基座与立柱（吃光照）
            Bar(sb, px, g + new Vector2(0f, -1f), 6f, 2f, 0f, baseC);
            Bar(sb, px, g + new Vector2(0f, -5f), 2f, 6f, 0f, post);
            //荧板（自发光，不乘环境光；焦黑变体光更弱）
            float flick = PlaqueFlicker(i, j) * (scorched ? 0.45f : 1f);
            Vector2 pc = g + new Vector2(1.5f * flip, -13f);
            Bar(sb, px, pc, 12f, 8f, 0f, SurvivorCyan * (0.16f * flick));
            Bar(sb, px, pc + new Vector2(0f, -4.4f), 12f, 1f, 0f, SurvivorCyan * (0.5f * flick));
            Bar(sb, px, pc + new Vector2(0f, 4.4f), 12f, 1f, 0f, SurvivorCyan * (0.5f * flick));
            //两行残余"文本"
            Bar(sb, px, pc + new Vector2(-1f * flip, -1.6f), 7f, 1f, 0f, SurvivorCyan * (0.65f * flick));
            Bar(sb, px, pc + new Vector2(1f * flip, 1.4f), 5f, 1f, 0f, SurvivorCyan * (0.55f * flick));
        }

        //数据瓮：收腰堆叠的存储罐，颈窄肩宽 + 盖顶
        private static void DrawUrn(SpriteBatch sb, Texture2D px, Vector2 g,
            float flip, float lean, bool scorched, Color lc) {
            Color body = Tone(Steel, scorched, lc);
            Color dark = Tone(SteelDark, scorched, lc);
            Color edge = Tone(SteelEdge, scorched, lc);
            //瓮身逐层（自下而上：足/腹/肩）
            float[] widths = [6f, 10f, 12f, 12f, 10f, 6f];
            for (int k = 0; k < widths.Length; k++) {
                Color layer = k % 2 == 0 ? body : dark;
                Bar(sb, px, g + new Vector2(0f, -1f - k * 2f), widths[k], 2f, lean, layer);
            }
            //颈与盖
            Bar(sb, px, g + new Vector2(0f, -13.4f), 4f, 2f, lean, dark);
            Bar(sb, px, g + new Vector2(0f, -15f), 6f, 1.4f, lean, edge);
            //裂纹（斜细线）
            Bar(sb, px, g + new Vector2(2f * flip, -5f), 6f, 0.8f, 1.1f * flip, Tone(CharBlack, false, lc));
        }

        //断管头：立管+断裂弯头，撕口毛边与黑洞口
        private static void DrawPipeStub(SpriteBatch sb, Texture2D px, Vector2 g,
            float flip, float lean, bool scorched, Color lc) {
            Color body = Tone(Steel, scorched, lc);
            Color dark = Tone(SteelDark, scorched, lc);
            Color edge = Tone(SteelEdge, scorched, lc);
            //立管
            Bar(sb, px, g + new Vector2(0f, -7f), 4f, 14f, lean, body);
            Bar(sb, px, g + new Vector2(-2.2f, -7f), 1f, 14f, lean, edge);
            //断裂弯头（朝 flip 侧撕开）
            Bar(sb, px, g + new Vector2(3f * flip, -14.5f), 6f, 4f, 0.15f * flip, body);
            //洞口黑芯
            Bar(sb, px, g + new Vector2(5.5f * flip, -14.5f), 2f, 2.4f, 0.15f * flip, Tone(CharBlack, false, lc));
            //撕口毛边三根
            Bar(sb, px, g + new Vector2(5f * flip, -17f), 3f, 1f, -0.7f * flip, dark);
            Bar(sb, px, g + new Vector2(6.5f * flip, -15.8f), 2.6f, 0.9f, -0.2f * flip, dark);
            Bar(sb, px, g + new Vector2(6f * flip, -12.6f), 2.4f, 0.9f, 0.5f * flip, dark);
        }
    }
}
