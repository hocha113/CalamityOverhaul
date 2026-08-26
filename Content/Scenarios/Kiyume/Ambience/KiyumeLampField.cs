using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>灯火登记表条目：origin 键（多格家具左上）+ 视觉中心（事件派声/烟用）</summary>
    internal readonly struct KiyumeLampEntry(Point16 key, Vector2 worldCenter)
    {
        internal readonly Point16 Key = key;
        internal readonly Vector2 WorldCenter = worldCenter;
    }

    /// <summary>
    /// 鬼梦灯火登记表（KIY-P5-C 基建）：进世界首个 update 帧惰性扫描村带
    /// [VillageLeft,GroveLeft)×[380,470)，类型 = Torches+Candles+Campfire（裁决 24——
    /// 村落纵深包会把部分窗火换成围炉/佛坛烛，只扫 Torch 会漏灯）
    /// + HangingLanterns（W4 生成域移交：灯道挂灯/殿内吊灯全是 tile 42），帽 128 盏。
    /// 扫描窗刻意只限村带：栈桥孤灯（滩涂）与石阶石灯（远山）不入表——
    /// 村带外灯位属二期「湖上孤灯」事件，已裁决留置，本轮不扩窗。
    /// litFactor 是「你看到的光」：经 <see cref="KiyumeLampLight"/> 逐盏乘暗，
    /// 不改任何 tile 帧（那是世界状态，DungeonworldSnuff 同款声明）。<br/>
    /// 线程契约：TileLightScanner.ExportTo 在 FastParallel 工作线程回调 ModifyLight
    /// （TML 源 TileLightScanner.cs 已核），登记表建满后一次性换引用发布，
    /// 此后只有 float 值覆写（4 字节原子写），永不增删键，字典无并发扩容风险。<br/>
    /// 权威端+同步字段：无。光照引擎本就逐客户端，全本地零包。<br/>
    /// P3 接口声明：v1 自扫（现状可用）；结构登记表暴露灯位列表后换源，扫描收在
    /// <see cref="Scan"/> 一处，换源是一行事。
    /// </summary>
    internal class KiyumeLampField : ModSystem
    {
        //litFactor 字典（键=家具 origin；null=未扫描或不在鬼梦，工作线程以此早退）
        private static Dictionary<Point16, float> lit;
        //枚举用条目表：与 lit 同批建成，此后只读（事件按索引取灯）
        private static readonly List<KiyumeLampEntry> entries = new(KiyumeScore.LampScanCap);
        //任一盏被压暗才走字典查询：写入沿立即置位（SetFactor），每帧扫表清位
        private static bool anyDimmed;
        private static bool scanned;

        internal static bool Scanned => scanned;
        internal static IReadOnlyList<KiyumeLampEntry> Entries => entries;
        //──KiyumeLampLight 热路径读口──
        internal static bool AnyDimmed => anyDimmed;
        internal static Dictionary<Point16, float> LitSnapshot => lit;

        internal static float GetFactor(Point16 key)
            => lit != null && lit.TryGetValue(key, out float f) ? f : 1f;

        internal static void SetFactor(Point16 key, float f) {
            if (lit == null || !lit.ContainsKey(key)) {
                return;
            }
            lit[key] = f;
            if (f < 0.999f) {
                anyDimmed = true;
            }
        }

        //==================== 生命周期 ====================

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();
        public override void Unload() => HardReset();

        private static void HardReset() {
            lit = null;
            entries.Clear();
            anyDimmed = false;
            scanned = false;
        }

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            if (!KiyumeWorld.Active || Main.gameMenu) {
                return;
            }
            //OnWorldLoad 时 tile 可能未全就绪：推迟到首个 update 帧惰性扫一次
            if (!scanned) {
                Scan();
                scanned = true;
            }
            //清位扫描：全部回亮后 ModifyLight 退回单 bool 早退
            if (anyDimmed && lit != null) {
                bool dim = false;
                foreach (float f in lit.Values) {
                    if (f < 0.999f) {
                        dim = true;
                        break;
                    }
                }
                anyDimmed = dim;
            }
        }

        //==================== 登记表扫描 ====================

        private static void Scan() {
            var built = new Dictionary<Point16, float>(KiyumeScore.LampScanCap);
            entries.Clear();
            for (int x = KiyumeMetrics.VillageLeft; x < KiyumeMetrics.GroveLeft; x++) {
                if (entries.Count >= KiyumeScore.LampScanCap) {
                    break;
                }
                for (int y = KiyumeScore.LampScanRowTop; y < KiyumeScore.LampScanRowBottom; y++) {
                    if (entries.Count >= KiyumeScore.LampScanCap) {
                        break;
                    }
                    Tile t = Framing.GetTileSafely(x, y);
                    if (!t.HasTile) {
                        continue;
                    }
                    Point16 key;
                    Vector2 center;
                    switch (t.TileType) {
                        case TileID.Torches:
                            //点燃帧 frameX<66（对源 TileLightScanner case 4），1×1
                            if (t.TileFrameX >= 66) {
                                continue;
                            }
                            key = new Point16(x, y);
                            center = new Vector2(x * 16f + 8f, y * 16f + 8f);
                            break;
                        case TileID.Candles:
                            //点燃帧 frameX==0（对源 case 33），1×1
                            if (t.TileFrameX != 0) {
                                continue;
                            }
                            key = new Point16(x, y);
                            center = new Vector2(x * 16f + 8f, y * 16f + 8f);
                            break;
                        case TileID.Campfire:
                            //点燃帧 frameY<36（对源 case 215）；3×2 六格皆发光，按左上归并防重复登记
                            if (t.TileFrameY >= 36) {
                                continue;
                            }
                            key = new Point16(x - t.TileFrameX % 54 / 18, y - t.TileFrameY % 36 / 18);
                            center = new Vector2(key.X * 16f + 24f, key.Y * 16f + 16f);
                            break;
                        case TileID.HangingLanterns:
                            //点燃帧 frameX==0（对源 TileLightScanner case 42，样式=frameY/36，熄灭帧 frameX 移列）；
                            //Style1x2Top 竖排两格皆发光，按顶格归并防重复登记
                            if (t.TileFrameX != 0) {
                                continue;
                            }
                            key = new Point16(x, y - t.TileFrameY % 36 / 18);
                            //视觉中心取灯体格（下格）：上格是链，声与烟从灯体出
                            center = new Vector2(x * 16f + 8f, (key.Y + 1) * 16f + 8f);
                            break;
                        default:
                            continue;
                    }
                    //营火/挂灯帧内多 tile 走到这里会撞键：TryAdd 失败即已登记
                    if (!built.TryAdd(key, 1f)) {
                        continue;
                    }
                    entries.Add(new KiyumeLampEntry(key, center));
                }
            }
            //建满后一次性发布：光照工作线程要么看到 null 要么看到完整表
            lit = built;
        }

        /// <summary>一行状态摘要（TestItem 验收用；量级预期：窗火/烛/围炉 ~30-45 + 灯道挂灯 ~45 ≈ 70-95）</summary>
        internal static string StatusLine() {
            int dimCount = 0;
            if (lit != null) {
                foreach (float f in lit.Values) {
                    if (f < 0.999f) {
                        dimCount++;
                    }
                }
            }
            return $"[灯火登记] {(scanned ? "已扫" : "未扫")} 登记{entries.Count}盏 压暗{dimCount}盏";
        }
    }

    /// <summary>
    /// 灯火过滤钩：TML 在 TileLightScanner.ApplyTileLight 尾调（仅 Main.tileLighted 类型），
    /// 纯客户端光照。类型过滤 = 裁决 24 三类 + 挂灯（W4 生成域移交）；
    /// 营火/挂灯任意格回算 origin 取同一份 litFactor。<br/>
    /// TODO(P5-C v2，按计划书 §4 v1 接受口径不实施)：litFactor=0 后火苗贴图仍在跳
    /// （光已灭、苗还画）。v1 接受：注册灯多在民居室内，村外读光为主，贴脸才穿帮。
    /// v2 修法已勘：GlobalTile.PreDraw 返 false 整体门控该 tile 绘制（TML
    /// TileDrawing.DrawSingleTile 入口，含火苗）+ PostDraw 自绘暗色杆身。
    /// </summary>
    internal class KiyumeLampLight : GlobalTile
    {
        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b) {
            //事件未压暗任何灯时单 bool 早退（主世界与鬼梦平时均零成本）
            if (!KiyumeLampField.AnyDimmed) {
                return;
            }
            Point16 key;
            if (type == TileID.Torches || type == TileID.Candles) {
                key = new Point16(i, j);
            }
            else if (type == TileID.Campfire) {
                Tile t = Main.tile[i, j];
                key = new Point16(i - t.TileFrameX % 54 / 18, j - t.TileFrameY % 36 / 18);
            }
            else if (type == TileID.HangingLanterns) {
                //1×2 竖排两格皆发光：回算顶格 origin 取同一份 litFactor
                Tile t = Main.tile[i, j];
                key = new Point16(i, j - t.TileFrameY % 36 / 18);
            }
            else {
                return;
            }
            var map = KiyumeLampField.LitSnapshot;
            if (map != null && map.TryGetValue(key, out float f) && f < 0.999f) {
                r *= f;
                g *= f;
                b *= f;
            }
        }
    }
}
