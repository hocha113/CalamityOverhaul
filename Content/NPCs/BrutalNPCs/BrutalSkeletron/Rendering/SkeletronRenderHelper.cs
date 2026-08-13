using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering
{
    /// <summary>灵骨材质绘制辅助：幽灵臂条带、骨链、眼火、冠火、预警线、旋杀涡流</summary>
    internal static class SkeletronRenderHelper
    {
        #region 色板（枯骨+阴魂火+诅咒暗）
        /// <summary>骨白</summary>
        public static readonly Color BonePale = new Color(232, 222, 196);
        /// <summary>骨影</summary>
        public static readonly Color BoneShadow = new Color(140, 128, 104);
        /// <summary>阴魂火主色</summary>
        public static readonly Color GhostCyan = new Color(110, 235, 214);
        /// <summary>阴魂火深色</summary>
        public static readonly Color GhostDeep = new Color(32, 120, 140);
        /// <summary>诅咒暗（深紫黑）</summary>
        public static readonly Color CurseDark = new Color(24, 12, 40);
        /// <summary>诅咒紫（点缀用）</summary>
        public static readonly Color CurseViolet = new Color(120, 70, 190);

        /// <summary>预乘 AlphaBlend 批里的加色写法：A=0 让黑底遮罩/光晕做纯加法</summary>
        public static Color AsAdditive(Color color) => new Color(color.R, color.G, color.B, (byte)0);
        #endregion

        #region 幽灵臂条带

        private const int ArmSegments = 16;
        private static readonly VertexPositionColorTexture[] armVerts = new VertexPositionColorTexture[(ArmSegments + 1) * 2];

        private static Vector2 QuadBez(Vector2 a, Vector2 c, Vector2 b, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        private static BasicEffect fallbackEffect;

        /// <summary>着色器缺失时的顶点色回退效果（冷焰批共用）</summary>
        internal static BasicEffect GetFallbackEffect(GraphicsDevice device) {
            fallbackEffect ??= new BasicEffect(device) {
                VertexColorEnabled = true,
                TextureEnabled = false,
            };
            fallbackEffect.World = Matrix.Identity;
            fallbackEffect.View = Matrix.Identity;
            fallbackEffect.Projection = VaultUtils.GetTransfromMatrix();
            return fallbackEffect;
        }

        /// <summary>卸载释放回退效果</summary>
        internal static void Unload() {
            fallbackEffect?.Dispose();
            fallbackEffect = null;
        }

        /// <summary>
        /// 绘制一条幽灵臂条带（世界坐标，肩→腕），腕端由调用方叠手掌贴图<br/>
        /// grow：materialize 头部生长 0~1；dissolve：自肩端向腕端侵蚀 0~1
        /// </summary>
        public static void DrawGhostArmStrip(Vector2 shoulder, Vector2 hand, float curvature,
            float width, float grow, float dissolve, float opacity, float seed) {
            if (opacity <= 0.01f || grow <= 0.01f) {
                return;
            }

            Vector2 axis = hand - shoulder;
            float len = axis.Length();
            if (len < 8f) {
                return;
            }
            Vector2 dir = axis / len;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 ctrl = (shoulder + hand) * 0.5f + perp * curvature;

            for (int i = 0; i <= ArmSegments; i++) {
                float t = i / (float)ArmSegments;
                Vector2 p = QuadBez(shoulder, ctrl, hand, t);
                //肩根细→前臂鼓→腕口收
                float profile = 0.30f + 0.70f * MathF.Pow(MathF.Sin(t * MathHelper.Pi * 0.92f + 0.08f), 0.62f);
                //切向由差分近似
                float tNext = MathHelper.Clamp(t + 0.03f, 0f, 1f);
                Vector2 tangent = (QuadBez(shoulder, ctrl, hand, tNext) - p).SafeNormalize(dir);
                Vector2 side = tangent.RotatedBy(MathHelper.PiOver2) * width * profile * 0.5f;

                Color col = Color.White * opacity;
                armVerts[i * 2] = new VertexPositionColorTexture(new Vector3(p.X + side.X, p.Y + side.Y, 0f), col, new Vector2(t, 0f));
                armVerts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(p.X - side.X, p.Y - side.Y, 0f), col, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullNone;

            Effect effect = EffectLoader.SkeletronGhostArm?.Value;
            if (effect != null && CWRAsset.PerlinNoise?.Value != null) {
                //预乘输出走 AlphaBlend
                device.BlendState = BlendState.AlphaBlend;
                effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uGrow"]?.SetValue(MathHelper.Clamp(grow, 0f, 1f));
                effect.Parameters["uDissolve"]?.SetValue(MathHelper.Clamp(dissolve, 0f, 1f));
                effect.Parameters["uSeed"]?.SetValue(seed % 1f);
                effect.Parameters["uCoreColor"]?.SetValue(BonePale.ToVector3());
                effect.Parameters["uBodyColor"]?.SetValue(GhostCyan.ToVector3());
                effect.Parameters["uEdgeColor"]?.SetValue(GhostDeep.ToVector3());
                effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, armVerts, 0, ArmSegments * 2);
                }
            }
            else {
                //回退：顶点色加色条带（生长/侵蚀吃进透明度），不依赖活动 SpriteBatch
                device.BlendState = BlendState.Additive;
                float fade = MathHelper.Clamp(grow, 0f, 1f) * (1f - MathHelper.Clamp(dissolve, 0f, 1f));
                Color fall = GhostCyan * (opacity * fade * 0.5f);
                for (int i = 0; i < armVerts.Length; i++) {
                    armVerts[i].Color = fall;
                }
                BasicEffect basic = GetFallbackEffect(device);
                foreach (EffectPass pass in basic.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, armVerts, 0, ArmSegments * 2);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>幽灵手掌贴图（加色三层：深晕/主体/亮核），世界坐标</summary>
        public static void DrawGhostHandSprite(SpriteBatch spriteBatch, Vector2 worldPos, float rotation,
            float scale, float opacity, int spriteDir = 1) {
            Main.instance.LoadNPC(NPCID.SkeletronHand);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronHand].Value;
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.SkeletronHand], 1);
            Rectangle rect = new Rectangle(0, 0, tex.Width, tex.Height / frameCount);
            Vector2 orig = rect.Size() / 2f;
            Vector2 drawPos = worldPos - Main.screenPosition;
            SpriteEffects fx = spriteDir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //注意：加色批源因子=SourceAlpha，染色必须携带alpha（A=0 会画不出东西）
            Main.EntitySpriteDraw(tex, drawPos, rect, GhostDeep * (opacity * 0.85f), rotation, orig, scale * 1.14f, fx, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, GhostCyan * (opacity * 0.8f), rotation, orig, scale, fx, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, new Color(225, 255, 248) * (opacity * 0.55f), rotation, orig, scale * 0.9f, fx, 0);
        }

        #endregion

        #region 骨链（物理手锁链）

        private static readonly Vector2[] chainPts = new Vector2[13];

        /// <summary>沿下垂曲线绘制骨节链，tension=1 时绷直；筋络为灵息绸带顶点层；seed 须逐链稳定</summary>
        public static void DrawBoneChain(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float tension, float opacity, float seed = 0.37f) {
            if (opacity <= 0.02f) {
                return;
            }
            Main.instance.LoadProjectile(ProjectileID.Bone);
            Texture2D bone = TextureAssets.Projectile[ProjectileID.Bone].Value;
            Vector2 orig = bone.Size() / 2f;

            float dist = from.Distance(to);
            if (dist < 24f) {
                return;
            }
            //松弛下垂量随张紧度收敛
            float sag = MathHelper.Lerp(MathHelper.Clamp(dist * 0.22f, 30f, 170f), 6f, MathHelper.Clamp(tension, 0f, 1f));
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(0f, sag);

            //幽蓝筋络：沿链曲线的灵息绸带（顶点层，深压在骨节之下）
            for (int i = 0; i < chainPts.Length; i++) {
                chainPts[i] = QuadBez(from, ctrl, to, i / (float)(chainPts.Length - 1));
            }
            DrawSpecterRibbon(chainPts, chainPts.Length, 7f, 10f,
                opacity * (0.34f + tension * 0.30f), 0.5f + tension * 0.6f,
                seed, 0.18f, 0.14f, 1.1f + tension * 1.6f);

            int links = (int)MathHelper.Clamp(dist / 22f, 4f, 34f);
            Vector2 prev = from;
            for (int i = 1; i <= links; i++) {
                float t = i / (float)links;
                Vector2 p = QuadBez(from, ctrl, to, t);
                Vector2 seg = p - prev;
                float rot = seg.ToRotation() + MathHelper.PiOver2;
                //隔节翻滚，避免复读贴纸
                float roll = (i % 2 == 0) ? 0.35f : -0.28f;
                Color lit = Lighting.GetColor((int)(p.X / 16f), (int)(p.Y / 16f));
                Color col = Color.Lerp(BoneShadow, BonePale, 0.55f).MultiplyRGB(lit) * opacity;
                spriteBatch.Draw(bone, p - Main.screenPosition, null, col, rot + roll, orig, 0.86f, SpriteEffects.None, 0f);
                prev = p;
            }
        }

        #endregion

        #region 眼火 / 冠火（阴魂冷焰顶点批）

        /// <summary>双眼窝阴魂火，随头旋转，intensity 支持 >1 过曝帧；压入冷焰批待 EndEntityDraw 统一顶点绘制</summary>
        public static void DrawEyeFlames(NPC head, float intensity, float alphaFade = 1f) {
            if (intensity <= 0.02f || alphaFade <= 0.02f) {
                return;
            }
            //焰轴 = 头顶方向
            float axisAngle = head.rotation - MathHelper.PiOver2;
            for (int side = -1; side <= 1; side += 2) {
                //眼窝局部偏移随头旋转，焰根压到眼窝下缘
                Vector2 local = new Vector2(side * 15f, -2f) * head.scale;
                Vector2 pos = head.Center + local.RotatedBy(head.rotation);
                float flick = 0.9f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + side * 2.1f + head.whoAmI);
                float s = MathHelper.Clamp(intensity, 0f, 1.6f) * flick;
                float sway = 0.10f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f + side * 1.3f);

                SkeletronFlameRender.Push(pos, axisAngle + sway,
                    new Vector2(26f, 46f) * head.scale * s,
                    MathHelper.Clamp(intensity * 0.75f, 0.3f, 1f),
                    (head.whoAmI * 0.31f + side * 0.17f) % 1f, 0.1f,
                    0.95f * MathHelper.Clamp(s, 0f, 1f) * alphaFade);
            }
        }

        /// <summary>二阶段诅咒火之冠：头顶沿弧五舌幽火（冷焰顶点批）</summary>
        public static void DrawCrownFlames(NPC head, float intensity, float alphaFade = 1f) {
            if (intensity <= 0.02f || alphaFade <= 0.02f) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                float arc = (i - 2f) * 0.38f;
                //火舌根锚在颅顶弧线上
                Vector2 local = new Vector2((float)Math.Sin(arc) * 34f, -40f - (float)Math.Cos(arc) * 10f) * head.scale;
                Vector2 pos = head.Center + local.RotatedBy(head.rotation);
                float hash = (i * 37 % 11) / 11f;
                float flick = 0.72f + 0.38f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (9f + hash * 5f) + i * 2.7f);
                float h = MathHelper.Clamp(intensity * flick, 0f, 1.5f);

                SkeletronFlameRender.Push(pos, head.rotation - MathHelper.PiOver2 + arc * 0.55f,
                    new Vector2(20f + 8f * h, (44f + 26f * h) * (i == 2 ? 1.2f : 1f)) * head.scale,
                    MathHelper.Clamp(intensity * 0.8f, 0f, 1f),
                    (head.whoAmI * 0.11f + i * 0.19f) % 1f, 0.25f + hash * 0.4f,
                    0.8f * MathHelper.Clamp(h, 0f, 1f) * alphaFade);
            }
        }

        #endregion

        #region 灵息绸带（通用顶点带：预警线/链筋络/运动轨迹）

        private const int MaxRibbonPoints = 64;
        private static readonly VertexPositionColorTexture[] ribbonVerts = new VertexPositionColorTexture[MaxRibbonPoints * 2];
        private static readonly Vector2[] telegraphPts = new Vector2[8];
        private static readonly Vector2[] trailPts = new Vector2[16];

        /// <summary>
        /// 灵息绸带顶点条带（世界坐标折线，uv.x 0=首点尾端→1=末点头端）<br/>
        /// 自管设备状态，可在活动 Deferred 批期间调用（图元先落，压在本批所有贴图之下）<br/>
        /// 着色器缺失时返回 false，调用方自行降级
        /// </summary>
        public static bool DrawSpecterRibbon(Vector2[] pts, int count, float halfWidthTail, float halfWidthHead,
            float opacity, float coreBoost, float seed, float fadeIn, float fadeOut, float flowSpeed) {
            if (opacity <= 0.02f || count < 2 || count > MaxRibbonPoints) {
                return true;
            }
            Effect effect = EffectLoader.SkeletronSpecterRibbon?.Value;
            if (effect == null || CWRAsset.PerlinNoise?.Value == null) {
                return false;
            }

            Color pack = new Color(MathHelper.Clamp(coreBoost, 0f, 1f), 0f, 0f, MathHelper.Clamp(opacity, 0f, 1f));
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 p = pts[i];
                //切向差分（端点取邻段）
                Vector2 tangent = i == 0
                    ? (pts[1] - pts[0]).SafeNormalize(Vector2.UnitX)
                    : (p - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 side = new Vector2(-tangent.Y, tangent.X) * MathHelper.Lerp(halfWidthTail, halfWidthHead, t);
                ribbonVerts[i * 2] = new VertexPositionColorTexture(new Vector3(p.X + side.X, p.Y + side.Y, 0f), pack, new Vector2(t, 0f));
                ribbonVerts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(p.X - side.X, p.Y - side.Y, 0f), pack, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            DepthStencilState origDepth = device.DepthStencilState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(seed % 1f);
            effect.Parameters["uFadeIn"]?.SetValue(MathHelper.Clamp(fadeIn, 0.001f, 1f));
            effect.Parameters["uFadeOut"]?.SetValue(MathHelper.Clamp(fadeOut, 0.001f, 1f));
            effect.Parameters["uFlowSpeed"]?.SetValue(flowSpeed);
            effect.Parameters["uCoreColor"]?.SetValue(BonePale.ToVector3());
            effect.Parameters["uBodyColor"]?.SetValue(GhostCyan.ToVector3());
            effect.Parameters["uEdgeColor"]?.SetValue(GhostDeep.ToVector3());
            effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, ribbonVerts, 0, (count - 1) * 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
            device.DepthStencilState = origDepth;
            return true;
        }

        /// <summary>冲刺预警线：自起点沿角度的灵息绸带（头端在远处收散）</summary>
        public static void DrawDashTelegraph(SpriteBatch spriteBatch, Vector2 origin, float angle, float strength) {
            if (strength <= 0.02f) {
                return;
            }
            float len = 1500f;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
            Vector2 dir = angle.ToRotationVector2();
            for (int i = 0; i < telegraphPts.Length; i++) {
                telegraphPts[i] = origin + dir * (len * i / (telegraphPts.Length - 1f));
            }
            float halfW = MathHelper.Lerp(9f, 26f, strength) * pulse;
            if (DrawSpecterRibbon(telegraphPts, telegraphPts.Length, halfW, halfW * 0.55f,
                0.72f * strength, 0.85f, angle * 0.159f, 0.05f, 0.45f, 2.6f)) {
                return;
            }
            //回退：灰度光线仅作衬光（着色器缺失时）
            Texture2D beam = CWRAsset.LightShot?.Value;
            if (beam == null) {
                return;
            }
            Vector2 scale = new Vector2(len / beam.Width, MathHelper.Lerp(0.35f, 1.15f, strength) * pulse);
            Vector2 orig = new Vector2(0f, beam.Height / 2f);
            spriteBatch.Draw(beam, origin - Main.screenPosition, null, AsAdditive(GhostCyan) * (0.42f * strength),
                angle, orig, scale, SpriteEffects.None, 0f);
        }

        /// <summary>沿 oldPos 的运动轨迹绸带（旋杀涂抹/砸击拖尾），零向量安全</summary>
        public static void DrawMotionRibbon(NPC npc, float heat, float halfWidth, float opacity) {
            if (heat <= 0.05f || opacity <= 0.02f) {
                return;
            }
            //oldPos[0] 最新 → 数组尾最旧；绸带 uv.x=0 是尾端，翻转填充
            int valid = 0;
            for (int i = npc.oldPos.Length - 1; i >= 0; i--) {
                if (npc.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                trailPts[valid++] = npc.oldPos[i] + npc.Size / 2f;
                if (valid >= trailPts.Length - 1) {
                    break;
                }
            }
            trailPts[valid++] = npc.Center;
            if (valid < 3) {
                return;
            }
            DrawSpecterRibbon(trailPts, valid, halfWidth * 0.25f, halfWidth,
                opacity * heat, 0.55f, npc.whoAmI * 0.137f, 0.4f, 0.1f, 1.8f);
        }

        #endregion

        #region 旋杀涡流（SkeletronSpinStorm.fx quad）

        /// <summary>头周涡流quad；converge&gt;0 时表现为向心汇聚（仪式/大招蓄力）</summary>
        public static void DrawSpinVortex(SpriteBatch spriteBatch, Vector2 worldCenter, float spin,
            float intensity, float converge, float radius = 340f) {
            if (intensity <= 0.02f) {
                return;
            }
            Effect shader = EffectLoader.SkeletronSpinStorm?.Value;
            if (shader == null || CWRAsset.PerlinNoise?.Value == null) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uSpin"]?.SetValue(spin);
            shader.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1.2f));
            shader.Parameters["uConverge"]?.SetValue(MathHelper.Clamp(converge, 0f, 1f));
            shader.Parameters["uColorA"]?.SetValue(GhostCyan.ToVector3());
            shader.Parameters["uColorB"]?.SetValue(GhostDeep.ToVector3());
            shader.Parameters["uBone"]?.SetValue(BonePale.ToVector3());
            shader.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            float size = radius * 2f;
            spriteBatch.Draw(quad, worldCenter - Main.screenPosition, null, Color.White, 0f,
                quad.Size() / 2f, new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        #endregion
    }
}
