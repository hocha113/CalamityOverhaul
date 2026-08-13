using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨渍贴花管理器:命中的余韵层,渍斑比墨滴活得久。
    /// 地面渍钉在世界上,NPC 渍挂在宿主身上随行(宿主消亡快淡收场);
    /// 环形上限防堆积,纯客户端表现,由 <see cref="KikasaRainSystem"/> 驱动、
    /// <see cref="KikasaRainRender"/> 在墨滴之下绘制
    /// </summary>
    internal static class KikasaInkFX
    {
        private const int GroundCap = 48;
        private const int NpcCap = 24;

        /// <summary>晕染扩张帧数:缘先扩后定</summary>
        private const int BloomFrames = 22;

        private const int GroundLife = 220;
        private const int NpcLife = 150;

        private class InkSplat
        {
            public Vector2 Pos;
            /// <summary>各向异性主轴(撞击面切向,单位向量)</summary>
            public Vector2 Dir;
            public float Aniso;
            public float Size;
            public float Seed;
            public int Age;
            public int Life;
            //NPC 附着字段,NpcWho=-1 即地面渍
            public int NpcWho = -1;
            public int NpcType;
            public Vector2 Offset;
            /// <summary>宿主消亡后的快淡</summary>
            public float DeadFade = 1f;
            public bool Done;
        }

        private static readonly List<InkSplat> ground = [];
        private static readonly List<InkSplat> attached = [];

        //==================== 入账 ====================

        /// <summary>地面渍:主轴取撞击方向的切向,溅斑总是横着摊开的</summary>
        public static void AddGroundSplat(Vector2 pos, Vector2 impactVel, float size) {
            if (Main.dedServ) {
                return;
            }
            if (ground.Count >= GroundCap) {
                ground.RemoveAt(0);
            }
            Vector2 dir = impactVel.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            ground.Add(new InkSplat {
                Pos = pos,
                Dir = dir,
                Aniso = Main.rand.NextFloat(1.25f, 1.7f),
                Size = size,
                Seed = Main.rand.NextFloat(8f),
                Life = GroundLife + Main.rand.Next(-30, 40),
            });
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
                Size = MathF.Min(size, MathF.Max(npc.width, 34f)),
                Seed = Main.rand.NextFloat(8f),
                Life = NpcLife + Main.rand.Next(-20, 30),
            });
        }

        //==================== 推进 ====================

        public static void Update() {
            for (int i = ground.Count - 1; i >= 0; i--) {
                InkSplat s = ground[i];
                s.Age++;
                //渍越新滴得越勤:挂壁滴淌交给自有滴淌件
                if (s.Age < s.Life * 0.6f && Main.rand.NextBool(30)) {
                    float dx = (KikasaInk.Hash((int)(s.Seed * 977f), s.Age) - 0.5f) * s.Size * 0.6f;
                    PRTLoader.NewParticle<PRT_KikasaInkDrip>(s.Pos + new Vector2(dx, s.Size * 0.2f),
                        new Vector2(0f, Main.rand.NextFloat(0.3f, 0.9f)), KikasaInk.InkBody,
                        Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(20, 32));
                }
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
        }

        public static void Clear() {
            ground.Clear();
            attached.Clear();
        }

        //==================== 绘制(由 KikasaRainRender 调用,墨滴之下) ====================

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

                DrawListShader(sb, fx, canvas, ground, view);
                DrawListShader(sb, fx, canvas, attached, view);
                sb.End();
                return;
            }

            //精灵回退:三团暗渍
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            DrawListFallback(sb, ground, view);
            DrawListFallback(sb, attached, view);
            sb.End();
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
                fx.Parameters["uDir"]?.SetValue(s.Dir);
                fx.CurrentTechnique.Passes[0].Apply();

                float side = s.Size * 2.4f;
                Vector2 scale = new(side / canvas.Width, side / canvas.Height);
                sb.Draw(canvas, s.Pos - Main.screenPosition, null, Color.White,
                    0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
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
                Vector2 basePos = s.Pos - Main.screenPosition;
                for (int i = 0; i < 3; i++) {
                    Vector2 off = new((KikasaInk.Hash((int)(s.Seed * 977f), i) - 0.5f) * s.Size * 0.5f,
                        (KikasaInk.Hash((int)(s.Seed * 977f), i + 3) - 0.5f) * s.Size * 0.32f);
                    float blob = (0.3f + KikasaInk.Hash((int)(s.Seed * 977f), i + 6) * 0.24f) * s.Size / tex.Width * 2f;
                    sb.Draw(tex, basePos + off, null, KikasaInk.InkDeep * (0.55f * fade), 0f, origin,
                        new Vector2(blob * 1.25f, blob), SpriteEffects.None, 0f);
                    sb.Draw(tex, basePos + off, null, KikasaInk.InkBody * (0.85f * fade), 0f, origin,
                        new Vector2(blob, blob * 0.82f), SpriteEffects.None, 0f);
                }
            }
        }
    }
}
