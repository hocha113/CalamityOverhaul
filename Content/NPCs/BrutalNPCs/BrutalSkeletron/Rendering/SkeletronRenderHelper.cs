using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering
{
    /// <summary>灵骨材质绘制辅助：幽灵臂条带、骨链、眼火、冠火、预警线、旋杀涡流</summary>
    internal static class SkeletronRenderHelper
    {
        #region 自持资源
        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        internal static Asset<Texture2D> TearFlame = null;
        [VaultLoaden(CWRConstant.Other + "SoulFire")]
        internal static Asset<Texture2D> SoulFire = null;
        #endregion

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

        /// <summary>着色器缺失时的顶点色回退效果</summary>
        private static BasicEffect GetFallbackEffect(GraphicsDevice device) {
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

        /// <summary>沿下垂曲线绘制骨节链，tension=1 时绷直</summary>
        public static void DrawBoneChain(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float tension, float opacity) {
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
                //幽蓝筋络衬在骨节之间（预乘批 A=0 加色）
                if (i % 2 == 0) {
                    Texture2D glow = CWRAsset.SoftGlow.Value;
                    spriteBatch.Draw(glow, (p + prev) * 0.5f - Main.screenPosition, null,
                        AsAdditive(GhostCyan) * (opacity * 0.16f), 0f, glow.Size() / 2f, 0.30f, SpriteEffects.None, 0f);
                }
                prev = p;
            }
        }

        #endregion

        #region 眼火 / 冠火

        /// <summary>双眼窝阴魂火，随头旋转，intensity 支持 >1 过曝帧</summary>
        public static void DrawEyeFlames(SpriteBatch spriteBatch, NPC head, float intensity, float alphaFade = 1f) {
            if (intensity <= 0.02f || alphaFade <= 0.02f) {
                return;
            }
            Texture2D soul = SoulFireTex;
            if (soul == null) {
                return;
            }
            int frame = (int)(Main.GameUpdateCount / 5 + head.whoAmI) % 5;
            Rectangle rect = new Rectangle(0, soul.Height / 5 * frame, soul.Width, soul.Height / 5);
            Vector2 orig = new Vector2(rect.Width / 2f, rect.Height * 0.72f);
            Texture2D glow = CWRAsset.SoftGlow.Value;

            for (int side = -1; side <= 1; side += 2) {
                //眼窝局部偏移随头旋转
                Vector2 local = new Vector2(side * 15f, -8f) * head.scale;
                Vector2 pos = head.Center + local.RotatedBy(head.rotation) - Main.screenPosition;
                float flick = 0.9f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + side * 2.1f + head.whoAmI);
                float s = MathHelper.Clamp(intensity, 0f, 1.6f) * flick;

                //光晕走预乘批 A=0 加色
                spriteBatch.Draw(glow, pos, null, AsAdditive(GhostDeep) * (0.5f * s * alphaFade), 0f, glow.Size() / 2f, 0.62f * s, SpriteEffects.None, 0f);
                spriteBatch.Draw(soul, pos, rect, Color.White * (0.9f * MathHelper.Clamp(s, 0f, 1f) * alphaFade),
                    head.rotation, orig, 0.62f * s, SpriteEffects.None, 0f);
            }
        }

        /// <summary>二阶段诅咒火之冠：头顶沿弧五舌幽火</summary>
        public static void DrawCrownFlames(SpriteBatch spriteBatch, NPC head, float intensity, float alphaFade = 1f) {
            if (intensity <= 0.02f || alphaFade <= 0.02f) {
                return;
            }
            Texture2D tongue = TearFlame?.Value;
            if (tongue == null) {
                return;
            }
            Vector2 torig = new Vector2(tongue.Width / 2f, tongue.Height);

            for (int i = 0; i < 5; i++) {
                float arc = (i - 2f) * 0.38f;
                //火舌根锚在颅顶弧线上
                Vector2 local = new Vector2((float)Math.Sin(arc) * 34f, -44f - (float)Math.Cos(arc) * 10f) * head.scale;
                Vector2 pos = head.Center + local.RotatedBy(head.rotation) - Main.screenPosition;
                float hash = (i * 37 % 11) / 11f;
                float flick = 0.72f + 0.38f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (9f + hash * 5f) + i * 2.7f);
                float h = intensity * flick;
                Color col = Color.Lerp(GhostCyan, GhostDeep, hash * 0.6f);
                //黑底遮罩贴图在预乘批必须 A=0 加色，否则出黑框
                spriteBatch.Draw(tongue, pos, null, AsAdditive(col) * (0.72f * h * alphaFade),
                    head.rotation + arc * 0.5f, torig, new Vector2(0.30f, 0.44f + 0.3f * h), SpriteEffects.None, 0f);
            }
        }

        private static Texture2D SoulFireTex => SoulFire?.Value;

        #endregion

        #region 冲刺预警线

        /// <summary>沿角度画预警光线（加色，读作"轨迹将至"）</summary>
        public static void DrawDashTelegraph(SpriteBatch spriteBatch, Vector2 origin, float angle, float strength) {
            if (strength <= 0.02f) {
                return;
            }
            Texture2D beam = CWRAsset.LightShot?.Value;
            if (beam == null) {
                return;
            }
            float len = 1500f;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
            Vector2 scale = new Vector2(len / beam.Width, MathHelper.Lerp(0.35f, 1.15f, strength) * pulse);
            Vector2 orig = new Vector2(0f, beam.Height / 2f);
            //灰度光线贴图在预乘批走 A=0 加色
            spriteBatch.Draw(beam, origin - Main.screenPosition, null, AsAdditive(GhostCyan) * (0.42f * strength),
                angle, orig, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(beam, origin - Main.screenPosition, null, AsAdditive(BonePale) * (0.20f * strength),
                angle, orig, scale * new Vector2(1f, 0.4f), SpriteEffects.None, 0f);
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
