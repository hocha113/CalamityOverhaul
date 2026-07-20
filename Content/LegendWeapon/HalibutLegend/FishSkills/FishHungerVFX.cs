using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 鱼中饿鬼共享演出协作类。<br/>
    /// 材质：血肉饿鬼（WoF 语系湿肉捕食者），暗红生肉 AlphaBlend 本体、光效极少；
    /// 三签名行为：触须蠕动（相位沿体节传递）、饥饿躁动（数量逼近上限时抖动渐强）、
    /// 扑咬定帧+血沫+撕扯拉锯。<br/>
    /// 与近邻区分：FishCrimsonTiger 是锐利冲刺线条、FishBloodyManowar 是软体伞膜、
    /// FishEaterofPlankton 是腐绿小虫群，这里是中大型不透明肉块
    /// </summary>
    internal static class FishHungerVFX
    {
        //==== 色彩脚本（生肉暗红族，比虎鱼猩红更暗更浊）====
        /// <summary>暗肉深红（剪影底/触须尖/外缘）</summary>
        public static readonly Color MeatDark = new(44, 8, 12);
        /// <summary>生肉红（主体）</summary>
        public static readonly Color MeatMid = new(132, 26, 32);
        /// <summary>筋膜粉（碎肉截面、腱膜）</summary>
        public static readonly Color SinewPale = new(206, 148, 138);
        /// <summary>湿光（非纯白，仅小面积瞬时镜面点）</summary>
        public static readonly Color WetGlint = new(240, 210, 202);

        /// <summary>生肉族随机取色（粒子用）</summary>
        public static Color Meat(float t) => Color.Lerp(MeatDark, MeatMid, t);

        /// <summary>带过冲缓出（血肉成形的「撑开」曲线）</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        /// <summary>显形收束：血珠向成形点倒吸 + 内收暗肉环，配合本体 easeOutBack 撑开构成入场拍</summary>
        public static void SummonConverge(Vector2 center, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            //内收环:出生大半径向中心缩拢, 读作血肉被攥聚
            PRTLoader.NewParticle<PRT_DWave>(center, Vector2.Zero, MeatDark * 0.75f, 0.30f * scale)
                ?.Configure(new Vector2(1f, 0.85f), Main.rand.NextFloat(MathHelper.TwoPi), 0.05f, 12);
            for (int i = 0; i < 12; i++) {
                Vector2 p = center + Main.rand.NextVector2CircularEdge(46f, 46f) * Main.rand.NextFloat(0.6f, 1f) * scale;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(p, (center - p) * Main.rand.NextFloat(0.10f, 0.16f)
                    , Meat(Main.rand.NextFloat(0.35f, 1f)), Main.rand.NextFloat(0.6f, 1f) * scale)
                    ?.Configure(Main.rand.Next(9, 14), 0.03f, 1f);
            }
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(14f, 14f) * scale
                    , DustID.Blood, Main.rand.NextVector2Circular(1.2f, 1.2f), 120, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
        }

        /// <summary>攻击蓄力吸气：血珠被倒吸进嘴，替代旧的均匀 Dust 环</summary>
        public static void ChargeSuction(Vector2 mouth) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                Vector2 p = mouth + Main.rand.NextVector2CircularEdge(34f, 34f) * Main.rand.NextFloat(0.7f, 1f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(p, (mouth - p) * Main.rand.NextFloat(0.12f, 0.18f)
                    , Meat(Main.rand.NextFloat(0.4f, 1f)), Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(8, 12), 0.02f, 1f);
            }
        }

        /// <summary>扑咬起跳：压扁暗环沿冲刺轴 + 蹬出的后抛血珠</summary>
        public static void LungeKick(Vector2 pos, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, MeatDark * 0.7f, 0.06f)
                ?.Configure(new Vector2(1f, 0.55f), dir.ToRotation(), 0.32f, 10);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = -dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(2.5f, 5.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Meat(Main.rand.NextFloat(0.4f, 1f))
                    , Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(14, 20), 0.24f);
            }
        }

        /// <summary>
        /// 咬合血沫：沿咬向的重力血珠锥 + 筋膜碎屑 + 碎肉块。ke 0..1 动能系数，量与初速∝动能
        /// </summary>
        public static void BiteSpray(Vector2 pos, Vector2 dir, float ke) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            int drops = (int)(6 + 5 * ke);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 8f + 4f * ke)
                    - Vector2.UnitY * Main.rand.NextFloat(1.4f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Meat(Main.rand.NextFloat(0.3f, 1f))
                    , Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(20, 32));
            }
            for (int i = 0; i < 2; i++) {
                //筋膜碎屑:更淡更轻, 短命
                Vector2 vel = dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 4.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, SinewPale, Main.rand.NextFloat(0.4f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 18), 0.12f, 0.94f);
            }
            int gobbets = 2 + (ke > 0.55f ? 1 : 0);
            for (int i = 0; i < gobbets; i++) {
                Vector2 vel = dir.RotatedByRandom(0.85f) * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * 1.6f;
                PRTLoader.NewParticle<PRT_FishHungerGobbet>(pos, vel, default, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(26, 38));
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood, dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(2f, 5f)
                    , 110, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
        }

        /// <summary>拉锯甩沫：撕扯期沿咬轴两侧甩出的少量血珠</summary>
        public static void TugShed(Vector2 pos, Vector2 axis) {
            if (Main.dedServ) {
                return;
            }
            Vector2 perp = axis.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = perp * Main.rand.NextFloat(-3.4f, 3.4f) - Vector2.UnitY * Main.rand.NextFloat(0.8f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Meat(Main.rand.NextFloat(0.4f, 1f))
                    , Main.rand.NextFloat(0.55f, 0.85f))?.Configure(Main.rand.Next(14, 22), 0.28f);
            }
        }

        /// <summary>垂涎：嘴角滴落单珠，饥饿越深滴得越勤（调用端控频率）</summary>
        public static void Drool(Vector2 mouth, Vector2 faceDir) {
            if (Main.dedServ) {
                return;
            }
            Vector2 pos = mouth + faceDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * 3f;
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, faceDir * 0.4f + Vector2.UnitY * 0.6f
                , Meat(Main.rand.NextFloat(0.45f, 0.95f)), Main.rand.NextFloat(0.4f, 0.6f))
                ?.Configure(Main.rand.Next(18, 26), 0.3f);
        }

        /// <summary>肉体塌散：本体死亡处炸开碎肉块与血珠，残迹活得比本体久（aftermath）</summary>
        public static void CollapseBurst(Vector2 pos, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, MeatDark * 0.7f, 0.05f * scale)
                ?.Configure(new Vector2(1f, 0.8f), Main.rand.NextFloat(MathHelper.TwoPi), 0.30f * scale, 11);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f) - Vector2.UnitY * 1.2f;
                PRTLoader.NewParticle<PRT_FishHungerGobbet>(pos + Main.rand.NextVector2Circular(6f, 6f), vel
                    , default, Main.rand.NextFloat(0.75f, 1.15f) * scale)?.Configure(Main.rand.Next(28, 42));
            }
            for (int i = 0; i < 9; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f) - Vector2.UnitY * Main.rand.NextFloat(1.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Meat(Main.rand.NextFloat(0.3f, 1f))
                    , Main.rand.NextFloat(0.8f, 1.3f) * scale)?.Configure(Main.rand.Next(20, 32));
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood, Main.rand.NextVector2Circular(3f, 3f)
                    , 110, default, Main.rand.NextFloat(1.1f, 1.8f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 饿鬼碎肉块：白像素三层矩形拼装的哑光肉屑（暗底/肉面/筋膜棱），乘环境光零发光；<br/>
    /// 受重力抛物翻滚、触地弹跳一次后落定收尾，年轻期带极小面积湿光点
    /// </summary>
    internal class PRT_FishHungerGobbet : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float gravity;
        private float spin;
        private bool bounced;
        private float tone;//块面深浅随机, 群体有层次

        public PRT_FishHungerGobbet Configure(int lifetime, float gravityStrength = 0.30f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            spin = 0f;
            bounced = false;
            tone = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.08f, 0.22f) * (Main.rand.NextBool() ? 1f : -1f);
            tone = Main.rand.NextFloat(0.72f, 1.05f);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
            }
            if (gravity == 0f) {
                gravity = 0.30f;
            }
        }

        public override void AI() {
            if (Velocity.Y < 15f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.985f;
            //翻滚速率挂水平速度:滚得快转得快
            Rotation += spin * (0.5f + Math.Abs(Velocity.X) * 0.06f);

            //飞行中甩落细血点
            if (!bounced && Main.rand.NextBool(8) && Velocity.LengthSquared() > 2f) {
                Dust d = Dust.NewDustPerfect(Position, DustID.Blood, Velocity * 0.2f, 130, default, 0.9f);
                d.noGravity = false;
            }

            if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(3f), 6, 6)) {
                if (!bounced) {
                    bounced = true;
                    Velocity.Y = -Math.Abs(Velocity.Y) * 0.36f;
                    Velocity.X *= 0.5f;
                    spin *= 1.4f;
                }
                else {
                    //二次触地落定:停移缓转, 提前进入收尾
                    Velocity *= 0.2f;
                    spin *= 0.4f;
                    if (Lifetime - Time > 8) {
                        Time = Lifetime - 8;
                    }
                }
            }

            Opacity = MathHelper.Clamp(Time / 2f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 3.2f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = new(0.5f, 0.5f);
            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            Color under = FishHungerVFX.MeatDark.MultiplyRGB(light);
            Color face = Color.Lerp(FishHungerVFX.MeatMid, FishHungerVFX.MeatDark, 0.22f).MultiplyRGB(light) * tone;
            Color sinew = FishHungerVFX.SinewPale.MultiplyRGB(light) * (tone * 0.9f);

            float w = 8f * Scale;
            float h = 5.6f * Scale;
            Vector2 rotDown = (Rotation + MathHelper.PiOver2).ToRotationVector2();

            //暗底错位在下:给块体厚度
            spriteBatch.Draw(pixel, pos + rotDown * (h * 0.22f), src, under * Opacity, Rotation
                , origin, new Vector2(w, h), SpriteEffects.None, 0f);
            //肉面
            spriteBatch.Draw(pixel, pos, src, face * Opacity, Rotation
                , origin, new Vector2(w * 0.92f, h * 0.85f), SpriteEffects.None, 0f);
            //筋膜截面小棱:偏转错位打破矩形轮廓
            spriteBatch.Draw(pixel, pos - rotDown * (h * 0.2f), src, sinew * (Opacity * 0.85f), Rotation + 0.55f
                , origin, new Vector2(w * 0.4f, h * 0.34f), SpriteEffects.None, 0f);
            //年轻期湿光点(加色, 极小面积)
            if (LifetimeCompletion < 0.35f) {
                spriteBatch.Draw(pixel, pos - rotDown * (h * 0.3f), src, (FishHungerVFX.WetGlint with { A = 0 }) * (Opacity * 0.4f)
                    , Rotation, origin, new Vector2(w * 0.2f, h * 0.16f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
