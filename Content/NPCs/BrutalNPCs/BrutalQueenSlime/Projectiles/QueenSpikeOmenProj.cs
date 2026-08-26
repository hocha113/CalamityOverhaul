using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>
    /// 尖刺预告实体(预告即承诺，生成后不再改向)：<br/>
    /// ai[0]=模式(0竖直车道 1绽放环) ai[1]=长度/半径 ai[2]=打包(车道:寿命；环:缺口中心角*100+寿命)
    /// </summary>
    internal class QueenSpikeOmenProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal enum OmenMode : int
        {
            /// <summary>竖直车道：自锚点向下的虚线走廊</summary>
            Lane = 0,
            /// <summary>绽放环：环点阵+可见缺口(缺口=安全角，与发射循环同一常量)</summary>
            BurstRing = 1,
        }

        /// <summary>绽放环缺口半角(弧度)——发射循环与本预告共读，缺口可见即安全</summary>
        internal const float BurstGapHalfAngle = 0.55f;

        /// <summary>打包环模式 ai[2]：缺口中心角(0~2π 移到正区间)*100 + 寿命</summary>
        internal static float PackRing(float gapCenter, int life) {
            float norm = ((gapCenter % MathHelper.TwoPi) + MathHelper.TwoPi) % MathHelper.TwoPi;
            return (float)Math.Round(norm * 100f) * 1000f + life;
        }

        private OmenMode Mode => (OmenMode)(int)Projectile.ai[0];
        private float Span => Projectile.ai[1];
        private int Life => (int)Projectile.ai[2] % 1000;
        private float GapCenter => (int)Projectile.ai[2] / 1000 * 0.01f;
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.42f, Pitch = 0.6f, MaxInstances = 5 }, Projectile.Center);
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;
            if (Timer >= Life) {
                Projectile.Kill();
            }
        }

        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>预告绘制：临期脉冲加速(可读性阀)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float lifeP = MathHelper.Clamp(Timer / (float)Math.Max(Life, 1), 0f, 1f);
            //淡入淡出包络
            float env = Math.Min(MathHelper.Clamp(Timer / 8f, 0f, 1f), MathHelper.Clamp((Life - Timer) / 6f, 0f, 1f));
            //临期脉冲：越接近发射闪得越急
            float pulse = 0.65f + 0.35f * (float)Math.Sin(Timer * (0.25f + lifeP * 0.55f));

            if (Mode == OmenMode.Lane) {
                DrawLane(spriteBatch, env, pulse, lifeP);
            }
            else {
                DrawBurstRing(spriteBatch, env, pulse);
            }
        }

        /// <summary>竖直车道：分段虚线(两端渐隐收口)+底部落点亮斑</summary>
        private void DrawLane(SpriteBatch sb, float env, float pulse, float lifeP) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color hue = QueenMotion.PrismHue(Projectile.whoAmI * 0.19f % 1f);
            Vector2 top = Projectile.Center - Main.screenPosition;
            const int segs = 13;
            for (int i = 0; i < segs; i++) {
                float along = (i + 0.5f) / segs;
                //两端包络收口，禁平切
                float endCap = (float)Math.Sin(along * MathHelper.Pi);
                //虚线滚动(向下行进，读出落向)
                float dash = 0.55f + 0.45f * (float)Math.Sin(along * 26f - Timer * 0.32f);
                Vector2 pos = top + new Vector2(0f, Span * along);
                sb.Draw(glow, pos, null, hue * (0.4f * env * pulse * endCap * dash), 0f,
                    glow.Size() / 2f, new Vector2(0.16f, 0.5f), SpriteEffects.None, 0f);
            }
            //落点亮斑(贴地扁光，随临期增大)
            Vector2 ground = top + new Vector2(0f, Span);
            sb.Draw(glow, ground, null, hue * (0.6f * env * pulse), 0f, glow.Size() / 2f,
                new Vector2(0.9f + lifeP * 0.5f, 0.22f), SpriteEffects.None, 0f);
            sb.Draw(glow, ground, null, Color.White * (0.32f * env * pulse), 0f, glow.Size() / 2f,
                new Vector2(0.45f, 0.12f), SpriteEffects.None, 0f);
        }

        /// <summary>绽放环：点阵环+缺口(跳过缺口内的点——缺口可见即安全角)</summary>
        private void DrawBurstRing(SpriteBatch sb, float env, float pulse) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color hue = QueenMotion.PrismHue(Projectile.whoAmI * 0.19f % 1f);
            Vector2 center = Projectile.Center - Main.screenPosition;
            const int dots = 30;
            for (int i = 0; i < dots; i++) {
                float ang = MathHelper.TwoPi * i / dots + Timer * 0.012f;
                //缺口判定与发射循环同一常量
                float delta = MathHelper.WrapAngle(ang - GapCenter);
                if (Math.Abs(delta) < BurstGapHalfAngle) {
                    continue;
                }
                Vector2 pos = center + ang.ToRotationVector2() * Span;
                sb.Draw(glow, pos, null, hue * (0.5f * env * pulse), 0f, glow.Size() / 2f, 0.17f, SpriteEffects.None, 0f);
            }
            //缺口两肩亮点(把"门框"点出来)
            for (int s = -1; s <= 1; s += 2) {
                float ang = GapCenter + s * BurstGapHalfAngle;
                Vector2 pos = center + ang.ToRotationVector2() * Span;
                sb.Draw(glow, pos, null, Color.White * (0.65f * env * pulse), 0f, glow.Size() / 2f, 0.26f, SpriteEffects.None, 0f);
            }
        }
    }
}
