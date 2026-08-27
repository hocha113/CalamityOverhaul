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
    /// 样貌忠于原版：本体用原版波弹贴图原色直画，保住像素细节、配色与轮廓（契约4.2），
    /// 拖尾为同贴图同色残影（契约5）。相对原版只补两件事——
    /// 出膛短显形（<see cref="MaterializeTime"/> 帧快速淡入，无伤，弹体自带预告）、
    /// 点火后复合加速（越飞越急，绝不匀速）。
    /// 形态口径：全生成点统一点火提速（<see cref="LaunchBoost"/>，生成侧速度数值不改口径）
    /// </summary>
    internal class MLordBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>显形帧长：无伤、快速淡入并前漂（弹幕自带的短预告）</summary>
        internal const int MaterializeTime = 7;
        /// <summary>点火后复合加速上限（相对初速倍率）</summary>
        private const float AccelCap = 1.5f;
        /// <summary>点火速度统一增幅：所有生成点的波矢一并提速（生成侧速度数值不改口径；含移速翻倍 1.75→3.5）</summary>
        private const float LaunchBoost = 3.5f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float FullSpeed => ref Projectile.localAI[1];

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
                //显形相：淡入中前漂（可见的"将射未射"，不做原地凝滞）
                Projectile.velocity = dir * FullSpeed * 0.3f;
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
                Projectile.velocity *= 1.024f;
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

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.PhantasmalBolt);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.PhantasmalBolt].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //显形包络：快速淡入 + 由小到大（原版箭系淡入观感）
            float grow = MathHelper.Clamp(Timer / MaterializeTime, 0f, 1f);
            float scale = MathHelper.Lerp(0.5f, 1f, VaultUtils.EaseOutCubic(grow));
            float alpha = MathHelper.Lerp(0.2f, 1f, grow);

            //拖尾 = 本体原色残影（同贴图同色，契约5：横轴比≥0.5、真 alpha 遮挡）
            if (Timer > MaterializeTime) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, trailPos, null, Color.White * (0.42f * k * alpha),
                        Projectile.oldRot[i], origin, scale * MathHelper.Lerp(0.6f, 0.9f, k), SpriteEffects.None, 0);
                }
            }

            //本体：原版贴图原色直画，不改色不压形——样貌即原版幻影矢
            Main.EntitySpriteDraw(tex, pos, null, Color.White * alpha,
                Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            //幻影青薄晕（加色，速度越快越亮——能量弹自发光，不改剪影）
            float speedHeat = MathHelper.Clamp(Projectile.velocity.Length() / (FullSpeed * AccelCap + 0.01f), 0f, 1f);
            Main.EntitySpriteDraw(tex, pos, null,
                MLordDirector.Phantasmal with { A = 0 } * (0.2f + 0.25f * speedHeat) * alpha,
                Projectile.rotation, origin, scale * 1.05f, SpriteEffects.None, 0);
            return false;
        }
    }
}
