using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.OtherMods.BossChecklist;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Rendering
{
    /// <summary>
    /// 脓蕾沙蟒图鉴沙盒：腐化沙暮色里的招牌动线循环。段链与八腿由
    /// <see cref="SerpentPortraitRig"/> 驱动（贴图与荒花同源），蒙皮走
    /// <c>FssCorruptSkin</c> 着色器换皮（缺编回退 <see cref="FssVfx.SkinMul"/> 手染，
    /// 剪影模式同走回退路），囊肿节配灵液辉光与滴漏
    /// </summary>
    internal sealed class FssPortraitActor : BossPortraitActor
    {
        public static FssPortraitActor Instance => instance ??= new FssPortraitActor();
        private static FssPortraitActor instance;

        private readonly SerpentPortraitRig rig;
        private readonly PortraitMotes motes = new();

        private float dripTimer;
        private float dustTimer;

        /// <summary>腐沙暮色环境光（着色器路径的顶点色）</summary>
        private static readonly Color Ambient = new(230, 224, 238);
        /// <summary>腿的手染环境色（≈ Ambient × SkinMul 再压一档）</summary>
        private static readonly Color LegAmbient = new(140, 120, 170);

        public override Vector2 SceneHalfSize => new(322f, 258f);

        private FssPortraitActor() {
            rig = new SerpentPortraitRig(bodyCount: 12, segmentGap: 46f, sandY: 122f, patrolHalfWidth: 236f);
            rig.OnDive = pos => SandBurst(pos, -Vector2.UnitY, 10, 0.8f);
            rig.OnLand = pos => SandBurst(pos, -Vector2.UnitY, 9, 0.9f);
            rig.OnBreach = (pos, dir) => {
                SandBurst(pos, dir, 18, 1.3f);
                for (int i = 0; i < 9; i++) {
                    Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-1f, 1f))
                        * Main.rand.NextFloat(2.4f, 6.8f);
                    motes.Spawn(pos + Main.rand.NextVector2Circular(16f, 9f), vel,
                        new Vector2(Main.rand.NextFloat(3.6f, 6.5f), Main.rand.NextFloat(3f, 5f)),
                        Color.Lerp(FssVfx.IchorGold, FssVfx.IchorBright, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.6f, 1.1f), gravity: 0.24f, drag: 0.985f,
                        rot: Main.rand.NextFloat(MathHelper.TwoPi),
                        rotVel: Main.rand.NextFloat(-0.14f, 0.14f));
                }
            };
        }

        protected override void Reset() {
            rig.Reset();
            motes.Clear();
            dripTimer = dustTimer = 0f;
        }

        protected override void Update(float dt) {
            float frames = dt * 60f;
            rig.Update(dt);
            motes.Update(frames);

            //囊肿滴漏：地表期从随机囊肿节垂灵液
            dripTimer += dt;
            if (dripTimer > 0.45f && !rig.HeadBuried) {
                dripTimer = 0f;
                SerpentPortraitRig.SegmentPose[] segs = rig.Segments;
                int pick = Main.rand.Next(4) * FssDirector.CystStep + (FssDirector.CystStep - 1);
                if (pick < rig.TailOrdinal && !rig.SegmentBuried(pick)) {
                    motes.Spawn(segs[pick].Center + Main.rand.NextVector2Circular(9f, 9f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.3f, 0.8f)),
                        new Vector2(2.6f, Main.rand.NextFloat(4f, 6f)),
                        Color.Lerp(FssVfx.IchorGold, FssVfx.IchorDeep, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.7f, 1f), gravity: 0.14f, drag: 0.995f);
                }
            }

            //沙下推进：鼓包冒腐沙
            if (rig.CurrentStage == SerpentPortraitRig.Stage.Buried) {
                dustTimer += dt;
                if (dustTimer > 0.09f) {
                    dustTimer = 0f;
                    Vector2 top = new(rig.MoundX + Main.rand.NextFloat(-24f, 24f), rig.SandY - 12f);
                    motes.Spawn(top, new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.6f, 1.6f)),
                        new Vector2(Main.rand.NextFloat(3f, 5.5f), Main.rand.NextFloat(2f, 3.5f)),
                        Color.Lerp(FssVfx.TaintedSand, FssVfx.NecroShadow, Main.rand.NextFloat(0.6f)),
                        Main.rand.NextFloat(0.35f, 0.7f), gravity: 0.1f, drag: 0.97f,
                        rotVel: Main.rand.NextFloat(-0.1f, 0.1f));
                }
            }
        }

        public override void Draw(SpriteBatch sb, in PortraitFrame frame) {
            DrawSky(sb, in frame);
            rig.DrawLegs(sb, in frame, LegAmbient);
            DrawWorm(sb, in frame);
            DrawSandBand(sb, in frame);
            motes.Draw(sb, in frame);
        }

        //==================== 蒙皮 ====================

        private bool IsCyst(int ordinal)
            => ordinal < rig.TailOrdinal && FssStateContext.IsCystOrdinal(ordinal);

        private void DrawWorm(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D headTex = SerpentPortraitRig.HeadTex?.Value;
            Texture2D bodyTex = SerpentPortraitRig.BodyTex?.Value;
            Texture2D tailTex = SerpentPortraitRig.TailTex?.Value;
            if (headTex == null || bodyTex == null || tailTex == null) {
                return;
            }
            SerpentPortraitRig.SegmentPose[] segs = rig.Segments;

            Effect shader = EffectLoader.FssCorruptSkin?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            //剪影模式与缺编都走手染回退（着色器输出不受蒙版控制）
            if (shader != null && noise != null && !frame.Masked) {
                sb.End();
                BossPortraitStage.BeginShader(sb, frame, null);
                shader.CurrentTechnique = shader.Techniques["FesterTech"];
                shader.Parameters["uTime"]?.SetValue(Time);
                //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成本体贴图（镜像战斗绑定）
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                float vein = 0.55f * (0.88f + 0.12f * MathF.Sin(Time * 2.3f));
                for (int i = segs.Length - 1; i >= 0; i--) {
                    bool isTail = i == rig.TailOrdinal;
                    Texture2D tex = isTail ? tailTex : bodyTex;
                    Rectangle fr = isTail ? tailTex.Bounds
                        : SerpentPortraitRig.BodyFrame(bodyTex, IsCyst(i));
                    float swell = IsCyst(i) ? 0.35f + 0.3f * MathF.Sin(Time * 2.1f + i) : 0f;
                    ApplySkin(shader, tex, fr, i * 0.173f, i + 1f, swell, vein);
                    sb.Draw(tex, segs[i].Center, fr, Ambient, segs[i].Rotation,
                        fr.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
                }
                float headSwell = 0.2f + 0.15f * MathF.Sin(Time * 1.7f);
                ApplySkin(shader, headTex, headTex.Bounds, 0.031f, 0f, headSwell, vein);
                sb.Draw(headTex, rig.HeadPos, null, Ambient, rig.HeadRotation,
                    headTex.Size() * 0.5f, 1f, SpriteEffects.None, 0f);

                sb.End();
                BossPortraitStage.BeginAlpha(sb, in frame);
            }
            else {
                Color skin = frame.Tint(Ambient.MultiplyRGB(FssVfx.SkinMul));
                for (int i = segs.Length - 1; i >= 0; i--) {
                    bool isTail = i == rig.TailOrdinal;
                    Texture2D tex = isTail ? tailTex : bodyTex;
                    Rectangle fr = isTail ? tailTex.Bounds
                        : SerpentPortraitRig.BodyFrame(bodyTex, IsCyst(i));
                    sb.Draw(tex, segs[i].Center, fr, skin, segs[i].Rotation,
                        fr.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
                }
                sb.Draw(headTex, rig.HeadPos, null, skin, rig.HeadRotation,
                    headTex.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
            }

            //囊肿灵液辉光（加色事件层，剪影跳过）
            if (frame.Masked) {
                return;
            }
            for (int i = 0; i < rig.TailOrdinal; i++) {
                if (!IsCyst(i)) {
                    continue;
                }
                float glow = 0.5f + 0.35f * MathF.Sin(Time * 3.1f + i * 0.8f);
                Rectangle fr = SerpentPortraitRig.BodyFrame(bodyTex, alt: true);
                sb.Draw(bodyTex, segs[i].Center, fr,
                    FssVfx.IchorBright with { A = 0 } * (0.45f * glow), segs[i].Rotation,
                    fr.Size() * 0.5f, 1.06f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>逐段皮肤参数（镜像战斗 DrawSegmentCore 的参数面，portrait 无侵蚀/裂躯）</summary>
        private static void ApplySkin(Effect shader, Texture2D tex, Rectangle fr,
            float seed, float phase, float swell, float vein) {
            shader.Parameters["uUvRect"]?.SetValue(new Vector4(
                fr.X / (float)tex.Width, fr.Y / (float)tex.Height,
                fr.Width / (float)tex.Width, fr.Height / (float)tex.Height));
            shader.Parameters["uSeed"]?.SetValue(seed);
            shader.Parameters["uPhase"]?.SetValue(phase);
            shader.Parameters["uSwell"]?.SetValue(MathHelper.Clamp(swell, 0f, 1f));
            shader.Parameters["uCrack"]?.SetValue(0f);
            shader.Parameters["uVein"]?.SetValue(vein);
            shader.CurrentTechnique.Passes[0].Apply();
        }

        //==================== 场景 ====================

        /// <summary>腐化暮色天幕：坏死紫暗 → 污沙地平 + 病日 + 平流沙痕</summary>
        private void DrawSky(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = frame.SceneHalf;
            float dim = frame.Masked ? 0.42f : 1f;

            const int Bands = 20;
            float top = -half.Y;
            float bandH = (rig.SandY - top) / Bands;
            for (int i = 0; i < Bands; i++) {
                float t = i / (float)(Bands - 1);
                Color c = Dim(Color.Lerp(new Color(56, 42, 72), new Color(146, 118, 92), t), dim);
                sb.Draw(pixel, new Vector2(-half.X, top + i * bandH), src, c, 0f,
                    Vector2.Zero, new Vector2(half.X * 2f, bandH + 1f), SpriteEffects.None, 0f);
            }

            if (frame.Masked) {
                return;
            }

            //病日：昏黄弱盘，比荒花的尘日更沉
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                sb.Draw(glow, new Vector2(-half.X * 0.38f, -half.Y * 0.52f), null,
                    new Color(226, 188, 128) with { A = 0 } * 0.3f, 0f, glow.Size() * 0.5f,
                    150f / glow.Width, SpriteEffects.None, 0f);
            }

            //平流沙痕：污沙色横风
            for (int i = 0; i < 9; i++) {
                float ph = i * 113.7f;
                float x = (Time * (46f + i * 5f) + ph) % (half.X * 2f + 60f) - half.X - 30f;
                float y = -half.Y + (ph * 1.93f) % (rig.SandY + half.Y - 24f);
                float a = 0.12f + 0.05f * MathF.Sin(ph);
                sb.Draw(pixel, new Vector2(x, y), src,
                    FssVfx.TaintedSand with { A = 0 } * a, 0f, new Vector2(0f, 0.5f),
                    new Vector2(24f + i * 2f, 1.5f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>前景腐沙带（盖住沙下段链）+ 行进鼓包</summary>
        private void DrawSandBand(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = frame.SceneHalf;
            float dim = frame.Masked ? 0.42f : 1f;

            if (rig.CurrentStage == SerpentPortraitRig.Stage.Buried) {
                Color moundC = Dim(new Color(168, 138, 106), dim);
                sb.Draw(pixel, new Vector2(rig.MoundX - 48f, rig.SandY - 9f), src, moundC, 0f,
                    Vector2.Zero, new Vector2(96f, 10f), SpriteEffects.None, 0f);
                sb.Draw(pixel, new Vector2(rig.MoundX - 31f, rig.SandY - 16f), src, moundC, 0f,
                    Vector2.Zero, new Vector2(62f, 8f), SpriteEffects.None, 0f);
                sb.Draw(pixel, new Vector2(rig.MoundX - 17f, rig.SandY - 21f), src, moundC, 0f,
                    Vector2.Zero, new Vector2(34f, 6f), SpriteEffects.None, 0f);
            }

            const int Bands = 12;
            float bandH = (half.Y - rig.SandY) / Bands;
            for (int i = 0; i < Bands; i++) {
                float t = i / (float)(Bands - 1);
                Color c = Dim(Color.Lerp(new Color(148, 120, 92), new Color(74, 60, 68), t), dim);
                sb.Draw(pixel, new Vector2(-half.X, rig.SandY + i * bandH), src, c, 0f,
                    Vector2.Zero, new Vector2(half.X * 2f, bandH + 1f), SpriteEffects.None, 0f);
            }
            sb.Draw(pixel, new Vector2(-half.X, rig.SandY - 1.5f), src,
                Dim(new Color(176, 146, 112), dim), 0f,
                Vector2.Zero, new Vector2(half.X * 2f, 2.2f), SpriteEffects.None, 0f);
        }

        //==================== 微粒 ====================

        private void SandBurst(Vector2 pos, Vector2 dir, int count, float power) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-1f, 1f))
                    * Main.rand.NextFloat(2f, 6.5f) * power;
                motes.Spawn(pos + Main.rand.NextVector2Circular(12f, 6f), vel,
                    new Vector2(Main.rand.NextFloat(4f, 8f), Main.rand.NextFloat(2.6f, 4.6f)),
                    Color.Lerp(FssVfx.TaintedSand, FssVfx.NecroShadow, Main.rand.NextFloat(0.7f)),
                    Main.rand.NextFloat(0.5f, 1.05f), gravity: 0.2f, drag: 0.984f,
                    rot: Main.rand.NextFloat(MathHelper.TwoPi),
                    rotVel: Main.rand.NextFloat(-0.15f, 0.15f));
            }
        }

        /// <summary>不透明压暗（剪影模式的场景层保亮度层次、不透明度不动）</summary>
        private static Color Dim(Color c, float mul)
            => new((int)(c.R * mul), (int)(c.G * mul), (int)(c.B * mul), c.A);
    }
}
