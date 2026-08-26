using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core
{
    //雕刻器:把规划投影成材质栅格,自身零随机决策(噪声场种子由rng派生,决定论)
    //顺序:基底浇筑→主沟列雕→假裂缝→平原→深部冲压→支沟溶洞→出生房→微光斑→封板
    internal static class HadalTerrainCarver
    {
        internal static HadalTerrainModel Carve(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng) {
            var m = new HadalTerrainModel(p, plan);
            HadalRng ns = rng.Fork(0x20);
            ulong sZone = ns.NextULong();
            ulong sStrata = ns.NextULong();
            ulong sLens = ns.NextULong();
            ulong sIntr = ns.NextULong();
            ulong sMix = ns.NextULong();
            ulong sBed = ns.NextULong();
            ulong sWall = ns.NextULong();
            ulong sSpur = ns.NextULong();
            ulong sVFloor = ns.NextULong();
            ulong sPlain = ns.NextULong();
            ulong sStamp = ns.NextULong();
            ulong sSand = ns.NextULong();

            BaseFill(m, sZone, sStrata, sLens, sIntr, sMix, sBed, sSand);
            CarveTrench(m, sWall, sSpur, sVFloor);
            CarveFalseCracks(m, sWall);
            (int[] plainFloor, int[] plainCeil) = CarvePlain(m, sPlain);
            StampDeepStructures(m, sStamp, plainFloor);
            StampGalleriesAndCaves(m, sStamp);
            BuildSpawnRoom(m);
            ApplyBioSpots(m, sStamp);
            SealTop(m);
            return m;
        }

        //——基底浇筑:海床以下按分带落材质(蓝图§4),层界全domain-warp——
        private static void BaseFill(HadalTerrainModel m, ulong sZone, ulong sStrata,
            ulong sLens, ulong sIntr, ulong sMix, ulong sBed, ulong sSand) {
            HadalGenParams p = m.P;
            int w = p.Width, h = p.Height;
            //暮光层理预建带表:石为骨架(约半数)夹淤泥/黏土/泥薄彩带,
            //厚度逐带起伏且严格单调推进(首轮预览的均匀条纹壁纸病由此修)
            ReadOnlySpan<HadalMat> seq = [
                HadalMat.Stone, HadalMat.Silt, HadalMat.Stone, HadalMat.Clay,
                HadalMat.Stone, HadalMat.Silt, HadalMat.Stone, HadalMat.Mud,
            ];
            const int strataTop = 380, strataBottom = 1520;
            var strataOfRow = new HadalMat[strataBottom - strataTop];
            {
                float yc = strataTop;
                int bi = 0;
                while (yc < strataBottom) {
                    HadalMat mt = seq[bi % seq.Length];
                    float baseThk = mt == HadalMat.Stone ? 34f : 15f;
                    float thk = baseThk * (0.55f + HadalNoise.Value1(bi * 3.71f, sStrata) * 1.05f);
                    int from = (int)yc;
                    yc += MathF.Max(4f, thk);
                    int to = Math.Min(strataBottom, (int)yc);
                    for (int row = from; row < to; row++) {
                        strataOfRow[row - strataTop] = mt;
                    }
                    bi++;
                }
            }
            for (int x = 0; x < w; x++) {
                int seabed = m.Plan.SeabedY[x];
                //分带界与沙层厚的横向摆动,消灭水平硬切线
                float zoneWarp = (HadalNoise.Fbm1(x * 0.006f, sZone, 3) - 0.5f) * 56f;
                float sandWarp = HadalNoise.Fbm1(x * 0.02f, sSand, 3);
                for (int y = 0; y < h; y++) {
                    if (y < seabed) {
                        continue; //海床上方:开阔水柱/天空,保持None
                    }
                    HadalMat mat;
                    if (y >= p.DeepestPlayableRow) {
                        //封底基岩:花岗岩基座+黑曜石团块
                        mat = HadalNoise.Fbm2(x * 0.006f, y * 0.006f, sBed, 3) > 0.6f
                            ? HadalMat.Obsidian : HadalMat.Granite;
                    }
                    else {
                        float zy = y + zoneWarp;
                        if (zy < p.SunlitBottom) {
                            int d = y - seabed;
                            mat = d < 9 + sandWarp * 6f ? HadalMat.Sand
                                : d < 26 + sandWarp * 12f ? HadalMat.HardSand
                                : HadalMat.Sandstone;
                        }
                        else if (zy < p.TwilightBottom) {
                            //沉积层理:带表查行+构造褶皱(x向低频起伏)+向沟心下垂
                            float dist = MathF.Min(400f, MathF.Abs(x - m.Plan.CenterX[y]));
                            float sag = dist * 0.055f;
                            float fold = (HadalNoise.Fbm1(x * 0.0035f, sStrata ^ 0x77UL, 3) - 0.5f) * 56f;
                            int row = Math.Clamp((int)(y - sag + fold), strataTop, strataBottom - 1);
                            mat = strataOfRow[row - strataTop];
                        }
                        else if (zy < p.MidnightBottom) {
                            //午夜:石为主+淤泥透镜+花岗岩侵入体
                            mat = HadalMat.Stone;
                            if (HadalNoise.Fbm2(x * 0.012f, y * 0.012f, sLens, 3) > 0.72f) {
                                mat = HadalMat.Silt;
                            }
                            else if (HadalNoise.Fbm2(x * 0.004f, y * 0.004f, sIntr, 3) > 0.74f) {
                                mat = HadalMat.Granite;
                            }
                        }
                        else if (zy < p.AbyssalBottom) {
                            //深渊:花岗岩/石冷硬混合
                            float t = HadalNoise.Fbm2(x * 0.006f, y * 0.006f, sMix, 4);
                            mat = t > 0.52f ? HadalMat.Granite : HadalMat.Stone;
                        }
                        else {
                            //超深渊:黑曜石/花岗岩混斑+灰烬透镜
                            float g = HadalNoise.Fbm2(x * 0.008f, y * 0.008f, sMix ^ 0x99UL, 4);
                            mat = g < 0.40f ? HadalMat.Granite : HadalMat.Obsidian;
                            if (HadalNoise.Fbm2(x * 0.02f, y * 0.02f, sLens ^ 0x33UL, 3) > 0.78f) {
                                mat = HadalMat.Ash;
                            }
                        }
                    }
                    m.Fill(x, y, mat);
                }
            }
        }

        //——主沟列式雕刻:中心线±半宽+壁面fBm侵蚀+午夜岩脊残留(蓝图§1.2)——
        private static void CarveTrench(HadalTerrainModel m, ulong sWall, ulong sSpur, ulong sVFloor) {
            HadalGenParams p = m.P;
            HadalTerrainPlan plan = m.Plan;
            for (int y = 110; y < p.Height; y++) {
                float hl = plan.HalfL[y];
                float hr = plan.HalfR[y];
                if (hl <= 0f && hr <= 0f) {
                    continue;
                }
                float c = plan.CenterX[y];
                float xaF = c - hl;
                float xbF = c + hr;
                float amp = EdgeAmp(p, y);
                bool allowSpur = y > p.TwilightBottom && y < 2690 && (xbF - xaF) > 30f;
                int xa = (int)MathF.Floor(xaF) - 8;
                int xb = (int)MathF.Ceiling(xbF) + 8;
                //V底走廊地板起伏:超过地板线不再下挖(蓝图§2.5)
                bool vFloorZone = y > 4640;
                for (int x = xa; x <= xb; x++) {
                    float inside = MathF.Min(x - xaF, xbF - x);
                    float wob = (HadalNoise.Fbm2(x * 0.045f, y * 0.045f, sWall, 3) - 0.5f) * 2f * amp;
                    if (inside + wob <= 0f) {
                        continue;
                    }
                    if (vFloorZone) {
                        float floorY = 4732f + (HadalNoise.Fbm1(x * 0.02f, sVFloor, 3) - 0.5f) * 36f;
                        if (y > floorY) {
                            continue;
                        }
                    }
                    //岩脊残留:贴壁窄条让山脊突进沟内,破"光滑槽"感
                    if (allowSpur && inside < 7f
                        && HadalNoise.Ridged2(x * 0.016f, y * 0.016f, sSpur, 3) > 0.8f) {
                        continue;
                    }
                    m.Carve(x, y);
                }
            }
        }

        private static float EdgeAmp(HadalGenParams p, int y) {
            if (y < p.SunlitBottom) {
                return 3.5f;
            }
            if (y < p.TwilightBottom) {
                return 3f;
            }
            if (y < 2690) {
                return 5f;
            }
            if (y < p.AbyssalBottom) {
                return 2.5f;
            }
            return 3.5f;
        }

        //——假裂缝:海床死端叙事沟(蓝图§2.1)——
        private static void CarveFalseCracks(HadalTerrainModel m, ulong sWall) {
            foreach ((int cx, int topW, int depth) in m.Plan.FalseCracks) {
                int bed = m.Plan.SeabedY[cx];
                for (int dy = -2; dy <= depth; dy++) {
                    int y = bed + dy;
                    float t = MathF.Max(0f, dy / (float)depth);
                    float half = topW * 0.5f * MathF.Pow(1f - t, 0.8f);
                    if (half < 0.6f) {
                        break;
                    }
                    float wobL = (HadalNoise.Fbm2(cx * 0.05f, y * 0.07f, sWall ^ 0xC1UL, 3) - 0.5f) * 3f;
                    for (int x = (int)(cx - half - wobL); x <= (int)(cx + half + wobL); x++) {
                        m.Carve(x, y);
                    }
                }
            }
        }

        //——深渊平原:透镜巨腔+垂乳顶+残柱+热液丘(蓝图§2.4)——
        private static (int[] floorY, int[] ceilY) CarvePlain(HadalTerrainModel m, ulong sPlain) {
            HadalGenParams p = m.P;
            HadalPlainSpec plain = m.Plan.Plain;
            int x0 = (int)(plain.CenterX - plain.HalfSpan);
            int x1 = (int)(plain.CenterX + plain.HalfSpan);
            float midBase = (plain.Top + plain.Bottom) * 0.5f;
            float halfBase = (plain.Bottom - plain.Top) * 0.5f;
            int[] floorY = new int[p.Width];
            int[] ceilY = new int[p.Width];
            for (int x = 0; x < p.Width; x++) {
                floorY[x] = -1;
            }

            //门槛喉落点先定,接驳井要用
            int linkY = m.Plan.PlainLinkY;
            int linkX = (int)Math.Clamp(m.Plan.CenterX[linkY],
                plain.CenterX - plain.HalfSpan * 0.6f, plain.CenterX + plain.HalfSpan * 0.6f);

            //自中心向两翼推进,遇顶底闭合即止:透镜尖端干净收口,不留密封死腔
            //(第三轮预览:尖端外侧droop把顶压过地板,越过封点继续挖就成孤腔)
            int centerIx = (int)plain.CenterX;
            foreach (int dir in stackalloc int[] { 1, -1 }) {
                for (int x = centerIx; x >= x0 && x <= x1; x += dir) {
                    float t = (x - plain.CenterX) / plain.HalfSpan;
                    float env = 1f - t * t;
                    if (env <= 0.04f) {
                        break;
                    }
                    env = MathF.Sqrt(env);
                    float mid = midBase + (HadalNoise.Fbm1(x * 0.004f, sPlain, 3) - 0.5f) * 70f;
                    float halfH = env * halfBase * (0.72f + 0.45f * HadalNoise.Fbm1(x * 0.006f, sPlain ^ 0x11UL, 3));
                    //顶板垂乳突:山脊噪声向下咬出悬垂岩幔
                    float droop = HadalNoise.Ridged2(x * 0.03f, mid * 0.01f, sPlain ^ 0x22UL, 3);
                    float ceil = mid - halfH + droop * droop * 55f;
                    float floor = mid + halfH * 0.92f
                        + (HadalNoise.Fbm1(x * 0.02f, sPlain ^ 0x33UL, 3) - 0.5f) * 14f;
                    if (floor - ceil < 4f) {
                        break; //闭合即收口
                    }
                    ceilY[x] = (int)ceil;
                    floorY[x] = (int)floor;
                    for (int y = (int)ceil; y <= (int)floor; y++) {
                        m.Carve(x, y);
                    }
                }
            }

            //门槛喉→平原顶接驳井:显式冲压贯通,不靠顶板噪声巧合搭接
            //(第二轮预览回归教训:顶板压低即断链,连接必须构造性成立)
            int ceilAtLink = -1;
            for (int probe = 0; probe < 200 && ceilAtLink < 0; probe++) {
                int px = linkX + (probe % 2 == 0 ? probe / 2 : -(probe / 2 + 1));
                if (px > x0 && px < x1 && floorY[px] >= 0) {
                    linkX = px;
                    ceilAtLink = ceilY[px];
                }
            }
            if (ceilAtLink > 0) {
                var top = new HadalPathNode(m.Plan.CenterX[m.Plan.TrenchCarveBottom - 20], m.Plan.TrenchCarveBottom - 20, 9f);
                var bot = new HadalPathNode(linkX, ceilAtLink + 24, 11f);
                var mid = new HadalPathNode((top.X + bot.X) * 0.5f, (top.Y + bot.Y) * 0.5f, 9f);
                CarveCapsule(m, top, mid, 3f, sPlain ^ 0xCCUL);
                CarveCapsule(m, mid, bot, 3f, sPlain ^ 0xCCUL);
            }

            //残柱:2D侧视里顶天立地的全柱就是一堵墙(第四轮预览根因),
            //故全柱形态改"腰断柱对":上残柱+下柱墩,腰部留12-20行可游间隙
            int pillarIdx = 0;
            foreach ((float px, float halfW, int mode) in plain.Pillars) {
                pillarIdx++;
                int ix = (int)px;
                if (ix <= x0 + 4 || ix >= x1 - 4 || floorY[ix] < 0) {
                    continue;
                }
                float top = ceilY[ix] - 6;
                float bottom = floorY[ix] + 6;
                float mid = (top + bottom) * 0.5f;
                float halfH = (bottom - top) * 0.5f;
                //腰断口位置与半高(哈希噪声,免耗随机流)
                float waistY = mid + (HadalNoise.Value1(pillarIdx * 5.3f, sPlain ^ 0x66UL) - 0.5f) * halfH * 0.6f;
                float gapHalf = 6f + HadalNoise.Value1(pillarIdx * 8.9f, sPlain ^ 0x67UL) * 4f;
                float yStart = top, yEnd = bottom;
                if (mode == 1) {
                    yEnd = top + (bottom - top) * 0.55f;
                }
                else if (mode == 2) {
                    yStart = bottom - (bottom - top) * 0.45f;
                }
                for (int y = (int)yStart; y <= (int)yEnd; y++) {
                    //腰断口:全柱形态在断口带跳过,永不封舱
                    if (mode == 0 && MathF.Abs(y - waistY) < gapHalf) {
                        continue;
                    }
                    float bulge = (y - mid) / halfH;
                    float hw = halfW * (0.7f + 0.5f * bulge * bulge)
                        * (0.85f + 0.3f * HadalNoise.Fbm2(px * 0.05f, y * 0.03f, sPlain ^ 0x44UL, 3));
                    //断口两侧与残柱端头收尖
                    if (mode == 0) {
                        float dGap = MathF.Abs(y - waistY) - gapHalf;
                        if (dGap < 8f) {
                            hw *= 0.45f + 0.55f * (dGap / 8f);
                        }
                    }
                    else if (mode == 1) {
                        float tt = (y - top) / MathF.Max(1f, yEnd - top);
                        hw *= MathF.Sqrt(MathF.Max(0f, 1.05f - tt));
                    }
                    else if (mode == 2) {
                        float tt = (yEnd - y) / MathF.Max(1f, yEnd - yStart);
                        hw *= MathF.Sqrt(MathF.Max(0f, 1.05f - tt));
                    }
                    for (int x = (int)(px - hw); x <= (int)(px + hw); x++) {
                        m.Fill(x, y, HadalNoise.Fbm2(x * 0.03f, y * 0.03f, sPlain ^ 0x55UL, 2) > 0.5f
                            ? HadalMat.Granite : HadalMat.Stone);
                    }
                }
            }

            //热液丘群:黑曜石锥丘+烟囱+灰烬毯(第一版纯地貌,蓝图§2.4)
            float shaftX = m.Plan.Shafts.Count > 0 ? m.Plan.Shafts[0].Nodes[0].X : -999f;
            int clusterIdx = 0;
            foreach (float vcx in plain.VentClusters) {
                clusterIdx++;
                if (MathF.Abs(vcx - shaftX) < 55f) {
                    continue; //让位主竖井口
                }
                //簇内锥数/偏移由哈希噪声派生,免消耗随机流
                int cones = 2 + (int)(HadalNoise.Value1(clusterIdx * 7.13f, sPlain ^ 0x66UL) * 3f);
                for (int k = 0; k < cones; k++) {
                    float off = (HadalNoise.Value1(clusterIdx * 13.7f + k * 3.31f, sPlain ^ 0x77UL) - 0.5f) * 56f;
                    int vx = (int)(vcx + off);
                    if (vx <= x0 + 3 || vx >= x1 - 3 || floorY[vx] < 0) {
                        continue;
                    }
                    int baseY = floorY[vx] + 1;
                    int hCone = 10 + (int)(HadalNoise.Value1(clusterIdx * 3.7f + k * 9.1f, sPlain ^ 0x88UL) * 15f);
                    int wCone = 8 + (int)(HadalNoise.Value1(clusterIdx * 5.9f + k * 4.7f, sPlain ^ 0x99UL) * 9f);
                    for (int dy = 0; dy <= hCone; dy++) {
                        float half = wCone * (1f - dy / (float)hCone);
                        for (int x = (int)(vx - half); x <= (int)(vx + half); x++) {
                            m.Fill(x, baseY - dy, half < wCone * 0.4f ? HadalMat.Obsidian : HadalMat.Ash);
                        }
                    }
                    //烟囱柱3宽,顶部略收
                    int chim = 8 + (int)(HadalNoise.Value1(clusterIdx * 11.3f + k * 2.9f, sPlain ^ 0xAAUL) * 9f);
                    for (int dy = 0; dy <= chim; dy++) {
                        m.Fill(vx - 1, baseY - hCone - dy, HadalMat.Obsidian);
                        m.Fill(vx, baseY - hCone - dy, HadalMat.Obsidian);
                        if (dy < chim - 3) {
                            m.Fill(vx + 1, baseY - hCone - dy, HadalMat.Obsidian);
                        }
                    }
                }
                //灰烬沉积毯
                for (int x = (int)vcx - 48; x <= (int)vcx + 48; x++) {
                    if (x <= x0 || x >= x1 || floorY[x] < 0) {
                        continue;
                    }
                    for (int dy = 1; dy <= 3; dy++) {
                        int y = floorY[x] + dy;
                        HadalMat cur = m.At(x, y);
                        if (cur == HadalMat.Stone || cur == HadalMat.Granite) {
                            m.Fill(x, y, HadalMat.Ash);
                        }
                    }
                }
            }
            return (floorY, ceilY);
        }

        //——深部冲压:竖井/下厅/厅间走廊/盆地——
        private static void StampDeepStructures(HadalTerrainModel m, ulong sStamp, int[] plainFloor) {
            foreach (HadalShaft shaft in m.Plan.Shafts) {
                //井口锚到平原实测地板:地板噪声起伏下依然构造性开口
                HadalPathNode mouth = shaft.Nodes[0];
                int mx = (int)mouth.X;
                if (mx >= 0 && mx < m.P.Width && plainFloor[mx] >= 0) {
                    var lip = new HadalPathNode(mouth.X, plainFloor[mx] - 6, mouth.R);
                    CarveCapsule(m, lip, mouth, 3f, sStamp);
                }
                for (int i = 0; i + 1 < shaft.Nodes.Count; i++) {
                    CarveCapsule(m, shaft.Nodes[i], shaft.Nodes[i + 1], 3.5f, sStamp);
                }
            }
            foreach (HadalHall hall in m.Plan.Halls) {
                CarveEllipse(m, hall.CX, hall.CY, hall.RX, hall.RY, 0.3f, sStamp, worley: false);
            }
            //厅间走廊按三元组(起-中-终)成段冲压
            List<HadalPathNode> cor = m.Plan.HallCorridor;
            for (int i = 0; i + 2 < cor.Count; i += 3) {
                CarveCapsule(m, cor[i], cor[i + 1], 3f, sStamp);
                CarveCapsule(m, cor[i + 1], cor[i + 2], 3f, sStamp);
            }
            //封闭盆地:满水死寂,登记白名单(蓝图§2.5)
            foreach (HadalBasin b in m.Plan.Basins) {
                CarveEllipse(m, b.CX, b.CY, b.RX, b.RY, 0.22f, sStamp, worley: false);
            }
            //沟底终腔:V底走廊尽头豁然一个横腔(鲸落场落位)
            (float bx, float by, float brx, float bry) = m.Plan.VEndBulb;
            CarveEllipse(m, bx, by, brx, bry, 0.28f, sStamp, worley: true);
        }

        //——支沟折线+溶洞腔室链(蓝图§2.3)——
        private static void StampGalleriesAndCaves(HadalTerrainModel m, ulong sStamp) {
            foreach (HadalGallery g in m.Plan.Galleries) {
                for (int i = 0; i + 1 < g.Nodes.Count; i++) {
                    CarveCapsule(m, g.Nodes[i], g.Nodes[i + 1], 3.5f, sStamp);
                }
                if (!g.LoopBack && !g.HasCaveField) {
                    //死端鼓包
                    HadalPathNode end = g.Nodes[^1];
                    CarveEllipse(m, end.X, end.Y, end.R * 1.9f, end.R * 1.4f, 0.3f, sStamp, worley: true);
                }
            }
            foreach (HadalCaveField f in m.Plan.CaveFields) {
                for (int i = 0; i < f.Chambers.Count; i++) {
                    (float cx, float cy, float rx, float ry) = f.Chambers[i];
                    CarveEllipse(m, cx, cy, rx, ry, 0.42f, sStamp, worley: true);
                }
                foreach ((int a, int b, float r) in f.Links) {
                    var na = new HadalPathNode(f.Chambers[a].x, f.Chambers[a].y, r);
                    var nb = new HadalPathNode(f.Chambers[b].x, f.Chambers[b].y, r);
                    CarveCapsule(m, na, nb, 2.5f, sStamp);
                }
                //支沟尾端→入口腔的进洞喉道
                var entry = new HadalPathNode(f.EntryFrom.x, f.EntryFrom.y, 4.5f);
                var target = new HadalPathNode(f.Chambers[f.EntryChamber].x, f.Chambers[f.EntryChamber].y, 4f);
                CarveCapsule(m, entry, target, 2.5f, sStamp);
            }
        }

        //——出生气穴房:壳→内膛→月池→廊道(蓝图§2.6),晚于一切大开凿故壳体完整——
        private static void BuildSpawnRoom(HadalTerrainModel m) {
            HadalSpawnRoomSpec r = m.Plan.SpawnRoom;
            //砂岩壳:内膛外扩4,底下再厚垫3行防悬空
            for (int x = r.Left - 4; x <= r.Right + 4; x++) {
                for (int y = r.TopY - 4; y <= r.BotY + 4; y++) {
                    m.Fill(x, y, HadalMat.RoomShell);
                }
            }
            //内膛=登记气穴
            for (int x = r.Left; x <= r.Right; x++) {
                for (int y = r.TopY; y <= r.BotY; y++) {
                    m.Carve(x, y);
                }
            }
            m.SetAirRect(r.Left, r.TopY, r.Right, r.BotY);
            //月池竖井3宽:室内地板凿穿向下(水面锁在池口)
            for (int x = r.PoolLeft; x <= r.PoolLeft + 2; x++) {
                for (int y = r.BotY + 1; y <= r.PoolBottomY; y++) {
                    m.Carve(x, y);
                }
            }
            //水平廊道3高:池底折向主沟,打进内膛富余6格
            int corrTop = r.PoolBottomY - 2;
            int xFrom = r.Dir > 0 ? r.CorridorTargetX : r.PoolLeft + 2;
            int xTo = r.Dir > 0 ? r.PoolLeft : r.CorridorTargetX;
            if (xFrom > xTo) {
                (xFrom, xTo) = (xTo, xFrom);
            }
            for (int x = xFrom; x <= xTo; x++) {
                for (int y = corrTop; y <= r.PoolBottomY; y++) {
                    m.Carve(x, y);
                }
            }
            m.SpawnX = r.SpawnX;
            m.SpawnY = r.SpawnY;
        }

        //——微光蘑菇斑:开凿后找地板落泥+蘑菇泥表层(蓝图§4微光行)——
        private static void ApplyBioSpots(HadalTerrainModel m, ulong sStamp) {
            int spotIdx = 0;
            foreach ((float sx, float sy) in m.Plan.BioSpots) {
                spotIdx++;
                int x = (int)sx;
                int y = (int)sy - 3;
                //起点若埋在实心里先上浮出腔,再下探找地板
                int guard = 0;
                while (m.At(x, y) != HadalMat.None && guard++ < 60) {
                    y--;
                }
                if (guard >= 60) {
                    continue;
                }
                int floor = -1;
                for (int dy = 0; dy < 90; dy++) {
                    if (m.At(x, y + dy) != HadalMat.None && m.At(x, y + dy - 1) == HadalMat.None) {
                        floor = y + dy;
                        break;
                    }
                }
                if (floor < 0 || floor > 4755 || floor < 200) {
                    continue;
                }
                float rx = 5f + HadalNoise.Value1(spotIdx * 3.7f, sStamp ^ 0xB0UL) * 6f;
                const float ry = 3.2f;
                //斑体填泥
                for (int dx = (int)-rx; dx <= (int)rx; dx++) {
                    for (int dy = -3; dy <= 4; dy++) {
                        float n = dx * dx / (rx * rx) + dy * dy / (ry * ry);
                        if (n <= 1f && m.At(x + dx, floor + dy) != HadalMat.None) {
                            m.Fill(x + dx, floor + dy, HadalMat.Mud);
                        }
                    }
                }
                //暴露面转蘑菇泥(游戏侧映射蘑菇草)
                for (int dx = (int)-rx; dx <= (int)rx; dx++) {
                    for (int dy = -3; dy <= 4; dy++) {
                        int tx = x + dx, ty = floor + dy;
                        if (m.At(tx, ty) != HadalMat.Mud) {
                            continue;
                        }
                        if (m.At(tx - 1, ty) == HadalMat.None || m.At(tx + 1, ty) == HadalMat.None
                            || m.At(tx, ty - 1) == HadalMat.None || m.At(tx, ty + 1) == HadalMat.None) {
                            m.Fill(tx, ty, HadalMat.MushroomMud);
                        }
                    }
                }
            }
        }

        //顶部封板:钳制线上方不可见,防实体逃逸
        private static void SealTop(HadalTerrainModel m) {
            for (int x = 0; x < m.P.Width; x++) {
                for (int y = 0; y < 6; y++) {
                    m.Fill(x, y, HadalMat.Stone);
                }
            }
        }

        //——冲压原语——
        private static void CarveCapsule(HadalTerrainModel m, HadalPathNode a, HadalPathNode b, float wobAmp, ulong seed) {
            float minX = MathF.Min(a.X, b.X) - MathF.Max(a.R, b.R) - wobAmp - 2f;
            float maxX = MathF.Max(a.X, b.X) + MathF.Max(a.R, b.R) + wobAmp + 2f;
            float minY = MathF.Min(a.Y, b.Y) - MathF.Max(a.R, b.R) - wobAmp - 2f;
            float maxY = MathF.Max(a.Y, b.Y) + MathF.Max(a.R, b.R) + wobAmp + 2f;
            float abx = b.X - a.X, aby = b.Y - a.Y;
            float lenSq = abx * abx + aby * aby;
            for (int y = (int)minY; y <= (int)maxY; y++) {
                for (int x = (int)minX; x <= (int)maxX; x++) {
                    float t = lenSq <= 0.001f ? 0f
                        : Math.Clamp(((x - a.X) * abx + (y - a.Y) * aby) / lenSq, 0f, 1f);
                    float px = a.X + abx * t, py = a.Y + aby * t;
                    float dx = x - px, dy = y - py;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float r = a.R + (b.R - a.R) * t;
                    float wob = (HadalNoise.Fbm2(x * 0.06f, y * 0.06f, seed, 3) - 0.5f) * 2f * wobAmp;
                    if (d < r + wob) {
                        m.Carve(x, y);
                    }
                }
            }
        }

        private static void CarveEllipse(HadalTerrainModel m, float cx, float cy, float rx, float ry,
            float edgeNoise, ulong seed, bool worley) {
            float pad = MathF.Max(rx, ry) * edgeNoise + 2f;
            for (int y = (int)(cy - ry - pad); y <= (int)(cy + ry + pad); y++) {
                for (int x = (int)(cx - rx - pad); x <= (int)(cx + rx + pad); x++) {
                    float nx = (x - cx) / rx, nyy = (y - cy) / ry;
                    float nrm = MathF.Sqrt(nx * nx + nyy * nyy);
                    float edge = 1f + (HadalNoise.Fbm2(x * 0.05f, y * 0.05f, seed ^ 0xE1UL, 3) - 0.5f) * 2f * edgeNoise;
                    if (worley) {
                        //Worley腔壁纹理:胸腔式的鼓包起伏(蓝图§3.4)
                        edge += (0.5f - HadalNoise.WorleyF1(x, y, seed ^ 0xE2UL, 9f)) * 0.3f;
                    }
                    if (nrm < edge) {
                        m.Carve(x, y);
                    }
                }
            }
        }
    }
}
