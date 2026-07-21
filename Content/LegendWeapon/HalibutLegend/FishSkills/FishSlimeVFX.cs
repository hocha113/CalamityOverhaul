using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>胶着行迹域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishSlimeAssets
    {
        /// <summary>凝胶球本体，半透明果冻 blob，暗蓝厚缘+体内微泡+偏移内部高光</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishSlimeBlob { get; private set; }
    }

    /// <summary>胶着行迹 VFX，半透明蓝凝胶 AlphaBlend+内高光，禁大面积常驻纯白</summary>
    internal static class FishSlimeVFX
    {
        /// <summary>深蓝（厚缘/丝暗部/溅斑陈化色）</summary>
        public static readonly Color GelDeep = new(28, 74, 158);
        /// <summary>主体蓝（半透明体色）</summary>
        public static readonly Color GelBody = new(66, 138, 224);
        /// <summary>亮蓝（内层/滑珠/丝亮芯）</summary>
        public static readonly Color GelBright = new(150, 208, 255);
        /// <summary>高光近白，仅限小点</summary>
        public static readonly Color GelSheen = new(224, 244, 255);

        //==== 拉丝 ====

        /// <summary>
        /// 凝胶拉丝，from→to 悬垂贝塞尔链，段用真 alpha 液滴贴图叠出有机鼓包（两端粗中间细）；
        /// slack 0..1 越大越松弛（垂弧大、丝粗），绷紧时细直欲断；丝上一颗滑珠往复游走
        /// 全程 AlphaBlend，Extra_98 为真 alpha 贴图，黑底遮罩类禁入
        /// </summary>
        public static void DrawStrand(SpriteBatch sb, Vector2 from, Vector2 to, float slack, float alpha, float time, float seed) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || alpha <= 0.02f) {
                return;
            }
            float dist = Vector2.Distance(from, to);
            if (dist < 6f) {
                return;
            }
            slack = MathHelper.Clamp(slack, 0f, 1f);
            int segs = Math.Clamp((int)(dist / 12f), 4, 30);
            //垂弧，松弛的凝胶被重力拽弯
            Vector2 mid = (from + to) * 0.5f + new Vector2(0f, dist * 0.22f * slack + 2f);
            float wEnd = 3.2f + 2.4f * slack;
            float wMid = 0.9f + 2.1f * slack;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 prev = from;
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs;
                Vector2 p = Vector2.Lerp(Vector2.Lerp(from, mid, t), Vector2.Lerp(mid, to, t), t);
                Vector2 seg = p - prev;
                float rot = seg.ToRotation() + MathHelper.PiOver2;
                //宽度剖面，端粗中细
                float endness = MathF.Pow(MathF.Abs(t - 0.5f) * 2f, 1.4f);
                float width = MathHelper.Lerp(wMid, wEnd, endness);
                Vector2 drawPos = (prev + p) * 0.5f - Main.screenPosition;
                //y 超采 1.55 让相邻液滴段的尖端互相咬合成连续丝
                Vector2 segScale = new(width / tex.Width * 3f, seg.Length() / tex.Height * 1.55f);
                //半透明体 + 细亮芯，湿润凝胶的双层
                sb.Draw(tex, drawPos, null, GelDeep * (0.6f * alpha), rot, origin, segScale, SpriteEffects.None, 0);
                sb.Draw(tex, drawPos, null, GelBright * (0.34f * alpha), rot, origin, segScale * new Vector2(0.4f, 1f), SpriteEffects.None, 0);
                prev = p;
            }
            //滑珠，凝胶小珠沿丝游走，卖粘度
            float bt = 0.5f + 0.36f * MathF.Sin(time * 1.35f + seed * 11f);
            Vector2 bead = Vector2.Lerp(Vector2.Lerp(from, mid, bt), Vector2.Lerp(mid, to, bt), bt);
            Vector2 beadPos = bead - Main.screenPosition;
            sb.Draw(tex, beadPos, null, GelBright * (0.6f * alpha), 0f, origin, 0.1f + 0.04f * slack, SpriteEffects.None, 0);
            sb.Draw(tex, beadPos + new Vector2(-1f, -1f), null, GelSheen * (0.45f * alpha), 0f, origin, 0.045f, SpriteEffects.None, 0);
        }

        /// <summary>拉丝绷断，断口两侧各留一截回缩断头 + 断点溅珠 + 高音短促湿响</summary>
        public static void SpawnStrandSnap(Vector2 a, Vector2 b, float slack) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishSlimeStrandSnap>(a, Vector2.Zero, GelBody, 1f)?.Configure(b, slack, 11);
            Vector2 mid = (a + b) * 0.5f + new Vector2(0f, Vector2.Distance(a, b) * 0.18f * slack);
            for (int i = 0; i < 3; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.2f) + new Vector2(0f, -0.8f);
                Droplet(mid + Main.rand.NextVector2Circular(4f, 4f), vel, Main.rand.NextFloat(0.5f, 0.8f));
            }
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.22f, Pitch = 0.62f, MaxInstances = 4 }, mid);
        }

        //==== 凝胶滴 ====

        /// <summary>单颗凝胶滴，受重力、随速度拉伸、落地压扁成溅斑残迹</summary>
        public static void Droplet(Vector2 pos, Vector2 vel, float scale, bool chunky = false) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishSlimeDroplet>(pos, vel, GelBody, scale)
                ?.Configure(Main.rand.Next(26, 40), chunky);
        }

        /// <summary>沿 dir 锥形迸出的凝胶滴组（附着拍击、伤害 tick 用）</summary>
        public static void GelBurst(Vector2 pos, Vector2 dir, int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(0.75f) * Main.rand.NextFloat(0.4f, 1f) * speed
                    + new Vector2(0f, -0.6f);
                Droplet(pos + Main.rand.NextVector2Circular(5f, 5f), vel, Main.rand.NextFloat(0.5f, 0.9f));
            }
        }

        /// <summary>
        /// 凝胶球爆裂，滴扇（上偏）+ 大块凝胶 + 深蓝扩散环 + 少量 Dust 填底
        /// mul 随领域层数微调规模
        /// </summary>
        public static void GelPop(Vector2 pos, float mul) {
            if (Main.dedServ) {
                return;
            }
            int drops = (int)(12 * mul);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = (-Vector2.UnitY).RotatedByRandom(1.9f) * Main.rand.NextFloat(2.5f, 8.5f);
                Droplet(pos + Main.rand.NextVector2Circular(8f, 8f), vel, Main.rand.NextFloat(0.55f, 0.95f));
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = (-Vector2.UnitY).RotatedByRandom(2.4f) * Main.rand.NextFloat(1.5f, 4.5f);
                Droplet(pos, vel, Main.rand.NextFloat(1.05f, 1.5f), true);
            }
            //扩散环压暗色，读作水波而非闪光
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, GelDeep * 0.7f, 0.12f)
                ?.Configure(Vector2.One, 0f, 0.5f * mul, 12);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.TintableDust, Main.rand.NextVector2Circular(5f, 5f)
                    , 120, GelBody, Main.rand.NextFloat(1f, 1.7f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 凝胶滴粒子，半透明蓝滴，受重力、随速度纵向拉伸、带内部高光点；<br/>
    /// 撞入实心物块后压扁成贴地溅斑并延寿缓退（活得比弹体久的残迹）；<br/>
    /// chunky 为大块凝胶，少拉伸、带反相 wobble
    /// </summary>
    internal class PRT_FishSlimeDroplet : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private bool chunky;
        private bool splat;
        private float gravity;
        private float wobbleAmp;
        private float wobblePhase;

        public PRT_FishSlimeDroplet Configure(int lifetime, bool bigChunk = false, float gravityPerFrame = 0.3f) {
            Lifetime = lifetime;
            chunky = bigChunk;
            gravity = gravityPerFrame;
            if (chunky) {
                wobbleAmp = 0.3f;
                wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            }
            return this;
        }

        public override void Reset() {
            base.Reset();
            chunky = false;
            splat = false;
            gravity = 0f;
            wobbleAmp = 0f;
            wobblePhase = 0f;
        }

        public override void AI() {
            if (splat) {
                Velocity = Vector2.Zero;
                return;
            }
            Velocity.X *= 0.985f;
            Velocity.Y += gravity;
            if (Velocity.Y > 13f) {
                Velocity.Y = 13f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            if (chunky) {
                wobblePhase += 0.5f;
                wobbleAmp *= 0.94f;
            }
            //落地
            if (Time > 4 && Collision.SolidCollision(Position - new Vector2(2f, 2f), 4, 4)) {
                splat = true;
                Velocity = Vector2.Zero;
                Lifetime = Time + (chunky ? 46 : 30);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float lifeT = LifetimeCompletion;
            if (splat) {
                //溅斑，压扁的半透明凝胶渍
                float fade = 1f - MathF.Pow(lifeT, 2f);
                Color aged = Color.Lerp(Color, FishSlimeVFX.GelDeep, 0.4f);
                spriteBatch.Draw(tex, pos, null, aged * (0.62f * fade), 0f, origin
                    , new Vector2(1.7f, 0.3f) * (0.5f * Scale), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos + new Vector2(0f, -1.5f), null, FishSlimeVFX.GelBright * (0.3f * fade), 0f, origin
                    , new Vector2(0.8f, 0.16f) * (0.5f * Scale), SpriteEffects.None, 0f);
                return false;
            }
            float stretch = chunky ? 0.22f : MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            float w = chunky ? wobbleAmp * MathF.Cos(wobblePhase) : 0f;
            Vector2 scale = new Vector2(0.4f * (1f - stretch * 0.3f) * (1f + w)
                , 0.58f * (1f + stretch * 1.6f) * (1f - w)) * Scale;
            float fade2 = 1f - MathF.Pow(lifeT, 2.2f);
            //半透明体 + 偏移内部高光点
            spriteBatch.Draw(tex, pos, null, Color * (0.85f * fade2), Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + new Vector2(-1.4f, -1.8f), null, FishSlimeVFX.GelBright * (0.5f * fade2)
                , Rotation, origin, scale * 0.42f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 拉丝绷断残段，端点冻结，两侧断头沿原垂弧回缩、端头甩尾带小珠，绷断三拍的后两拍
    /// </summary>
    internal class PRT_FishSlimeStrandSnap : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Vector2 endB;
        private float slack;

        public PRT_FishSlimeStrandSnap Configure(Vector2 otherEnd, float slackAmount, int lifetime) {
            endB = otherEnd;
            slack = MathHelper.Clamp(slackAmount, 0f, 1f);
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            endB = default;
            slack = 0f;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = LifetimeCompletion;
            float retract = 0.5f * (1f - MathF.Pow(t, 0.8f)); //先快后慢的回缩
            float alpha = (1f - t) * 0.9f;
            Vector2 mid = (Position + endB) * 0.5f + new Vector2(0f, Vector2.Distance(Position, endB) * 0.2f * slack);
            DrawStub(spriteBatch, Position, mid, retract, alpha, t, 0f);
            DrawStub(spriteBatch, endB, mid, retract, alpha, t, 3.1f);
            return false;
        }

        private void DrawStub(SpriteBatch sb, Vector2 root, Vector2 mid, float reach, float alpha, float t, float phase) {
            Vector2 along = mid - root;
            Vector2 tip = root + along * reach;
            //断头甩尾，垂直方向的衰减摆动
            Vector2 perp = along.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            tip += perp * (MathF.Sin(t * 14f + phase) * 7f * (1f - t));
            FishSlimeVFX.DrawStrand(sb, root, tip, slack * (1f - t * 0.5f), alpha, t * 0.4f, phase);
            //端头小珠，回缩中的凝胶聚在断口
            Texture2D drop = CWRAsset.Extra_98?.Value;
            if (drop != null) {
                sb.Draw(drop, tip - Main.screenPosition, null, FishSlimeVFX.GelBright * (0.65f * alpha), 0f
                    , drop.Size() * 0.5f, 0.09f, SpriteEffects.None, 0f);
            }
        }
    }
}
