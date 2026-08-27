using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 星陨彗星：引力弯折的坠落轨迹 + 星尘拖尾。
    /// ai[0]=横向弯折加速度，ai[1]=1 落地生星火，ai[2]=引爆深度 Y（世界坐标）
    /// </summary>
    internal class MLordCometProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>移速统一倍率：初速×2、加速度×4、封顶×2——同一条弹道加倍速穿行，
        /// 弧形与落点不变，走完只用一半时间（生成侧速度数值不改口径）</summary>
        private const float SpeedBoost = 2f;

        /// <summary>体积统一倍率：本体/拖尾/星芯画幅与碰撞箱一并缩至原大小 75%（原型遮屏过大）</summary>
        private const float SizeScale = 0.75f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = (int)(30 * SizeScale);
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            //点火倍率：首帧按生成包初速翻倍（各端确定性同步）
            if (Timer == 1f) {
                Projectile.velocity *= SpeedBoost;
            }

            //重力 + 横向弯折（天体弧线轨迹，绝不匀速直线）——加速度按倍率平方缩放，弧形不变
            Projectile.velocity.Y += 0.11f * SpeedBoost * SpeedBoost;
            Projectile.velocity.X += Projectile.ai[0] * SpeedBoost * SpeedBoost;
            if (Projectile.velocity.Length() > 23f * SpeedBoost) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * (23f * SpeedBoost);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //出生短程后允许撞地（帧数随倍率缩短，放行深度不变）
            if (Timer > 24f / SpeedBoost) {
                Projectile.tileCollide = true;
            }
            //到达引爆深度
            if (Projectile.ai[2] > 0f && Projectile.Center.Y >= Projectile.ai[2]) {
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, MLordDirector.Phantasmal.ToVector3() * 0.8f);

            if (VaultUtils.isServer) {
                return;
            }
            //星尘剥落 ∝ 速度
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * Main.rand.NextFloat(0.05f, 0.16f),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.MoonWhite, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(14, 26));
            }
        }

        public override void OnKill(int timeLeft) {
            //落点爆裂
            if (!VaultUtils.isServer) {
                MLordScreenFX.StarBurst(Projectile.Center, 1.05f, 16);
                MLordScreenFX.Punch(Projectile.Center, 4.5f, 9, Projectile.velocity);
                SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.75f, Pitch = -0.35f, MaxInstances = 5 }, Projectile.Center);
            }
            //星火余留（服务端裁定）
            if (!VaultUtils.isClient && Projectile.ai[1] == 1f) {
                Vector2 ground = MLordScreenFX.FindGroundBelow(Projectile.Center);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), ground - new Vector2(0f, 18f), Vector2.Zero,
                    ModContent.ProjectileType<MLordStarfireProj>(), Projectile.damage * 2 / 3, 0f, Main.myPlayer);
            }
        }

        /// <summary>彗核暗鞘色（真 alpha 遮挡层，契约4.4：暗层禁走加色）</summary>
        private static readonly Color CometDark = new(26, 16, 58);

        /// <summary>彗星本体双层：暗紫外鞘（真 alpha 剪影）+ 幻影青热芯（加色）</summary>
        private static void DrawCometBody(Texture2D glow, Texture2D star, Vector2 screenPos,
            float rotation, Vector2 bodyScale, float alpha, float starRot) {
            Main.EntitySpriteDraw(glow, screenPos, null, CometDark * (0.9f * alpha),
                rotation, glow.Size() / 2f, bodyScale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.Phantasmal with { A = 0 } * alpha,
                rotation, glow.Size() / 2f, bodyScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenPos, null, MLordDirector.MoonWhite with { A = 0 } * (0.9f * alpha),
                starRot, star.Size() / 2f, 0.3f * SizeScale * alpha, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return false;
            }

            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.2f, 0.8f);
            Vector2 bodyScale = new(0.4f * SizeScale * (1f + stretch), 0.4f * SizeScale * (1f - stretch * 0.35f));
            float flicker = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.whoAmI);

            //拖尾 = 本体同材质重绘（契约5）：双层彗体沿轨迹衰减，横轴比 0.85→0.5
            for (int i = Projectile.oldPos.Length - 1; i >= 2; i -= 2) {
                //trail 缓存未填满前是零向量，画出去会闪到世界原点
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                DrawCometBody(glow, star, pos, Projectile.rotation,
                    bodyScale * MathHelper.Lerp(0.5f, 0.85f, k), 0.12f + 0.4f * k,
                    Projectile.rotation * 0.4f);
            }

            //本体
            DrawCometBody(glow, star, Projectile.Center - Main.screenPosition, Projectile.rotation,
                bodyScale, flicker, Projectile.rotation * 0.4f);
            return false;
        }
    }
}
