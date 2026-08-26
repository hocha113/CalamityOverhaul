using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 幻影波矢：替代原版幻影波弹（462）的基线弹。
    /// 原版件的三宗瑕疵在此修正——纯白刺眼（压回幻影星质配色，暗紫鞘保剪影）、
    /// 匀速直线飞行（显形→点火→复合加速的完整生命周期）、
    /// 出膛零预告（12 帧显形相无伤且几乎不动，弹体自带预告）。
    /// 本体用原版波弹贴图保住像素细节与轮廓（契约4.2），拖尾同材质重绘（契约5）。
    /// 形态口径：全生成点统一点火提速（<see cref="LaunchBoost"/>），
    /// 弹体沿飞行轴压短 + 拖尾缩短——快而短的矢，不拖长条
    /// </summary>
    internal class MLordBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>显形帧长：无伤、近乎静止、由暗到亮（弹幕自带预告）</summary>
        internal const int MaterializeTime = 12;
        /// <summary>点火后复合加速上限（相对初速倍率）</summary>
        private const float AccelCap = 1.4f;
        /// <summary>点火速度统一增幅：所有生成点的波矢一并提速（生成侧速度数值不改口径）</summary>
        private const float LaunchBoost = 1.4f;
        /// <summary>沿飞行轴压缩比：弹体与残影同步变短，横轴宽度不动（契约5横轴比不受影响）</summary>
        private const float LengthScale = 0.68f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float FullSpeed => ref Projectile.localAI[1];

        /// <summary>波矢暗鞘色（真 alpha 遮挡层，契约4.4：暗层禁走加色）</summary>
        private static readonly Color BoltDark = new(30, 18, 62);
        /// <summary>本体压光色调：把原版的纯白压进月白-幻影青之间</summary>
        private static readonly Color BoltBody = new(202, 236, 242);

        public override void SetStaticDefaults() {
            //拖尾缓存收短：矢更快也更短，不留长条尾迹
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //初帧记初速：显形相压速，点火帧恢复（各端确定性同步于生成包速度）
            if (Timer == 0f) {
                FullSpeed = Math.Max(Projectile.velocity.Length(), 1f) * LaunchBoost;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
            Timer++;

            Vector2 dir = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
            if (Timer <= MaterializeTime) {
                //显形相：原地凝聚，缓慢漂进（可见的"将射未射"）
                Projectile.velocity = dir * FullSpeed * 0.12f;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(14f, 30f);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, (Projectile.Center - pos) * 0.16f,
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, 10);
                }
            }
            else if (Timer == MaterializeTime + 1) {
                //点火：一帧回满初速
                Projectile.velocity = dir * FullSpeed;
            }
            else if (Projectile.velocity.Length() < FullSpeed * AccelCap) {
                //复合加速：越飞越急，绝不匀速
                Projectile.velocity *= 1.016f;
            }

            Lighting.AddLight(Projectile.Center, MLordDirector.Phantasmal.ToVector3() * 0.35f);

            //飞行星屑剥落（稀疏）
            if (!VaultUtils.isServer && Timer > MaterializeTime && Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    Projectile.Center, -Projectile.velocity * Main.rand.NextFloat(0.04f, 0.1f),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.DeepViolet, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        //显形相无伤：伤害窗与可见"点火"精确对齐（契约2.3）
        public override bool? CanDamage() => Timer > MaterializeTime + 2 ? null : false;

        public override void OnKill(int timeLeft) {
            //消亡余痕：星屑外溅，痕迹活得比弹体久
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.2f, 3.4f),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.MoonWhite, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(false, Main.rand.Next(14, 22));
            }
        }

        /// <summary>贴图纵轴=飞行轴：Y 向压缩得到短矢剪影，横轴不动</summary>
        private static Vector2 Squash(float s) => new(s, s * LengthScale);

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.PhantasmalBolt);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.PhantasmalBolt].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //显形包络：由小到大、由暗到亮
            float grow = MathHelper.Clamp(Timer / MaterializeTime, 0f, 1f);
            float scale = MathHelper.Lerp(0.35f, 1f, VaultUtils.EaseOutCubic(grow));
            float alpha = MathHelper.Lerp(0.25f, 1f, grow);

            //拖尾 = 本体同材质残影（契约5）：暗紫鞘随影衰减，横轴比恒 1
            if (Timer > MaterializeTime) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, trailPos, null, BoltDark * (0.55f * k),
                        Projectile.oldRot[i], origin, Squash(scale * MathHelper.Lerp(0.55f, 0.92f, k)), SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(tex, trailPos, null,
                        MLordDirector.Phantasmal with { A = 0 } * (0.34f * k),
                        Projectile.oldRot[i], origin, Squash(scale * MathHelper.Lerp(0.5f, 0.85f, k)), SpriteEffects.None, 0);
                }
            }

            //暗紫外鞘（真 alpha 剪影，亮天幕上立得住）
            Main.EntitySpriteDraw(tex, pos, null, BoltDark * (0.85f * alpha),
                Projectile.rotation, origin, Squash(scale * 1.22f), SpriteEffects.None, 0);
            //压光本体：原版像素细节保留，纯白收敛为月青
            Main.EntitySpriteDraw(tex, pos, null, BoltBody * alpha,
                Projectile.rotation, origin, Squash(scale), SpriteEffects.None, 0);
            //幻影青缘（加色，速度越快越亮）
            float speedHeat = MathHelper.Clamp(Projectile.velocity.Length() / (FullSpeed * AccelCap + 0.01f), 0f, 1f);
            Main.EntitySpriteDraw(tex, pos, null,
                MLordDirector.Phantasmal with { A = 0 } * (0.35f + 0.35f * speedHeat) * alpha,
                Projectile.rotation, origin, Squash(scale * 1.06f), SpriteEffects.None, 0);
            return false;
        }
    }
}
