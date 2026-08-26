using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 缝匠视觉复合工具（C4 自有）：骨线顶点条带（StitcherThread.fx）+ 干骨拼装批
    /// （StitcherBoneDress.fx）+ 缝合位几何纯函数（服务器生成与各端预告共用，预告即承诺）。
    /// 骨线在活动 Deferred 批期间直接落图元（压在本批贴图之下），自管设备状态，
    /// 镜像 SkeletronBoneSpike 的先例；着色器缺失一律走贴图回退，不空窗。
    /// </summary>
    internal static class StitcherVfx
    {
        //色板：L5 骨白 + 缝线暖金（冷粉/冷青是他路独占禁区）
        internal static readonly Color BoneDust = new(222, 215, 200);
        internal static readonly Color BonePale = new(233, 226, 209);
        internal static readonly Color BoneShadow = new(148, 138, 118);
        internal static readonly Color ThreadGold = new(255, 225, 140);
        internal static readonly Color ThreadDark = new(140, 97, 41);
        internal static readonly Color ThreadCore = new(255, 247, 219);

        //==================== 骨线（顶点条带）====================

        private const int ThreadSegs = 14;
        private static readonly VertexPositionColorTexture[] threadVerts
            = new VertexPositionColorTexture[(ThreadSegs + 1) * 2];

        /// <summary>
        /// 骨线一根（世界坐标两端）：宏观垂弧按松弛度由 C# 顶点几何给出，小尺度摆动交给
        /// shader。tension=张力（0 松垂~1 绷直），fray=磨损（承伤累计外显），u=0 在 from 端，
        /// 玻点奔向 to 端（能量方向=货物方向）
        /// </summary>
        internal static void DrawThread(SpriteBatch sb, Vector2 from, Vector2 to,
            float tension, float fray, float alpha, float seed, float halfWidth = 2.6f) {
            if (alpha <= 0.01f) {
                return;
            }
            float len = Vector2.Distance(from, to);
            if (len < 4f) {
                return;
            }
            Effect effect = EffectLoader.StitcherThread?.Value;
            if (effect == null || CWRAsset.PerlinNoise?.Value == null) {
                DrawThreadFallback(sb, from, to, tension, alpha);
                return;
            }

            //二次贝塞尔垂弧：控制点下坠，坠深随松弛与线长
            float sag = len * 0.16f * (1f - tension) + 2f;
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(0f, sag);
            float wHalf = halfWidth * (1.7f - tension * 0.7f);

            for (int s = 0; s <= ThreadSegs; s++) {
                float t = s / (float)ThreadSegs;
                Vector2 p = Vector2.Lerp(Vector2.Lerp(from, ctrl, t), Vector2.Lerp(ctrl, to, t), t);
                Vector2 tan = Vector2.Lerp(ctrl - from, to - ctrl, t).SafeNormalize(Vector2.UnitX);
                Vector2 perp = new(-tan.Y, tan.X);
                //顶点色即数据通道：R=张力 G=磨损 A=包络
                Color vc = new(tension, fray, 0f, alpha);
                Vector2 l = p - perp * wHalf;
                Vector2 r = p + perp * wHalf;
                threadVerts[s * 2] = new VertexPositionColorTexture(new Vector3(l.X, l.Y, 0f), vc, new Vector2(t, 0f));
                threadVerts[s * 2 + 1] = new VertexPositionColorTexture(new Vector3(r.X, r.Y, 0f), vc, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            DepthStencilState origDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(seed % 1f);
            effect.Parameters["uLenPx"]?.SetValue(len);
            effect.Parameters["uThreadCol"]?.SetValue(ThreadGold.ToVector3());
            effect.Parameters["uCoreCol"]?.SetValue(ThreadCore.ToVector3());
            effect.Parameters["uDarkCol"]?.SetValue(ThreadDark.ToVector3());
            //噪声显式绑 s1（shader 内 register(s1)，VFX.md 采样器纪律）
            device.Textures[1] = CWRAsset.PerlinNoise.Value;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, threadVerts, 0, ThreadSegs * 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
            device.DepthStencilState = origDepth;
        }

        /// <summary>回退：默认批 A=0 加色细光条（着色器缺失时不空窗）</summary>
        private static void DrawThreadFallback(SpriteBatch sb, Vector2 from, Vector2 to, float tension, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float len = Vector2.Distance(from, to);
            Vector2 mid = (from + to) * 0.5f + new Vector2(0f, len * 0.12f * (1f - tension));
            float rot = (to - from).ToRotation();
            Color col = new Color(ThreadGold.R, ThreadGold.G, ThreadGold.B, 0) * (alpha * (0.35f + 0.4f * tension));
            sb.Draw(glow, mid - Main.screenPosition, null, col, rot, glow.Size() * 0.5f,
                new Vector2(len / glow.Width * 1.05f, 4f / glow.Height), SpriteEffects.None, 0f);
        }

        //==================== 干骨拼装批（Immediate + StitcherBoneDress）====================

        /// <summary>
        /// 切入干骨批：返回 false=着色器缺失，调用方走平染回退且不得调 EndDress。
        /// 用法：BeginDress → 逐件 SetDressParams+Draw → EndDress（还原默认批）
        /// </summary>
        internal static bool BeginDress(SpriteBatch sb) {
            Effect effect = EffectLoader.StitcherBoneDress?.Value;
            if (effect == null || CWRAsset.PerlinNoise?.Value == null) {
                return false;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.Textures[1] = CWRAsset.PerlinNoise.Value;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            //共享参数一次上载；帧区/磨损逐件 SetDressParams（全参每帧重设，VFX.md 参数纪律）
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uBonePale"]?.SetValue(BonePale.ToVector3());
            effect.Parameters["uBoneShadow"]?.SetValue(BoneShadow.ToVector3());
            effect.Parameters["uGold"]?.SetValue(ThreadGold.ToVector3());
            return true;
        }

        /// <summary>逐件参数：帧区钳制 + 干白/磨损/缝金；SetValue 后必须 Apply 才真正上卡</summary>
        internal static void SetDressParams(Texture2D tex, Rectangle frame,
            float chalk, float wear, float seamGlow, Vector2 seamY, float seed) {
            Effect effect = EffectLoader.StitcherBoneDress?.Value;
            if (effect == null) {
                return;
            }
            Vector2 texel = new(1f / tex.Width, 1f / tex.Height);
            effect.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X * texel.X, frame.Y * texel.Y, frame.Width * texel.X, frame.Height * texel.Y));
            effect.Parameters["uTexel"]?.SetValue(texel);
            effect.Parameters["uChalk"]?.SetValue(chalk);
            effect.Parameters["uWear"]?.SetValue(wear);
            effect.Parameters["uSeamGlow"]?.SetValue(seamGlow);
            effect.Parameters["uSeamY"]?.SetValue(seamY);
            effect.Parameters["uSeed"]?.SetValue(seed % 1f);
            effect.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>退出干骨批：还原默认批 + 归还 s1 槽</summary>
        internal static void EndDress(SpriteBatch sb) {
            Main.graphics.GraphicsDevice.Textures[1] = null;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 缝合位几何（服务器/各端共用纯函数）====================

        /// <summary>锚点打包入 ai[2]：tileX*8192+tileY；子世界 2000×6000 → 上界 1638 万 &lt; float 精确整数界 2^24</summary>
        internal static float PackAnchor(Point tile) => tile.X * 8192 + tile.Y;

        internal static Point UnpackAnchor(float packed) {
            int v = (int)packed;
            return new Point(v >> 13, v & 8191);
        }

        /// <summary>槽位横向偏移（tile）：1 只居中，2/3 只对称展开</summary>
        internal static int SlotOffsetTiles(int index, int count) {
            if (count <= 1) {
                return 0;
            }
            if (count == 2) {
                return index == 0 ? -3 : 3;
            }
            return (index - 1) * 4;
        }

        /// <summary>
        /// 槽位地表探针：锚行上 2 下 12 找首个实心面。锚（列+行）由服务器进缝合时冻结入
        /// ai[2] 过线，探针本身确定性 → 服务器生成点与各端预告点必然重合
        /// </summary>
        internal static Vector2 SlotGround(Point anchor, int index, int count) {
            int x = Math.Clamp(anchor.X + SlotOffsetTiles(index, count), 5, Main.maxTilesX - 5);
            for (int j = anchor.Y - 2; j <= anchor.Y + 12; j++) {
                if (WorldGen.InWorld(x, j) && WorldGen.SolidTile(x, j)) {
                    return new Vector2(x * 16f + 8f, j * 16f);
                }
            }
            return new Vector2(x * 16f + 8f, (anchor.Y + 1) * 16f);
        }
    }

    /// <summary>
    /// 缝合放件：骨件沿骨线轨道从匠架甩向缝合位（完成沿点燃，各端本地表现）。
    /// 收线是暴力的——ease-in 加速砸下；到位帧碎屑+闷响；线轨滞留 10f 渐散磨损上翻（余波相）
    /// </summary>
    internal class PRT_StitcherBoneCast : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override bool CanLoad() => DungeonworldEliteGate.Enabled;

        private const int LingerFrames = 10;
        private Vector2 fromPos;
        private Vector2 toPos;
        private int flight;
        private float spin;
        private float seed;

        public PRT_StitcherBoneCast Configure(Vector2 from, Vector2 to, int flightFrames) {
            fromPos = from;
            toPos = to;
            flight = Math.Max(1, flightFrames);
            Lifetime = flight + LingerFrames;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            spin = Main.rand.NextFloat(0.3f, 0.55f) * (Main.rand.NextBool() ? 1f : -1f);
            seed = Main.rand.NextFloat();
        }

        private float FlightT => MathHelper.Clamp(Time / (float)flight, 0f, 1f);

        public override void AI() {
            float e = FlightT * FlightT;
            Vector2 ctrl = (fromPos + toPos) * 0.5f + new Vector2(0f, 26f * (1f - e));
            Position = Vector2.Lerp(Vector2.Lerp(fromPos, ctrl, e), Vector2.Lerp(ctrl, toPos, e), e);
            Rotation += spin * (0.4f + e * 1.6f);
            Opacity = 1f;
            if (Time == flight) {
                //到位帧：碎屑迸散 + 闷响（PRT 只在表现端跑，直接发声安全）
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 5 }, toPos);
                for (int i = 0; i < 4; i++) {
                    Dust.NewDust(toPos - new Vector2(8f, 10f), 16, 10, DustID.Bone,
                        Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, -0.5f));
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = FlightT;
            float lingerFade = Time <= flight ? 1f : 1f - (Time - flight) / (float)LingerFrames;
            Vector2 end = t < 1f ? Position : toPos;
            //线轨：飞行中绷紧；到位后滞留渐散、磨损上翻（断线余味）
            StitcherVfx.DrawThread(spriteBatch, fromPos, end,
                t < 1f ? 0.95f : 0.55f, t < 1f ? 0.1f : 0.8f, 0.8f * lingerFade, seed);
            if (t < 1f) {
                Main.instance.LoadProjectile(ProjectileID.Bone);
                Texture2D bone = TextureAssets.Projectile[ProjectileID.Bone].Value;
                Color lit = Lighting.GetColor(Position.ToTileCoordinates()).MultiplyRGB(StitcherVfx.BoneDust);
                spriteBatch.Draw(bone, Position - Main.screenPosition, null, lit,
                    Rotation, bone.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>断线头：流产/致死沿迸出的线段残料，下坠翻卷、磨损上翻直至散尽</summary>
    internal class PRT_StitcherThreadSnip : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override bool CanLoad() => DungeonworldEliteGate.Enabled;

        private float segLen;
        private float seed;

        public PRT_StitcherThreadSnip Configure(int lifetime, float length) {
            Lifetime = lifetime;
            segLen = length;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            seed = Main.rand.NextFloat();
        }

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.96f, Math.Min(Velocity.Y + 0.22f, 9f));
            Rotation += 0.09f * (Velocity.X >= 0f ? 1f : -1f);
            Opacity = MathHelper.Clamp((1f - LifetimeCompletion) / 0.35f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = LifetimeCompletion;
            Vector2 tail = Position + Rotation.ToRotationVector2() * segLen * (1f - t * 0.3f);
            StitcherVfx.DrawThread(spriteBatch, Position, tail,
                0.15f, 0.4f + t * 0.6f, Opacity * 0.9f, seed, 2.2f);
            return false;
        }
    }
}
