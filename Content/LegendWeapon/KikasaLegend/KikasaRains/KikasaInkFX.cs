using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

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
    /// <see cref="KikasaRainRender"/> 在墨滴之下绘制
    /// </summary>
    internal static class KikasaInkFX
    {
        private const int GroundCap = 48;
        private const int NpcCap = 24;
        private const int LakeCap = 12;

        /// <summary>晕染扩张帧数:缘先扩后定</summary>
        private const int BloomFrames = 22;

        /// <summary>湖面墨晕晕开帧数:水里散得慢</summary>
        private const int LakeBloomFrames = 46;

        private const int GroundLife = 220;
        private const int NpcLife = 150;

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

        /// <summary>逐面滴淌:地渍只洇、墙渍贴墙下滑、顶渍垂滴脱离坠落;渍越新滴得越勤</summary>
        private static void UpdateSurfaceDrip(InkSplat s) {
            if (s.Age >= s.Life * 0.6f) {
                return;
            }
            switch (s.Surface) {
                case SplatSurface.Floor:
                    if (Main.rand.NextBool(80)) {
                        float dx = (KikasaInk.Hash((int)(s.Seed * 977f), s.Age) - 0.5f) * s.Size * 0.5f;
                        PRTLoader.NewParticle<PRT_KikasaInkDrip>(s.Pos + new Vector2(dx, -2f),
                            new Vector2(0f, 0.2f), KikasaInk.InkBody,
                            Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(10, 16));
                    }
                    break;
                case SplatSurface.Ceiling:
                    if (Main.rand.NextBool(14)) {
                        float dx = (KikasaInk.Hash((int)(s.Seed * 977f), s.Age) - 0.5f) * s.Size * 0.6f;
                        PRTLoader.NewParticle<PRT_KikasaInkDrip>(s.Pos + new Vector2(dx, 4f),
                            new Vector2(0f, Main.rand.NextFloat(0.3f, 0.7f)), KikasaInk.InkBody,
                            Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(30, 44));
                    }
                    break;
                default: //两侧墙:贴着墙面下滑
                    if (Main.rand.NextBool(24)) {
                        float hug = s.Surface == SplatSurface.WallLeft ? 2f : -2f;
                        float dy = KikasaInk.Hash((int)(s.Seed * 977f), s.Age) * s.Size * 0.35f;
                        PRTLoader.NewParticle<PRT_KikasaInkDrip>(s.Pos + new Vector2(hug, dy),
                            new Vector2(0f, Main.rand.NextFloat(0.4f, 0.9f)), KikasaInk.InkBody,
                            Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(20, 32));
                    }
                    break;
            }
        }

        private static bool TryGetLakeY(int ownerWho, out float lakeY) {
            lakeY = 0f;
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers) {
                return false;
            }
            Player pl = Main.player[ownerWho];
            if (pl?.active != true || !pl.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT < 0.85f) {
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

        //==================== 绘制(由 KikasaRainRender 调用,墨滴之下) ====================

        public static void Draw(SpriteBatch sb) {
            if (ground.Count == 0 && attached.Count == 0 && lake.Count == 0) {
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

                //湖面墨晕最底
                if (lake.Count > 0) {
                    fx.CurrentTechnique = fx.Techniques["TechLakeBlot"];
                    DrawLakeShader(sb, fx, canvas, view);
                }
                fx.CurrentTechnique = fx.Techniques["TechSplat"];
                DrawListShader(sb, fx, canvas, ground, view);
                DrawListShader(sb, fx, canvas, attached, view);
                sb.End();
                return;
            }

            //精灵回退
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            DrawLakeFallback(sb, view);
            DrawListFallback(sb, ground, view);
            DrawListFallback(sb, attached, view);
            sb.End();
        }

        private static void DrawLakeShader(SpriteBatch sb, Effect fx, Texture2D canvas, Rectangle view) {
            foreach (LakeBlot b in lake) {
                if (!TryGetLakeY(b.OwnerWho, out float lakeY)) {
                    continue;
                }
                Vector2 pos = new(b.X, lakeY);
                if (!view.Contains(pos.ToPoint())) {
                    continue;
                }
                float bloom = MathHelper.Clamp(b.Age / (float)LakeBloomFrames, 0f, 1f);
                float dilute = MathHelper.Clamp((b.Age - 40f) / (b.Life - 60f), 0f, 1f);
                float fade = (1f - MathHelper.Clamp((b.Age - (b.Life - 46f)) / 46f, 0f, 1f)) * b.Fade;
                if (fade <= 0.01f) {
                    continue;
                }
                fx.Parameters["uSeed"]?.SetValue(b.Seed);
                fx.Parameters["uBloom"]?.SetValue(bloom);
                fx.Parameters["uDry"]?.SetValue(dilute);
                fx.Parameters["uFade"]?.SetValue(fade);
                fx.CurrentTechnique.Passes[0].Apply();

                float w = b.Size * 2.6f;
                float h = b.Size * 1.1f;
                sb.Draw(canvas, pos - Main.screenPosition, null, Color.White, 0f,
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
                fx.Parameters["uSeed"]?.SetValue(s.Seed);
                fx.Parameters["uBloom"]?.SetValue(bloom);
                fx.Parameters["uDry"]?.SetValue(dry);
                fx.Parameters["uRun"]?.SetValue(run);
                fx.Parameters["uFade"]?.SetValue(fade);
                fx.Parameters["uAniso"]?.SetValue(s.Aniso);
                fx.Parameters["uSquish"]?.SetValue(s.Squish);
                fx.Parameters["uRunScale"]?.SetValue(s.RunScale);
                fx.Parameters["uDir"]?.SetValue(s.Dir);
                fx.CurrentTechnique.Passes[0].Apply();

                float side = s.Size * 2.4f;
                Vector2 scale = new(side / canvas.Width, side / canvas.Height);
                sb.Draw(canvas, s.Pos - Main.screenPosition, null, Color.White,
                    0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawLakeFallback(SpriteBatch sb, Rectangle view) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            foreach (LakeBlot b in lake) {
                if (!TryGetLakeY(b.OwnerWho, out float lakeY)) {
                    continue;
                }
                Vector2 pos = new(b.X, lakeY);
                if (!view.Contains(pos.ToPoint())) {
                    continue;
                }
                float bloom = MathHelper.Clamp(b.Age / (float)LakeBloomFrames, 0f, 1f);
                float fade = (1f - MathHelper.Clamp((b.Age - (b.Life - 46f)) / 46f, 0f, 1f)) * b.Fade;
                float w = b.Size * (0.6f + 1.4f * bloom) / tex.Width * 2f;
                sb.Draw(tex, pos - Main.screenPosition, null, KikasaInk.InkBody * (0.5f * fade),
                    0f, tex.Size() * 0.5f, new Vector2(w, w * 0.16f), SpriteEffects.None, 0f);
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
