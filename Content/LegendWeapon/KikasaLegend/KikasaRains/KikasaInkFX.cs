using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>命中面:渍斑贴在哪一种表面上,决定摊开姿态与滴淌行为</summary>
    internal enum SplatSurface : byte
    {
        /// <summary>地面:摊成扁宽墨泊,几乎不滴只洇</summary>
        Floor,
        /// <summary>左侧墙(墙体在左):窄长竖渍,顺墙流</summary>
        WallLeft,
        /// <summary>右侧墙(墙体在右):窄长竖渍,顺墙流</summary>
        WallRight,
        /// <summary>天花板:垂挂,滴得最勤、拖得最长</summary>
        Ceiling
    }

    /// <summary>
    /// 墨渍贴花管理器:命中的余韵层,渍斑比墨滴活得久。
    /// 地面渍按命中面(地/墙/顶)吸附到 tile 表面并换贴面姿态,
    /// NPC 渍挂宿主随行(宿主消亡快淡),湖面墨晕沿水线晕开、稀释、随水漂。
    /// 环形上限防堆积,纯客户端表现,由 <see cref="KikasaRainSystem"/> 驱动、
    /// <see cref="KikasaRainRender"/> 在墨滴之下绘制地面/NPC 渍,
    /// 湖晕由 <see cref="KikasaDomainRender"/> 在 TechUnify 之后叠到做好的水面上
    /// </summary>
    internal static class KikasaInkFX
    {
        private const int GroundCap = 48;
        private const int NpcCap = 24;
        private const int LakeCap = 12;

        /// <summary>晕染扩张帧数:缘先扩后定</summary>
        private const int BloomFrames = 22;

        //画布合同:着色器 guard 从 0.88 起切,内容须先归零。只放大 C# 正方形不够——UV 仍满幅,左右照切。
        //下列上界对齐 KikasaInkSplat.fx(碎斑 R*1.8+噪声、滴淌 0.22+colN*0.5、湖晕指状 R*1.6+缘噪声)
        private const float CanvasBudget = 0.82f;
        private const float SplatQaExt = 1.52f;
        private const float SplatCenterY = 0.18f;
        private const float SplatDripN = 0.72f;

        /// <summary>湖面墨晕晕开帧数:水里散得慢</summary>
        private const int LakeBloomFrames = 46;

        private const int GroundLife = 220;
        private const int NpcLife = 150;

        //==================== 地形剖面 ====================
        //表面渍出生时沿贴面切向逐列取样地形,着色器按剖面逐列位移渍体:
        //墨随台阶下沉、贴斜坡、翻上墙角,悬空列淡出——不再是一张悬空的完整椭圆

        /// <summary>剖面取样列数,与 KikasaInkSplat.fx 的 uProf 长度一致</summary>
        private const int ProfN = 24;
        /// <summary>悬空哨兵(世界像素):落进着色器 44~64 淡出带之外,整列淡掉</summary>
        private const float ProfChasm = 72f;
        /// <summary>实心横贯列的位移钉点:墨沿墙角最多上翻这么高,不淡出</summary>
        private const float ProfClimbCap = 40f;
        /// <summary>剖面位移夹持上限,与着色器 clamp(±56) 同值</summary>
        private const float ProfShiftCap = 56f;

        /// <summary>NPC 渍共用的零剖面,不逐渍分配</summary>
        private static readonly float[] ZeroProfile = new float[ProfN];

        private class InkSplat
        {
            public Vector2 Pos;
            /// <summary>各向异性主轴(单位向量)</summary>
            public Vector2 Dir;
            public float Aniso;
            /// <summary>垂轴压扁:贴面姿态</summary>
            public float Squish = 1f;
            /// <summary>滴淌长度系数:随命中面</summary>
            public float RunScale = 1f;
            public SplatSurface Surface;
            public float Size;
            public float Seed;
            public int Age;
            public int Life;
            //NPC 附着字段,NpcWho=-1 即表面渍
            public int NpcWho = -1;
            public int NpcType;
            public Vector2 Offset;
            /// <summary>宿主消亡后的快淡</summary>
            public float DeadFade = 1f;
            public bool Done;

            //地形剖面(出生烘焙;NPC 渍保持零剖面=不扭不淡)
            public float[] Profile = ZeroProfile;
            /// <summary>剖面位移轴(=uProfN):地/顶 (0,1),墙 (1,0),NPC (0,0)</summary>
            public Vector2 ProfAxis;
            /// <summary>悬空淡出方向符号(=uEdgeSign)</summary>
            public float EdgeSign;
            /// <summary>取样 0 中心的切向世界坐标,滴淌取值用</summary>
            public float ProfT0;
            public float ProfQScale;
            public float ProfQOff;
            /// <summary>世界像素→q 单位(=1/(Size*1.2))</summary>
            public float InvWorldPerQ;
            /// <summary>夹持后的剖面位移范围(世界像素),画布预算用</summary>
            public float WarpLo;
            public float WarpHi;
        }

        private class LakeBlot
        {
            public int OwnerWho;
            public float X;
            public float DriftV;
            public float Size;
            public float Seed;
            public int Age;
            public int Life;
            /// <summary>域收起后的快淡</summary>
            public float Fade = 1f;
        }

        private static readonly List<InkSplat> ground = [];
        private static readonly List<InkSplat> attached = [];
        private static readonly List<LakeBlot> lake = [];

        //==================== 命中面解析 ====================

        private static bool SolidTile(int x, int y) {
            Tile t = Framing.GetTileSafely(x, y);
            return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
        }

        /// <summary>按入射姿态排出候选面依次探测,命中即把锚点吸附到该 tile 表面</summary>
        private static SplatSurface ResolveSurface(ref Vector2 pos, Vector2 vel) {
            Span<SplatSurface> order = stackalloc SplatSurface[4];
            SplatSurface sideFirst = vel.X >= 0f ? SplatSurface.WallRight : SplatSurface.WallLeft;
            SplatSurface sideLast = vel.X >= 0f ? SplatSurface.WallLeft : SplatSurface.WallRight;
            if (MathF.Abs(vel.Y) >= MathF.Abs(vel.X)) {
                if (vel.Y >= 0f) {
                    order[0] = SplatSurface.Floor;
                    order[1] = sideFirst;
                    order[2] = sideLast;
                    order[3] = SplatSurface.Ceiling;
                }
                else {
                    order[0] = SplatSurface.Ceiling;
                    order[1] = sideFirst;
                    order[2] = sideLast;
                    order[3] = SplatSurface.Floor;
                }
            }
            else {
                order[0] = sideFirst;
                order[1] = SplatSurface.Floor;
                order[2] = SplatSurface.Ceiling;
                order[3] = sideLast;
            }
            for (int i = 0; i < 4; i++) {
                if (TrySnap(order[i], ref pos)) {
                    return order[i];
                }
            }
            //探不到面(理论上贴地命中才会入账):按地面处理
            return SplatSurface.Floor;
        }

        private static bool TrySnap(SplatSurface surf, ref Vector2 pos) {
            int tx;
            int ty;
            switch (surf) {
                case SplatSurface.Floor:
                    tx = (int)(pos.X / 16f);
                    ty = (int)((pos.Y + 10f) / 16f);
                    if (!SolidTile(tx, ty)) {
                        return false;
                    }
                    pos.Y = ty * 16f - 1f;
                    return true;
                case SplatSurface.Ceiling:
                    tx = (int)(pos.X / 16f);
                    ty = (int)((pos.Y - 10f) / 16f);
                    if (!SolidTile(tx, ty)) {
                        return false;
                    }
                    pos.Y = ty * 16f + 17f;
                    return true;
                case SplatSurface.WallRight:
                    tx = (int)((pos.X + 10f) / 16f);
                    ty = (int)(pos.Y / 16f);
                    if (!SolidTile(tx, ty)) {
                        return false;
                    }
                    pos.X = tx * 16f - 1f;
                    return true;
                default: //WallLeft
                    tx = (int)((pos.X - 10f) / 16f);
                    ty = (int)(pos.Y / 16f);
                    if (!SolidTile(tx, ty)) {
                        return false;
                    }
                    pos.X = tx * 16f + 17f;
                    return true;
            }
        }

        //==================== 地形剖面烘焙 ====================

        /// <summary>滴淌贴剖面的悬空门:该列位移×EdgeSign 超过它即临近淡出带,不再落滴</summary>
        private const float DripVoidGate = 48f;

        /// <summary>斜坡感知的点实心:半砖/四种斜坡按格内几何裁剪,剖面才贴得住坡面</summary>
        private static bool PointSolid(float x, float y) {
            int tx = (int)(x / 16f);
            int ty = (int)(y / 16f);
            Tile t = Framing.GetTileSafely(tx, ty);
            if (!t.HasTile || !Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]) {
                return false;
            }
            float fx = x - tx * 16f;
            float fy = y - ty * 16f;
            if (t.IsHalfBlock) {
                return fy >= 8f;
            }
            return t.Slope switch {
                SlopeType.SlopeDownLeft => fy >= 16f - fx,
                SlopeType.SlopeDownRight => fy >= fx,
                SlopeType.SlopeUpLeft => fy <= fx,
                SlopeType.SlopeUpRight => fy <= 16f - fx,
                _ => true,
            };
        }

        /// <summary>
        /// 单列表面扫描:自翻角钉点(空气侧 ProfClimbCap 处)沿入固方向 2px 步进找首个实心,
        /// 返回沿剖面位移轴的带符号偏移(×EdgeSign 统一四面代数);
        /// 起点即实心=整列被墙横贯,位移钉在翻角上限不淡出;扫穿无实心=悬空哨兵
        /// </summary>
        private static float ScanColumn(Vector2 colBase, Vector2 scanDir, float edgeSign) {
            Vector2 start = colBase - scanDir * ProfClimbCap;
            if (PointSolid(start.X, start.Y)) {
                return -ProfClimbCap * edgeSign;
            }
            float range = ProfClimbCap + ProfChasm;
            for (float u = 2f; u <= range; u += 2f) {
                Vector2 w = start + scanDir * u;
                if (PointSolid(w.X, w.Y)) {
                    return (u - 1f - ProfClimbCap) * edgeSign;
                }
            }
            return ProfChasm * edgeSign;
        }

        /// <summary>
        /// 出生烘焙地形剖面:沿贴面切向每 16px 一列共 24 列,逐列扫表面写入相对锚点的
        /// 位移(世界像素)。下标换算与 KikasaInkSplat.fx 的 st=dot(q,uDir)*uProfQScale+uProfQOff
        /// 严格同构(墙面切向含 0.18 印面上移折算);顺手记录夹持位移范围供画布让位
        /// </summary>
        private static void BakeProfile(InkSplat s) {
            float halfSide = s.Size * 1.2f;
            s.Profile = new float[ProfN];
            s.ProfQScale = halfSide / 16f;
            s.InvWorldPerQ = 1f / halfSide;
            s.ProfQOff = (ProfN - 1) * 0.5f;

            Vector2 tan;
            Vector2 scanDir;
            switch (s.Surface) {
                case SplatSurface.Floor:
                    tan = new Vector2(1f, 0f);
                    scanDir = new Vector2(0f, 1f);
                    s.ProfAxis = new Vector2(0f, 1f);
                    s.EdgeSign = 1f;
                    break;
                case SplatSurface.Ceiling:
                    tan = new Vector2(1f, 0f);
                    scanDir = new Vector2(0f, -1f);
                    s.ProfAxis = new Vector2(0f, 1f);
                    s.EdgeSign = -1f;
                    break;
                case SplatSurface.WallLeft:
                    tan = new Vector2(0f, 1f);
                    scanDir = new Vector2(-1f, 0f);
                    s.ProfAxis = new Vector2(1f, 0f);
                    s.EdgeSign = -1f;
                    s.ProfQOff -= SplatCenterY * s.ProfQScale;
                    break;
                default: //WallRight
                    tan = new Vector2(0f, 1f);
                    scanDir = new Vector2(1f, 0f);
                    s.ProfAxis = new Vector2(1f, 0f);
                    s.EdgeSign = 1f;
                    s.ProfQOff -= SplatCenterY * s.ProfQScale;
                    break;
            }
            float anchorT = tan.X != 0f ? s.Pos.X : s.Pos.Y;
            s.ProfT0 = anchorT - (ProfN - 1) * 0.5f * 16f;

            float lo = 0f;
            float hi = 0f;
            for (int i = 0; i < ProfN; i++) {
                Vector2 colBase = s.Pos + tan * ((i - (ProfN - 1) * 0.5f) * 16f);
                float prof = ScanColumn(colBase, scanDir, s.EdgeSign);
                s.Profile[i] = prof;
                float shift = MathHelper.Clamp(prof, -ProfShiftCap, ProfShiftCap);
                lo = MathF.Min(lo, shift);
                hi = MathF.Max(hi, shift);
            }
            s.WarpLo = lo;
            s.WarpHi = hi;
        }

        /// <summary>CPU 侧剖面取样:与着色器同构的相邻列线性插值,滴淌粒子贴位移后的表面</summary>
        private static float SampleProfile(InkSplat s, float worldT) {
            if (s.ProfQScale <= 0f) {
                return 0f;
            }
            float idx = MathHelper.Clamp((worldT - s.ProfT0) / 16f, 0f, ProfN - 1);
            int i0 = (int)idx;
            int i1 = Math.Min(i0 + 1, ProfN - 1);
            float prof = MathHelper.Lerp(s.Profile[i0], s.Profile[i1], idx - i0);
            return MathHelper.Clamp(prof, -ProfShiftCap, ProfShiftCap);
        }

        //==================== 入账 ====================

        /// <summary>表面渍:解析命中面、吸附锚点、按面换贴面姿态</summary>
        public static void AddGroundSplat(Vector2 pos, Vector2 impactVel, float size) {
            if (Main.dedServ) {
                return;
            }
            if (ground.Count >= GroundCap) {
                ground.RemoveAt(0);
            }
            Vector2 snapped = pos;
            SplatSurface surf = ResolveSurface(ref snapped, impactVel);

            InkSplat s = new() {
                Pos = snapped,
                Surface = surf,
                Size = size,
                Seed = Main.rand.NextFloat(8f),
                Life = GroundLife + Main.rand.Next(-30, 40),
            };
            switch (surf) {
                case SplatSurface.Floor:
                    s.Dir = new Vector2(1f, 0f);
                    s.Aniso = Main.rand.NextFloat(1.5f, 1.9f);
                    s.Squish = 0.62f;
                    s.RunScale = 0.3f;
                    break;
                case SplatSurface.Ceiling:
                    s.Dir = new Vector2(1f, 0f);
                    s.Aniso = Main.rand.NextFloat(1.2f, 1.45f);
                    s.Squish = 0.75f;
                    s.RunScale = 1.6f;
                    break;
                default: //两侧墙
                    s.Dir = new Vector2(0f, 1f);
                    s.Aniso = Main.rand.NextFloat(1.3f, 1.65f);
                    s.Squish = 0.62f;
                    s.RunScale = 1.25f;
                    break;
            }
            //出生即烘焙:渍是死墨,地形剖面只取这一次
            BakeProfile(s);
            ground.Add(s);
        }

        /// <summary>NPC 渍:挂宿主局部偏移随行</summary>
        public static void AddNpcSplat(NPC npc, Vector2 hitPos, Vector2 impactVel, float size) {
            if (Main.dedServ || npc == null) {
                return;
            }
            if (attached.Count >= NpcCap) {
                attached.RemoveAt(0);
            }
            Vector2 offset = hitPos - npc.Center;
            //钳进身体范围,渍要贴在身上不悬在身边
            offset.X = MathHelper.Clamp(offset.X, -npc.width * 0.4f, npc.width * 0.4f);
            offset.Y = MathHelper.Clamp(offset.Y, -npc.height * 0.4f, npc.height * 0.4f);
            Vector2 dir = impactVel.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            attached.Add(new InkSplat {
                NpcWho = npc.whoAmI,
                NpcType = npc.type,
                Offset = offset,
                Pos = npc.Center + offset,
                Dir = dir,
                Aniso = Main.rand.NextFloat(1.15f, 1.5f),
                Squish = 1f,
                RunScale = 0.8f,
                Size = MathF.Min(size, MathF.Max(npc.width, 34f)),
                Seed = Main.rand.NextFloat(8f),
                Life = NpcLife + Main.rand.Next(-20, 30),
            });
        }

        /// <summary>湖面墨晕:同主近位合并生长(墨瀑持续冲刷不是刷屏),否则新起一片</summary>
        public static void AddLakeBlot(int ownerWho, float x, float size) {
            if (Main.dedServ) {
                return;
            }
            foreach (LakeBlot b in lake) {
                if (b.OwnerWho == ownerWho && MathF.Abs(b.X - x) < b.Size * 0.8f) {
                    b.Size = MathF.Min(b.Size + size * 0.3f, 150f);
                    //回春:新墨续进来,稀释从头再走一段
                    b.Age = Math.Min(b.Age, b.Life / 3);
                    return;
                }
            }
            if (lake.Count >= LakeCap) {
                lake.RemoveAt(0);
            }
            lake.Add(new LakeBlot {
                OwnerWho = ownerWho,
                X = x,
                DriftV = Main.rand.NextFloat(-0.12f, 0.12f),
                Size = size,
                Seed = Main.rand.NextFloat(8f),
                Life = 260 + Main.rand.Next(-40, 60),
            });
        }

        //==================== 推进 ====================

        public static void Update() {
            for (int i = ground.Count - 1; i >= 0; i--) {
                InkSplat s = ground[i];
                s.Age++;
                UpdateSurfaceDrip(s);
                if (s.Age >= s.Life) {
                    ground.RemoveAt(i);
                }
            }
            for (int i = attached.Count - 1; i >= 0; i--) {
                InkSplat s = attached[i];
                s.Age++;
                NPC npc = s.NpcWho >= 0 && s.NpcWho < Main.maxNPCs ? Main.npc[s.NpcWho] : null;
                if (npc?.active == true && npc.type == s.NpcType) {
                    s.Pos = npc.Center + s.Offset;
                }
                else {
                    //宿主没了:渍钉在最后位置快淡
                    s.DeadFade -= 0.09f;
                }
                if (s.Age >= s.Life || s.DeadFade <= 0f) {
                    attached.RemoveAt(i);
                }
            }
            for (int i = lake.Count - 1; i >= 0; i--) {
                LakeBlot b = lake[i];
                b.Age++;
                //墨膜随水面缓慢漂移
                b.X += b.DriftV;
                b.DriftV *= 0.996f;
                if (!TryGetLakeY(b.OwnerWho, out _)) {
                    b.Fade -= 0.06f;
                }
                if (b.Age >= b.Life || b.Fade <= 0f) {
                    lake.RemoveAt(i);
                }
            }
        }

        /// <summary>逐面滴淌:地渍只洇、墙渍贴墙下滑、顶渍垂滴脱离坠落;渍越新滴得越勤。
        /// 滴点经 SampleProfile 贴到位移后的表面,临近悬空淡出带的列不落滴</summary>
        private static void UpdateSurfaceDrip(InkSplat s) {
            if (s.Age >= s.Life * 0.6f) {
                return;
            }
            switch (s.Surface) {
                case SplatSurface.Floor:
                    if (Main.rand.NextBool(80)) {
                        float dx = (KikasaInk.Hash((int)(s.Seed * 977f), s.Age) - 0.5f) * s.Size * 0.5f;
                        float sag = SampleProfile(s, s.Pos.X + dx);
                        if (sag * s.EdgeSign < DripVoidGate) {
                            PRTLoader.NewParticle<PRT_KikasaInkDrip>(s.Pos + new Vector2(dx, sag - 2f),
                                new Vector2(0f, 0.2f), KikasaInk.InkBody,
                                Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(10, 16));
                        }
                    }
                    break;
                case SplatSurface.Ceiling:
                    if (Main.rand.NextBool(14)) {
                        float dx = (KikasaInk.Hash((int)(s.Seed * 977f), s.Age) - 0.5f) * s.Size * 0.6f;
                        float sag = SampleProfile(s, s.Pos.X + dx);
                        if (sag * s.EdgeSign < DripVoidGate) {
                            PRTLoader.NewParticle<PRT_KikasaInkDrip>(s.Pos + new Vector2(dx, sag + 4f),
                                new Vector2(0f, Main.rand.NextFloat(0.3f, 0.7f)), KikasaInk.InkBody,
                                Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(30, 44));
                        }
                    }
                    break;
                default: //两侧墙:贴着墙面下滑
                    if (Main.rand.NextBool(24)) {
                        float hug = s.Surface == SplatSurface.WallLeft ? 2f : -2f;
                        float dy = KikasaInk.Hash((int)(s.Seed * 977f), s.Age) * s.Size * 0.35f;
                        float shift = SampleProfile(s, s.Pos.Y + dy);
                        if (shift * s.EdgeSign < DripVoidGate) {
                            PRTLoader.NewParticle<PRT_KikasaInkDrip>(s.Pos + new Vector2(hug + shift, dy),
                                new Vector2(0f, Main.rand.NextFloat(0.4f, 0.9f)), KikasaInk.InkBody,
                                Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(20, 32));
                        }
                    }
                    break;
            }
        }

        /// <summary>与墨滴入水门槛 RiseT&gt;0.5 对齐,避免登记了却画不出、几帧被淡掉。</summary>
        private static bool TryGetLakeY(int ownerWho, out float lakeY) {
            lakeY = 0f;
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers) {
                return false;
            }
            Player pl = Main.player[ownerWho];
            if (pl?.active != true || !pl.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT < 0.5f) {
                return false;
            }
            lakeY = domain.LakeWorldY;
            return true;
        }

        public static void Clear() {
            ground.Clear();
            attached.Clear();
            lake.Clear();
        }

        //==================== 绘制 ====================

        /// <summary>地面/NPC 渍:EndEntityDraw,域内会被血湖镜面倒影。</summary>
        public static void Draw(SpriteBatch sb) {
            if (ground.Count == 0 && attached.Count == 0) {
                return;
            }
            Effect fx = EffectLoader.KikasaInkSplat?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);

            if (fx != null && canvas != null && noise != null) {
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    fx, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                fx.Parameters["uColBody"]?.SetValue(KikasaInk.InkBody.ToVector3());
                fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
                fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
                fx.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());

                fx.CurrentTechnique = fx.Techniques["TechSplat"];
                DrawListShader(sb, fx, canvas, ground, view);
                DrawListShader(sb, fx, canvas, attached, view);
                sb.End();
                return;
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            DrawListFallback(sb, ground, view);
            DrawListFallback(sb, attached, view);
            sb.End();
        }

        /// <summary>
        /// 湖面墨膜:必须叠在 TechUnify 之后。EndCapture 已是屏幕空间,
        /// 位置走矩阵变换、尺寸乘 Zoom,禁止再套 GameViewMatrix 批次。
        /// </summary>
        public static void DrawLakeOnWater(SpriteBatch sb) {
            if (lake.Count == 0) {
                return;
            }
            Effect fx = EffectLoader.KikasaInkSplat?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            if (fx != null && canvas != null && noise != null) {
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    fx);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                fx.Parameters["uColBody"]?.SetValue(KikasaInk.InkBody.ToVector3());
                fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
                fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
                fx.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());
                fx.CurrentTechnique = fx.Techniques["TechLakeBlot"];
                DrawLakeShader(sb, fx, canvas);
                sb.End();
                return;
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            DrawLakeFallback(sb);
            sb.End();
        }

        /// <summary>
        /// 渍斑逻辑坐标倍率:qa 空间半径经主轴/垂轴还原到 q,再计入中心上移与滴淌。
        /// 与 uCanvasFit、quad 缩放必须同一返回值,世界尺寸才不变。
        /// </summary>
        private static Vector2 SplatCanvasFit(InkSplat s, float bloom, float run) {
            float R = 0.30f + 0.34f * bloom;
            float along = SplatQaExt * MathF.Max(s.Aniso, 1f);
            float perp = SplatQaExt * MathF.Max(s.Squish, 0.2f);
            float drip = R * 0.45f * s.Squish + run * s.RunScale * SplatDripN;
            float ax = MathF.Abs(s.Dir.X);
            float ay = MathF.Abs(s.Dir.Y);
            float needX = ax * along + ay * perp;
            float needY = ay * along + ax * perp;
            float needYDown = MathF.Max(needY, drip);
            //地形剖面位移把内容沿 ProfAxis 整体搬走(夹持后范围 WarpLo~WarpHi),画布同步让位
            float warpNegQ = MathF.Max(0f, -s.WarpLo) * s.InvWorldPerQ;
            float warpPosQ = MathF.Max(0f, s.WarpHi) * s.InvWorldPerQ;
            if (s.ProfAxis.Y != 0f) {
                needY += warpNegQ;
                needYDown += warpPosQ;
            }
            else if (s.ProfAxis.X != 0f) {
                needX += MathF.Max(warpNegQ, warpPosQ);
            }
            float fx = MathF.Max(1f, needX / CanvasBudget);
            float fy = MathF.Max(1f, MathF.Abs(needYDown - SplatCenterY) / CanvasBudget);
            fy = MathF.Max(fy, (needY + SplatCenterY) / CanvasBudget);
            return new Vector2(fx, fy);
        }

        /// <summary>湖晕只在横向撑开,竖向原本装得下。</summary>
        private static Vector2 LakeCanvasFit(float bloom) {
            float R = 0.16f + 0.72f * bloom;
            float needX = R * 1.6f + 0.20f;
            return new Vector2(MathF.Max(1f, needX / CanvasBudget), 1f);
        }

        private static Vector2 WorldToScreen(Vector2 world)
            => Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);

        private static bool OnScreen(Vector2 screenPos) {
            return screenPos.X >= -220f && screenPos.Y >= -220f
                && screenPos.X <= Main.screenWidth + 220f && screenPos.Y <= Main.screenHeight + 220f;
        }

        private static void DrawLakeShader(SpriteBatch sb, Effect fx, Texture2D canvas) {
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            foreach (LakeBlot b in lake) {
                if (!TryGetLakeY(b.OwnerWho, out float lakeY)) {
                    continue;
                }
                Vector2 pos = WorldToScreen(new Vector2(b.X, lakeY));
                if (!OnScreen(pos)) {
                    continue;
                }
                float bloom = MathHelper.Clamp(b.Age / (float)LakeBloomFrames, 0f, 1f);
                float dilute = MathHelper.Clamp((b.Age - 40f) / (b.Life - 60f), 0f, 1f);
                float fade = (1f - MathHelper.Clamp((b.Age - (b.Life - 46f)) / 46f, 0f, 1f)) * b.Fade;
                if (fade <= 0.01f) {
                    continue;
                }
                Vector2 fit = LakeCanvasFit(bloom);
                fx.Parameters["uSeed"]?.SetValue(b.Seed);
                fx.Parameters["uBloom"]?.SetValue(bloom);
                fx.Parameters["uDry"]?.SetValue(dilute);
                fx.Parameters["uFade"]?.SetValue(fade);
                fx.Parameters["uCanvasFit"]?.SetValue(fit);
                fx.CurrentTechnique.Passes[0].Apply();

                float w = b.Size * 2.6f * fit.X * zoom.X;
                float h = b.Size * 1.1f * fit.Y * zoom.Y;
                sb.Draw(canvas, pos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, new Vector2(w / canvas.Width, h / canvas.Height),
                    SpriteEffects.None, 0f);
            }
        }

        private static void DrawListShader(SpriteBatch sb, Effect fx, Texture2D canvas,
            List<InkSplat> list, Rectangle view) {
            foreach (InkSplat s in list) {
                if (!view.Contains(s.Pos.ToPoint())) {
                    continue;
                }
                float bloom = MathHelper.Clamp(s.Age / (float)BloomFrames, 0f, 1f);
                float dry = MathHelper.Clamp((s.Age - 50f) / (s.Life * 0.62f), 0f, 1f);
                float run = MathHelper.Clamp(s.Age / 110f, 0f, 1f);
                float fade = (1f - MathHelper.Clamp((s.Age - (s.Life - 36f)) / 36f, 0f, 1f)) * s.DeadFade;
                if (fade <= 0.01f) {
                    continue;
                }
                Vector2 fit = SplatCanvasFit(s, bloom, run);
                fx.Parameters["uSeed"]?.SetValue(s.Seed);
                fx.Parameters["uBloom"]?.SetValue(bloom);
                fx.Parameters["uDry"]?.SetValue(dry);
                fx.Parameters["uRun"]?.SetValue(run);
                fx.Parameters["uFade"]?.SetValue(fade);
                fx.Parameters["uAniso"]?.SetValue(s.Aniso);
                fx.Parameters["uSquish"]?.SetValue(s.Squish);
                fx.Parameters["uRunScale"]?.SetValue(s.RunScale);
                fx.Parameters["uDir"]?.SetValue(s.Dir);
                fx.Parameters["uCanvasFit"]?.SetValue(fit);
                //地形剖面六件套:NPC 渍走默认零值(零剖面/零轴/零符号)=不扭不淡
                fx.Parameters["uProf"]?.SetValue(s.Profile);
                fx.Parameters["uProfN"]?.SetValue(s.ProfAxis);
                fx.Parameters["uProfQScale"]?.SetValue(s.ProfQScale);
                fx.Parameters["uProfQOff"]?.SetValue(s.ProfQOff);
                fx.Parameters["uInvWorldPerQ"]?.SetValue(s.InvWorldPerQ);
                fx.Parameters["uEdgeSign"]?.SetValue(s.EdgeSign);
                fx.CurrentTechnique.Passes[0].Apply();

                float side = s.Size * 2.4f;
                Vector2 scale = new(side * fit.X / canvas.Width, side * fit.Y / canvas.Height);
                sb.Draw(canvas, s.Pos - Main.screenPosition, null, Color.White,
                    0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawLakeFallback(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            foreach (LakeBlot b in lake) {
                if (!TryGetLakeY(b.OwnerWho, out float lakeY)) {
                    continue;
                }
                Vector2 pos = WorldToScreen(new Vector2(b.X, lakeY));
                if (!OnScreen(pos)) {
                    continue;
                }
                float bloom = MathHelper.Clamp(b.Age / (float)LakeBloomFrames, 0f, 1f);
                float fade = (1f - MathHelper.Clamp((b.Age - (b.Life - 46f)) / 46f, 0f, 1f)) * b.Fade;
                float w = b.Size * (0.6f + 1.4f * bloom) / tex.Width * 2f * zoom.X;
                sb.Draw(tex, pos, null, KikasaInk.InkBody * (0.5f * fade),
                    0f, tex.Size() * 0.5f, new Vector2(w, w * 0.16f * zoom.Y / zoom.X), SpriteEffects.None, 0f);
            }
        }

        private static void DrawListFallback(SpriteBatch sb, List<InkSplat> list, Rectangle view) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            foreach (InkSplat s in list) {
                if (!view.Contains(s.Pos.ToPoint())) {
                    continue;
                }
                float fade = (1f - MathHelper.Clamp((s.Age - (s.Life - 36f)) / 36f, 0f, 1f)) * s.DeadFade;
                if (fade <= 0.01f) {
                    continue;
                }
                //贴面姿态在回退里用缩放近似
                Vector2 shape = s.Dir.X != 0f
                    ? new Vector2(s.Aniso, s.Squish)
                    : new Vector2(s.Squish, s.Aniso);
                Vector2 basePos = s.Pos - Main.screenPosition;
                for (int i = 0; i < 3; i++) {
                    Vector2 off = new((KikasaInk.Hash((int)(s.Seed * 977f), i) - 0.5f) * s.Size * 0.5f * shape.X,
                        (KikasaInk.Hash((int)(s.Seed * 977f), i + 3) - 0.5f) * s.Size * 0.32f * shape.Y);
                    float blob = (0.3f + KikasaInk.Hash((int)(s.Seed * 977f), i + 6) * 0.24f) * s.Size / tex.Width * 2f;
                    sb.Draw(tex, basePos + off, null, KikasaInk.InkDeep * (0.55f * fade), 0f, origin,
                        new Vector2(blob * 1.25f, blob) * shape, SpriteEffects.None, 0f);
                    sb.Draw(tex, basePos + off, null, KikasaInk.InkBody * (0.85f * fade), 0f, origin,
                        new Vector2(blob, blob * 0.82f) * shape, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
