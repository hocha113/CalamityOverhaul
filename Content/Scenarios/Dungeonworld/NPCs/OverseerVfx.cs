using CalamityOverhaul.Common;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 监工视觉共置层：铸铁材质批（OverseerIronCast 的作用域式批管理）、
    /// 自有铁链绘制（与 A2 的 Undrowned.DrawChainLine 解耦，铁色调 + 张力/振颤参数）、
    /// 钟摆弧光带状图元（OverseerPendArc 走 DrawUserPrimitives）、
    /// 断轨冲击帧屏幕层（OverseerBreakFrame，Weight 1.632）与铁屑/热印 PRT。
    /// 所有 shader 路径都带无 shader 降级（乘色两层 / SoftGlow stamps），
    /// 服务器端由调用方 Main.dedServ 自守。
    /// </summary>
    internal static class OverseerVfx
    {
        //==================== 铸铁材质批（作用域式：Begin → 逐部件 DrawIronPart → End）====================

        /// <summary>
        /// 切入 Immediate AlphaBlend 批并绑噪声，返回 shader 路径是否可用。
        /// 返回 false 时不切批（调用方仍在实体批内，DrawIronPart 自动走乘色降级）
        /// </summary>
        internal static bool BeginIronCast(SpriteBatch sb) {
            if (EffectLoader.OverseerIronCast?.IsLoaded != true || CWRAsset.PerlinNoise?.IsLoaded != true) {
                return false;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            return true;
        }

        /// <summary>
        /// 画一枚铸铁部件。shaderOn=true 时走 IronCast 重染（锈斑/受热辉光/轮廓热 rim），
        /// 否则降级为暗缘 + 铸铁乘色两层（与旧观感兼容）
        /// </summary>
        internal static void DrawIronPart(SpriteBatch sb, bool shaderOn, Texture2D tex, Vector2 drawPos,
            Rectangle frame, Color lightColor, float rotation, Vector2 origin, float scale,
            SpriteEffects effects, float heat, float rust, float seed, float alpha) {
            if (alpha <= 0.01f) {
                return;
            }
            if (!shaderOn) {
                sb.Draw(tex, drawPos, frame, FoundryOverseer.IronDeep * (0.75f * alpha),
                    rotation, origin, scale * 1.08f, effects, 0f);
                sb.Draw(tex, drawPos, frame, lightColor.MultiplyRGB(FoundryOverseer.IronMul) * alpha,
                    rotation, origin, scale, effects, 0f);
                return;
            }

            Effect fx = EffectLoader.OverseerIronCast.Value;
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uRust"]?.SetValue(rust);
            fx.Parameters["uHeat"]?.SetValue(heat);
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAspect"]?.SetValue(frame.Width / (float)Math.Max(frame.Height, 1));
            fx.CurrentTechnique.Passes[0].Apply();
            //vc.rgb=世界光照乘入体色，vc.a=整体透明度（shader 内预乘）
            sb.Draw(tex, drawPos, frame, lightColor with { A = (byte)(alpha * 255f) },
                rotation, origin, scale, effects, 0f);
        }

        /// <summary>退出材质批回实体批（shaderOn=false 表示 Begin 未切批，原样返回）</summary>
        internal static void EndIronCast(SpriteBatch sb, bool shaderOn) {
            if (!shaderOn) {
                return;
            }
            Main.instance.GraphicsDevice.Textures[1] = null;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 铁链（自有实现：铁色调 + 张力驱动的下垂/振颤）====================

        /// <summary>
        /// 铁链：Chain22 逐环压印。slack=1 常态悬垂，0=绷直；
        /// shiver&gt;0 时链中段高频横颤（拉紧/受载的机械应答），单位 px
        /// </summary>
        internal static void DrawChain(SpriteBatch sb, Vector2 from, Vector2 to,
            Color lightColor, float alpha, float slack = 1f, float shiver = 0f) {
            Texture2D chainTex = Terraria.GameContent.TextureAssets.Chain22?.Value;
            if (chainTex == null || alpha <= 0.02f) {
                return;
            }
            Vector2 origin = chainTex.Size() * 0.5f;
            float dist = Vector2.Distance(from, to);
            int links = Math.Clamp((int)(dist / 12f), 2, 90);
            float sag = MathHelper.Clamp(dist * 0.14f, 4f, 46f) * MathHelper.Clamp(slack, 0f, 1f);
            Vector2 axis = dist > 0.01f ? (to - from) / dist : Vector2.UnitY;
            Vector2 perp = new(-axis.Y, axis.X);
            float shiverPhase = Main.GlobalTimeWrappedHourly * 46f;
            Color tint = lightColor.MultiplyRGB(FoundryOverseer.IronMul) * (0.92f * alpha);
            Color deep = FoundryOverseer.IronDeep * (0.55f * alpha);
            Vector2 prev = from;
            for (int i = 1; i <= links; i++) {
                float k = i / (float)links;
                Vector2 p = Vector2.Lerp(from, to, k);
                float arc = MathF.Sin(k * MathHelper.Pi);
                p.Y += arc * sag;
                if (shiver > 0.01f) {
                    p += perp * (MathF.Sin(shiverPhase + k * 9.4f) * arc * shiver);
                }
                Vector2 mid = (prev + p) * 0.5f - Main.screenPosition;
                float rot = (p - prev).ToRotation() + MathHelper.PiOver2;
                sb.Draw(chainTex, mid, null, deep, rot, origin, 0.98f, SpriteEffects.None, 0f);
                sb.Draw(chainTex, mid, null, tint, rot, origin, 0.9f, SpriteEffects.None, 0f);
                prev = p;
            }
        }

        //==================== 钟摆弧光（带状图元 + OverseerPendArc）====================

        private static readonly VertexPositionColorTexture[] arcVerts = new VertexPositionColorTexture[128];

        /// <summary>
        /// 摆锤弧光带：pts[0]=尾（最旧）→ pts[count-1]=头（摆锤当前位）。
        /// 顶点吃世界坐标（shader 端 transformMatrix 变换），带宽向尾收窄，
        /// 顶点 A 沿弧衰减；speed&lt;0.15 时 shader 侧自动熄灭（伤害窗同源可读）。
        /// 无 shader 降级为 SoftGlow 加色 stamps（调用方须处于预乘实体批内）
        /// </summary>
        internal static void DrawPendArcStrip(SpriteBatch sb, Vector2[] pts, int count,
            Vector2 anchor, float width, float speed, float seed, float alpha) {
            if (count < 2 || alpha <= 0.02f || speed <= 0.05f) {
                return;
            }
            count = Math.Min(count, arcVerts.Length / 2);

            if (EffectLoader.OverseerPendArc?.IsLoaded != true || CWRAsset.PerlinNoise?.IsLoaded != true) {
                DrawPendArcFallback(sb, pts, count, speed, alpha);
                return;
            }

            for (int i = 0; i < count; i++) {
                float u = i / (count - 1f);
                Vector2 outward = pts[i] - anchor;
                float len = outward.Length();
                outward = len > 0.01f ? outward / len : Vector2.UnitY;
                //带宽向尾收窄；路径贴近外缘（锐线在外弧）
                float w = width * (0.45f + 0.55f * u);
                Vector2 outer = pts[i] + outward * (w * 0.22f);
                Vector2 inner = pts[i] - outward * (w * 0.78f);
                byte a = (byte)(255f * alpha * (0.35f + 0.65f * u));
                Color vc = new(255, 255, 255, a);
                arcVerts[i * 2] = new VertexPositionColorTexture(new Vector3(outer, 0f), vc, new Vector2(u, 0f));
                arcVerts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(inner, 0f), vc, new Vector2(u, 1f));
            }

            sb.End();
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.BlendState = BlendState.Additive;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            Effect fx = EffectLoader.OverseerPendArc.Value;
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSpeed"]?.SetValue(speed);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, arcVerts, 0, count * 2 - 2);
            gd.Textures[1] = null;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无 shader 降级：预乘实体批内 A=0 加色 stamps</summary>
        private static void DrawPendArcFallback(SpriteBatch sb, Vector2[] pts, int count, float speed, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Vector2 gOrigin = glow.Size() * 0.5f;
            float gate = MathHelper.Clamp((speed - 0.15f) / 0.6f, 0f, 1f);
            for (int i = 0; i < count; i++) {
                float u = i / (count - 1f);
                float s = (10f + 10f * u) * 2f / glow.Width;
                sb.Draw(glow, pts[i] - Main.screenPosition, null,
                    (FoundryOverseer.FurnaceOrange with { A = 0 }) * (0.35f * alpha * gate * u),
                    0f, gOrigin, s, SpriteEffects.None, 0f);
            }
        }
    }

    //==================== 断轨冲击帧：全屏状态 + RenderHandle ====================

    /// <summary>断轨大拍的屏幕层状态（客户端本地，由断轨/死亡演出推入）</summary>
    internal static class OverseerScreenFX
    {
        internal static bool ImpactActive;
        internal static int ImpactAge;
        internal static int ImpactLife = 1;
        internal static float ImpactIntensity;

        /// <summary>推入一次冲击帧（调用方 Main.dedServ + 距离门自守）</summary>
        internal static void PushImpact(float intensity, int life) {
            ImpactActive = true;
            ImpactAge = 0;
            ImpactLife = Math.Max(life, 1);
            ImpactIntensity = intensity;
        }

        internal static void Update() {
            if (!ImpactActive) {
                return;
            }
            if (++ImpactAge >= ImpactLife) {
                ImpactActive = false;
                ImpactIntensity = 0f;
            }
        }
    }

    /// <summary>断轨冲击帧屏幕层，screenTarget ping-pong。Weight 1.632（A3 频段 1.630–1.639）</summary>
    internal class OverseerScreenRender : RenderHandle
    {
        public override float Weight => 1.632f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            OverseerScreenFX.Update();
            if (!OverseerScreenFX.ImpactActive
                || EffectLoader.OverseerBreakFrame?.IsLoaded != true
                || screenSwap == null || Main.screenTarget == null) {
                return;
            }

            Effect shader = EffectLoader.OverseerBreakFrame.Value;
            shader.Parameters["uIntensity"]?.SetValue(OverseerScreenFX.ImpactIntensity);
            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(
                OverseerScreenFX.ImpactAge / (float)OverseerScreenFX.ImpactLife, 0f, 1f));
            shader.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));

            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }

    //==================== PRT：铁屑与贴地热印 ====================

    /// <summary>铁屑：受击/冲击迸出的铸铁碎屑，重坠自旋，前 40% 生命带热尖</summary>
    internal class PRT_OverseerIronChip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 500;

        private Color initialColor;
        private float spin;

        public PRT_OverseerIronChip Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            spin = Main.rand.NextFloat(-0.22f, 0.22f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 20;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= 0.985f;
            Velocity.Y = MathF.Min(Velocity.Y + 0.42f, 12f);
            Rotation += spin + Velocity.X * 0.02f;
            float t = LifetimeCompletion;
            Opacity = 1f - MathF.Pow(t, 2.6f);
            Color = Color.Lerp(initialColor, FoundryOverseer.IronDeep, MathF.Pow(t, 1.4f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.06f, 0f, 0.7f);
            Vector2 scale = new Vector2(0.1f * (1f + stretch * 1.6f), 0.08f) * Scale;
            //暗铁本体
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            //热尖（前 40% 生命，A=0 加色）
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2.5f, 0f, 1f);
            if (fresh > 0.05f) {
                spriteBatch.Draw(tex, pos, null,
                    (FoundryOverseer.FurnaceOrange with { A = 0 }) * (0.7f * fresh * Opacity),
                    Rotation, origin, scale * new Vector2(0.55f, 0.8f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>贴地热印：锤底/滚碾路径的余热压痕，橙→暗红→熄的冷却史，横扁贴地</summary>
    internal class PRT_OverseerHeatScar : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private float widthPx;

        public PRT_OverseerHeatScar Configure(int lifetime, float width) {
            Lifetime = lifetime;
            widthPx = width;
            return this;
        }

        public override void Reset() {
            base.Reset();
            widthPx = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 50;
            }
            if (widthPx <= 0f) {
                widthPx = 40f;
            }
        }

        public override void AI() {
            Velocity = Vector2.Zero;
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 2f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float t = LifetimeCompletion;
            float heat = MathF.Pow(1f - t, 1.6f);
            float flick = 0.75f + 0.25f * MathF.Sin(t * 26f + Position.X * 0.05f);
            Vector2 scale = new Vector2(widthPx / tex.Width * 1.6f, 0.05f) * Scale;
            //焦痕本体（暗红→熄铁）
            spriteBatch.Draw(tex, pos, null,
                Color.Lerp(FoundryOverseer.SlagDark, FoundryOverseer.IronDeep, t) * (0.85f * Opacity),
                0f, origin, scale, SpriteEffects.None, 0f);
            //余热加色（A=0，热度衰减 + 微闪）
            spriteBatch.Draw(tex, pos, null,
                (FoundryOverseer.SlagHot with { A = 0 }) * (0.6f * heat * flick * Opacity),
                0f, origin, scale * new Vector2(0.8f, 0.9f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
