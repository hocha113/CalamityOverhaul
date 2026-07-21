using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>凝霜宣言域内 shader 资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishFrostMinnowAssets
    {
        /// <summary>命中点冰凌花纹 decal</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishFrostMinnowFern { get; private set; }
    }

    /// <summary>凝霜宣言</summary>
    internal static class FrostMinnowVFX
    {
        /// <summary>冰白</summary>
        public static readonly Color IceWhite = new(226, 243, 255);
        /// <summary>淡青</summary>
        public static readonly Color PaleCyan = new(150, 214, 236);
        /// <summary>深蓝</summary>
        public static readonly Color DeepBlue = new(56, 96, 156);
        /// <summary>暗蓝</summary>
        public static readonly Color AbyssBlue = new(22, 38, 68);
        /// <summary>霜雾灰蓝</summary>
        public static readonly Color MistBlue = new(176, 200, 218);


        /// <summary>冰晶脆响</summary>
        public static void CrystalTink(Vector2 pos, float pitch, float volume) {
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = pitch, Volume = volume, MaxInstances = 5 }, pos);
        }


        /// <summary>有棱角的冰晶碎屑</summary>
        public static void ChipBurst(Vector2 pos, Vector2 dir, int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(0.85f) * Main.rand.NextFloat(0.35f, 1f) * speed
                    + new Vector2(0f, Main.rand.NextFloat(-1.2f, 0f));
                Color body = Color.Lerp(DeepBlue, PaleCyan, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_SHPCShardGlass>(pos, vel, body, Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(PaleCyan, Main.rand.Next(20, 34));
            }
        }

        /// <summary>低温霜雾</summary>
        public static void Mist(Vector2 pos, Vector2 vel, float scale, int life, float opacity) {
            PRTLoader.NewParticle<PRT_FishFrostMinnowMist>(pos, vel, MistBlue, scale)
                ?.Configure(life, opacity);
        }

        /// <summary>冰晶单帧镜面闪，小而锐的冷白星闪，短命不驻留</summary>
        public static void Glint(Vector2 pos, float scale, int life = 10) {
            PRTLoader.NewParticle<PRT_Sparkle>(pos, Vector2.Zero, IceWhite, scale)
                ?.Configure(PaleCyan, life, Main.rand.NextFloat(-0.1f, 0.1f), 0.5f);
        }

        /// <summary>淡青冲击环</summary>
        public static void ImpactRing(Vector2 pos, float rot, float startScale, float finalScale, int life) {
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, PaleCyan * 0.75f, startScale)
                ?.Configure(new Vector2(1f, 0.8f), rot, finalScale, life);
        }

        /// <summary>冰晶碎裂复合拍，冰屑迸散+霜雾+暗雾尘压底+沿表面扁冲击环。ke 0..1 动能系数</summary>
        public static void CrystalShatter(Vector2 pos, Vector2 dir, float ke, float ringRotation) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitY);
            ChipBurst(pos, dir, 3 + (int)(3f * ke), 3.2f + 3.5f * ke);
            for (int i = 0; i < 2; i++) {
                Mist(pos + Main.rand.NextVector2Circular(6f, 6f), dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(0.5f, 1.3f)
                    , Main.rand.NextFloat(0.22f, 0.3f), Main.rand.Next(24, 34), 0.2f);
            }
            //暗雾尘压底, 亮部才立得住
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Smoke, dir.RotatedByRandom(1f) * Main.rand.NextFloat(0.8f, 2f)
                    , 190, AbyssBlue, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = true;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, DeepBlue, 0.08f)
                ?.Configure(new Vector2(1f, 0.62f), ringRotation, 0.3f + 0.18f * ke, 11);
        }

        //同一目标冰凌花限频
        private static readonly Dictionary<int, uint> fernClaim = new();

        /// <summary>命中冰凌蕨花</summary>
        public static void FernOnNPC(NPC target, Vector2 hitPos, Vector2 impactVel) {
            if (Main.dedServ || target == null) {
                return;
            }
            uint now = Main.GameUpdateCount;
            if (fernClaim.Count > 128) {
                fernClaim.Clear();
            }
            if (fernClaim.TryGetValue(target.whoAmI, out uint last) && now - last < 36) {
                return;
            }
            fernClaim[target.whoAmI] = now;
            Vector2 offset = hitPos - target.Center;
            float maxR = MathF.Max(target.width, target.height) * 0.4f;
            if (offset.Length() > maxR) {
                offset = offset.SafeNormalize(Main.rand.NextVector2Unit()) * maxR * 0.6f;
            }
            //长轴沿表面切向，垂直于入射方向
            float tangent = impactVel.ToRotation() + MathHelper.PiOver2 + Main.rand.NextFloat(-0.22f, 0.22f);
            float len = MathHelper.Clamp(MathF.Max(target.width, target.height) * 0.55f, 30f, 64f)
                * Main.rand.NextFloat(0.85f, 1.15f);
            PRTLoader.NewParticle<PRT_FishFrostMinnowFern>(target.Center + offset, Vector2.Zero, default, 1f)
                ?.Configure(target.whoAmI, offset, tangent, len, len * 0.62f, 52);
        }

        /// <summary>地面/墙面霜花印</summary>
        public static void FernPrint(Vector2 pos, float tangentRot, float len, int life = 42) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishFrostMinnowFern>(pos, Vector2.Zero, default, 1f)
                ?.Configure(-1, Vector2.Zero, tangentRot, len, len * 0.5f, life);
        }


        /// <summary>六角冰晶</summary>
        public static void DrawHexCrystal(SpriteBatch sb, Vector2 drawPos, float rotation, float radiusPx, float alpha, float coreAlpha = 0.45f) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || alpha <= 0.01f) {
                return;
            }
            Vector2 origin = tex.Size() / 2f;
            Vector2 blade = new Vector2(radiusPx * 0.42f / tex.Width, radiusPx * 2f / tex.Height);

            //暗底描边: 半透明压底给亮层一圈暗轮廓
            Color dark = AbyssBlue;
            dark.A = 175;
            for (int i = 0; i < 3; i++) {
                sb.Draw(tex, drawPos, null, dark * (0.8f * alpha), rotation + i * MathHelper.Pi / 3f, origin, blade * 1.24f, SpriteEffects.None, 0f);
            }
            //中层淡青(加色)
            Color mid = PaleCyan;
            mid.A = 0;
            for (int i = 0; i < 3; i++) {
                sb.Draw(tex, drawPos, null, mid * (0.62f * alpha), rotation + i * MathHelper.Pi / 3f, origin, blade, SpriteEffects.None, 0f);
            }
            //内层小晶面: 错开30°补出十二向棱角
            Color inner = IceWhite;
            inner.A = 0;
            for (int i = 0; i < 3; i++) {
                sb.Draw(tex, drawPos, null, inner * (0.38f * alpha), rotation + MathHelper.Pi / 6f + i * MathHelper.Pi / 3f, origin, blade * 0.5f, SpriteEffects.None, 0f);
            }
            //极小冰芯
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && coreAlpha > 0f) {
                Color core = IceWhite;
                core.A = 0;
                sb.Draw(glow, drawPos, null, core * (coreAlpha * alpha), 0f, glow.Size() / 2f, radiusPx * 0.55f / 32f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>单层晶刃三联（旋转拖影/位移残影用，不带暗底与冰芯）</summary>
        public static void DrawHexBlades(SpriteBatch sb, Vector2 drawPos, float rotation, float radiusPx, Color color, float alpha) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || alpha <= 0.01f) {
                return;
            }
            Vector2 origin = tex.Size() / 2f;
            Vector2 blade = new Vector2(radiusPx * 0.42f / tex.Width, radiusPx * 2f / tex.Height);
            color.A = 0;
            for (int i = 0; i < 3; i++) {
                sb.Draw(tex, drawPos, null, color * alpha, rotation + i * MathHelper.Pi / 3f, origin, blade, SpriteEffects.None, 0f);
            }
        }


        /// <summary>带过冲缓出（凝晶入场的落定曲线）</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - MathHelper.Clamp(x, 0f, 1f), 3f);
    }

    internal class PRT_FishFrostMinnowMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private int frameIdx;
        private float baseOpacity;
        private float sinkAccel;
        private float spin;

        public PRT_FishFrostMinnowMist Configure(int lifetime, float opacity = 0.24f, float sink = 0.018f) {
            Lifetime = lifetime;
            baseOpacity = opacity;
            sinkAccel = sink;
            return this;
        }

        public override void Reset() {
            base.Reset();
            frameIdx = 0;
            baseOpacity = 0f;
            sinkAccel = 0f;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            frameIdx = Main.rand.Next(4);
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(24, 36);
            }
            if (baseOpacity <= 0f) {
                baseOpacity = 0.22f;
            }
            if (sinkAccel <= 0f) {
                sinkAccel = 0.018f;
            }
        }

        public override void AI() {
            //冷雾下沉
            Velocity.X *= 0.94f;
            Velocity.Y = MathF.Min(Velocity.Y + sinkAccel, 1.1f);
            Scale += 0.0045f;
            Rotation += spin;

            float t = LifetimeCompletion;
            Opacity = baseOpacity * MathF.Min(t * 6f, 1f) * (1f - MathF.Pow(t, 2.2f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            int frameSize = tex.Width / 2;
            int fx = frameIdx % 2;
            int fy = frameIdx / 2;
            Rectangle frame = new(fx * frameSize, fy * frameSize, frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() / 2f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>冰凌蕨花 decal 粒子，命中点结晶花纹沿表面爬开的短命残迹（英雄时刻）。 本体走 <see cref="FishFrostMinnowAssets.FishFrostMinnowFern"/> shader quad， Voronoi 晶脉错相生长 + 前沿亮带 + 外梢先融；跟随宿主 NPC，宿主消失立即进入融解。 shader 缺失时降级为三条交叉淡青冰纹条</summary>
    internal class PRT_FishFrostMinnowFern : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private int npcIdx;
        private Vector2 anchorOffset;
        private float axisRot;
        private float halfX;
        private float halfY;
        private float seed;

        private const int GrowFrames = 14;   //生长前沿走完的帧数
        private const int MeltFrames = 20;   //末段融解帧数

        public PRT_FishFrostMinnowFern Configure(int npcIndex, Vector2 offset, float rot
            , float halfLen, float halfWide, int lifetime) {
            npcIdx = npcIndex;
            anchorOffset = offset;
            axisRot = rot;
            halfX = halfLen;
            halfY = halfWide;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            npcIdx = -1;
            anchorOffset = Vector2.Zero;
            axisRot = 0f;
            halfX = halfY = 0f;
            seed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Velocity = Vector2.Zero;
            seed = Main.rand.NextFloat(10f);
        }

        private int HoldEnd => Lifetime - MeltFrames;

        public override void AI() {
            if (npcIdx >= 0) {
                NPC npc = npcIdx < Main.maxNPCs ? Main.npc[npcIdx] : null;
                if (npc == null || !npc.active) {
                    //宿主没了，原地立即转入融解
                    npcIdx = -1;
                    if (Time < HoldEnd) {
                        Time = HoldEnd;
                    }
                }
                else {
                    Position = npc.Center + anchorOffset;
                }
            }
        }

        private float GrowT => FrostMinnowVFX.EaseOutCubic(MathHelper.Clamp(Time / (float)GrowFrames, 0f, 1f));
        private float FadeT => MathHelper.Clamp((Time - HoldEnd) / (float)MeltFrames, 0f, 1f);

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float grow = GrowT;
            float fade = FadeT;
            Effect fx = FishFrostMinnowAssets.FishFrostMinnowFern;
            Texture2D voro = CWRAsset.Extra_193?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            if (fx == null || voro == null || noise == null) {
                DrawFallback(spriteBatch, grow, fade);
                return false;
            }

            Vector2 ax = axisRot.ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            Vector2 c = Position;
            float hx = halfX * Scale;
            float hy = halfY * Scale;

            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((c - ax * hx - ay * hy).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((c + ax * hx - ay * hy).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((c - ax * hx + ay * hy).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((c + ax * hx + ay * hy).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uGrow"]?.SetValue(grow);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uGlint"]?.SetValue(MathHelper.Clamp((grow - 0.55f) * 2.5f, 0f, 1f) * (1f - fade));
            fx.Parameters["uVoroTex"]?.SetValue(voro);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
            return false;
        }

        /// <summary>shader 未就绪的降级，三条交叉淡青冰纹条，A=0 在预乘批中读作加色</summary>
        private void DrawFallback(SpriteBatch spriteBatch, float grow, float fade) {
            Texture2D streak = CWRAsset.Extra_98?.Value;
            if (streak == null) {
                return;
            }
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = streak.Size() * 0.5f;
            float alpha = grow * (1f - fade) * 0.5f;
            float len = halfX * Scale * 1.4f / streak.Height;
            for (int k = 0; k < 3; k++) {
                float rot = axisRot + k * MathHelper.Pi / 3f;
                spriteBatch.Draw(streak, pos, null, FrostMinnowVFX.PaleCyan with { A = 0 } * alpha
                    , rot, origin, new Vector2(0.10f, len * grow), SpriteEffects.None, 0f);
            }
        }
    }
}
