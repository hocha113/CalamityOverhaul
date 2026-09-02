using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.OtherMods.BossChecklist;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 荒花沙蟒图鉴沙盒：暖沙戈壁里的招牌动线循环——地表爬行、钻沙回返（沙面鼓包）、
    /// 破沙腾跃时花瓣与沙浪爆散。段链与八腿由 <see cref="SerpentPortraitRig"/> 驱动，
    /// 本类负责蒙皮（红花帧 + 脉冲加色）、沙暴天幕、前景沙带与花瓣微粒
    /// </summary>
    internal sealed class BssPortraitActor : BossPortraitActor
    {
        public static BssPortraitActor Instance => instance ??= new BssPortraitActor();
        private static BssPortraitActor instance;

        private readonly SerpentPortraitRig rig;
        private readonly PortraitMotes motes = new();

        private float petalTimer;
        private float flowerTimer;
        private float dustTimer;

        /// <summary>沙漠日光环境色</summary>
        private static readonly Color Ambient = new(250, 234, 204);
        /// <summary>腿的环境色（贴体暗一档）</summary>
        private static readonly Color LegAmbient = new(214, 194, 164);

        public override Vector2 SceneHalfSize => new(300f, 250f);

        private BssPortraitActor() {
            rig = new SerpentPortraitRig(bodyCount: 12, segmentGap: BssDirector.SegmentGap, sandY: 118f,
                patrolHalfWidth: 232f, neckGap: BssDirector.NeckGap) {
                WithClaws = true,
            };
            rig.OnDive = pos => SandBurst(pos, -Vector2.UnitY, 10, 0.8f);
            rig.OnLand = pos => SandBurst(pos, -Vector2.UnitY, 9, 0.9f);
            rig.OnBreach = (pos, dir) => {
                SandBurst(pos, dir, 20, 1.35f);
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f))
                        * Main.rand.NextFloat(2.2f, 6.5f);
                    SpawnPetal(pos + Main.rand.NextVector2Circular(18f, 10f), vel);
                }
            };
            //落步爪咬/滑刹犁沙的微尘（步足模拟的沙效出口）
            rig.OnLegSandFx = (pos, vel, power) =>
                SandBurst(pos, vel.SafeNormalize(-Vector2.UnitY), 2, 0.45f * power);
        }

        protected override void Reset() {
            rig.Reset();
            motes.Clear();
            petalTimer = flowerTimer = dustTimer = 0f;
        }

        protected override void Update(float dt) {
            float frames = dt * 60f;
            rig.Update(dt);
            motes.Update(frames);

            //天降花瓣：沙暴风向裹着零星红瓣横飘
            petalTimer += dt;
            if (petalTimer > 0.55f) {
                petalTimer = 0f;
                Vector2 half = SceneHalfSize;
                SpawnPetal(new Vector2(Main.rand.NextFloat(-half.X, half.X), -half.Y - 8f),
                    new Vector2(Main.rand.NextFloat(0.4f, 1.3f), Main.rand.NextFloat(0.5f, 1f)));
            }

            //红花节零星散瓣（地表期）
            flowerTimer += dt;
            if (flowerTimer > 0.9f && !rig.HeadBuried) {
                flowerTimer = 0f;
                SerpentPortraitRig.SegmentPose[] segs = rig.Segments;
                int pick = Main.rand.Next(4) * BssDirector.FlowerStep + (BssDirector.FlowerStep - 1);
                if (pick < segs.Length && !rig.SegmentBuried(pick)) {
                    SpawnPetal(segs[pick].Center + Main.rand.NextVector2Circular(8f, 8f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.2f, -0.3f)));
                }
            }

            //沙下推进：鼓包处持续冒沙尘
            if (rig.CurrentStage == SerpentPortraitRig.Stage.Buried) {
                dustTimer += dt;
                if (dustTimer > 0.09f) {
                    dustTimer = 0f;
                    Vector2 top = new(rig.MoundX + Main.rand.NextFloat(-24f, 24f), rig.SandY - 12f);
                    motes.Spawn(top, new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.6f, 1.6f)),
                        new Vector2(Main.rand.NextFloat(3f, 5.5f), Main.rand.NextFloat(2f, 3.5f)),
                        Color.Lerp(BssVfx.SandWarm, BssVfx.SandDark, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.35f, 0.7f), gravity: 0.1f, drag: 0.97f,
                        rotVel: Main.rand.NextFloat(-0.1f, 0.1f));
                }
            }
        }

        public override void Draw(SpriteBatch sb, in PortraitFrame frame) {
            DrawSky(sb, in frame);
            rig.DrawLegs(sb, in frame, LegAmbient);
            rig.DrawClawsBack(sb, in frame, LegAmbient);
            DrawWorm(sb, in frame);
            rig.DrawClawsFront(sb, in frame, LegAmbient);
            DrawSandBand(sb, in frame);
            motes.Draw(sb, in frame);
        }

        //==================== 蒙皮 ====================

        private void DrawWorm(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D headTex = SerpentPortraitRig.HeadTex?.Value;
            Texture2D bodyTex = SerpentPortraitRig.BodyTex?.Value;
            Texture2D tailTex = SerpentPortraitRig.TailTex?.Value;
            if (headTex == null || bodyTex == null || tailTex == null) {
                return;
            }
            Color skin = frame.Tint(Ambient);
            SerpentPortraitRig.SegmentPose[] segs = rig.Segments;

            //尾→头压顶（与战斗端整链层序一致）
            for (int i = segs.Length - 1; i >= 0; i--) {
                bool isTail = i == rig.TailOrdinal;
                Texture2D tex = isTail ? tailTex : bodyTex;
                Rectangle fr = isTail ? tailTex.Bounds
                    : SerpentPortraitRig.BodyFrame(bodyTex, i, BssStateContext.IsFlowerOrdinal(i));
                Vector2 origin = isTail ? SerpentPortraitRig.TailOrigin(tailTex) : fr.Size() * 0.5f;
                sb.Draw(tex, segs[i].Center, fr, skin, segs[i].Rotation,
                    origin, 1f, SpriteEffects.None, 0f);
            }
            //颚根藏在头底之下
            BssJawDraw.Draw(sb, rig.HeadPos, rig.HeadRotation,
                BssJawDraw.IdleOpen(Time * 3f), skin, Vector2.Zero);
            sb.Draw(headTex, rig.HeadPos, null, skin, rig.HeadRotation,
                headTex.Size() * 0.5f, 1f, SpriteEffects.None, 0f);

            //红花脉冲加色（剪影模式跳过）
            if (frame.Masked) {
                return;
            }
            for (int i = 0; i < rig.TailOrdinal; i++) {
                if (!BssStateContext.IsFlowerOrdinal(i)) {
                    continue;
                }
                float glow = 0.5f + 0.4f * MathF.Sin(Time * 2.6f + i * 0.9f);
                Rectangle fr = SerpentPortraitRig.BodyFrame(bodyTex, 2);
                sb.Draw(bodyTex, segs[i].Center, fr,
                    BssVfx.BloomRed with { A = 0 } * (0.5f * glow), segs[i].Rotation,
                    fr.Size() * 0.5f, 1.06f, SpriteEffects.None, 0f);
            }
        }

        //==================== 场景 ====================

        /// <summary>沙暴天幕：暖沙渐变 + 尘日 + 平流沙痕</summary>
        private void DrawSky(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = frame.SceneHalf;
            float dim = frame.Masked ? 0.42f : 1f;

            //天幕：暗琥珀 → 沙亮地平（不透明，压住图鉴纸底）
            const int Bands = 20;
            float top = -half.Y;
            float bandH = (rig.SandY - top) / Bands;
            for (int i = 0; i < Bands; i++) {
                float t = i / (float)(Bands - 1);
                Color c = Dim(Color.Lerp(new Color(116, 82, 58), new Color(196, 158, 106), t), dim);
                sb.Draw(pixel, new Vector2(-half.X, top + i * bandH), src, c, 0f,
                    Vector2.Zero, new Vector2(half.X * 2f, bandH + 1f), SpriteEffects.None, 0f);
            }

            if (frame.Masked) {
                return;
            }

            //尘蔽日：沙幕后一轮弱金盘
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                sb.Draw(glow, new Vector2(half.X * 0.42f, -half.Y * 0.56f), null,
                    new Color(255, 214, 150) with { A = 0 } * 0.4f, 0f, glow.Size() * 0.5f,
                    170f / glow.Width, SpriteEffects.None, 0f);
            }

            //平流沙痕：横风里的细沙线
            for (int i = 0; i < 10; i++) {
                float ph = i * 97.31f;
                float x = (Time * (52f + i * 6f) + ph) % (half.X * 2f + 60f) - half.X - 30f;
                float y = -half.Y + (ph * 1.71f) % (rig.SandY + half.Y - 24f);
                float a = 0.13f + 0.05f * MathF.Sin(ph);
                sb.Draw(pixel, new Vector2(x, y), src,
                    BssVfx.SandWarm with { A = 0 } * a, 0f, new Vector2(0f, 0.5f),
                    new Vector2(26f + i * 2f, 1.6f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>前景沙带（盖住沙下段链）+ 行进鼓包</summary>
        private void DrawSandBand(SpriteBatch sb, in PortraitFrame frame) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = frame.SceneHalf;
            float dim = frame.Masked ? 0.42f : 1f;

            //鼓包：沙下回返期的行进读数（画在带前、同色系略亮）
            if (rig.CurrentStage == SerpentPortraitRig.Stage.Buried) {
                Color moundC = Dim(new Color(214, 180, 124), dim);
                sb.Draw(pixel, new Vector2(rig.MoundX - 48f, rig.SandY - 9f), src, moundC, 0f,
                    Vector2.Zero, new Vector2(96f, 10f), SpriteEffects.None, 0f);
                sb.Draw(pixel, new Vector2(rig.MoundX - 31f, rig.SandY - 16f), src, moundC, 0f,
                    Vector2.Zero, new Vector2(62f, 8f), SpriteEffects.None, 0f);
                sb.Draw(pixel, new Vector2(rig.MoundX - 17f, rig.SandY - 21f), src, moundC, 0f,
                    Vector2.Zero, new Vector2(34f, 6f), SpriteEffects.None, 0f);
            }

            //沙带主体：亮沙 → 暗沙（不透明遮挡层）
            const int Bands = 12;
            float bandH = (half.Y - rig.SandY) / Bands;
            for (int i = 0; i < Bands; i++) {
                float t = i / (float)(Bands - 1);
                Color c = Dim(Color.Lerp(new Color(198, 164, 110), new Color(122, 92, 58), t), dim);
                sb.Draw(pixel, new Vector2(-half.X, rig.SandY + i * bandH), src, c, 0f,
                    Vector2.Zero, new Vector2(half.X * 2f, bandH + 1f), SpriteEffects.None, 0f);
            }
            //带缘亮线
            sb.Draw(pixel, new Vector2(-half.X, rig.SandY - 1.5f), src,
                Dim(new Color(224, 192, 136), dim), 0f,
                Vector2.Zero, new Vector2(half.X * 2f, 2.2f), SpriteEffects.None, 0f);
        }

        //==================== 微粒 ====================

        private void SandBurst(Vector2 pos, Vector2 dir, int count, float power) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-1f, 1f))
                    * Main.rand.NextFloat(2f, 6.5f) * power;
                motes.Spawn(pos + Main.rand.NextVector2Circular(12f, 6f), vel,
                    new Vector2(Main.rand.NextFloat(4f, 8f), Main.rand.NextFloat(2.6f, 4.6f)),
                    Color.Lerp(BssVfx.SandWarm, BssVfx.SandDark, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.5f, 1.05f), gravity: 0.2f, drag: 0.984f,
                    rot: Main.rand.NextFloat(MathHelper.TwoPi),
                    rotVel: Main.rand.NextFloat(-0.15f, 0.15f));
            }
        }

        private void SpawnPetal(Vector2 pos, Vector2 vel) {
            Color c = Color.Lerp(BssVfx.BloomRed, new Color(236, 110, 104), Main.rand.NextFloat(0.6f));
            motes.Spawn(pos, vel,
                new Vector2(Main.rand.NextFloat(5.5f, 8f), Main.rand.NextFloat(3f, 4.4f)),
                c, Main.rand.NextFloat(2.4f, 4f), gravity: 0.02f, drag: 0.988f,
                rot: Main.rand.NextFloat(MathHelper.TwoPi),
                rotVel: Main.rand.NextFloat(-0.09f, 0.09f));
        }

        /// <summary>不透明压暗（剪影模式的场景层保亮度层次、不透明度不动）</summary>
        private static Color Dim(Color c, float mul)
            => new((int)(c.R * mul), (int)(c.G * mul), (int)(c.B * mul), c.A);
    }
}
