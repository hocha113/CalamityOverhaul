using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 验收铁镖：镖阵齐射的实体（替换原版毒镖——毒属性与绿尾迹不属于铸造场语言）。
    /// 原版镖形贴图重染铸铁 + 热尖，速度复利微加速（机加工出膛的"越飞越稳"），
    /// oldPos 同材质残影拖尾（Contract：拖尾=本体重画，非异色贴纸）。
    /// 撞墙叮一声迸铁屑即碎；不上毒 debuff。伤害随 spawn 包过线，AI 全本地确定性
    /// </summary>
    internal class OverseerDartBolt : OverseerModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float MaxSpeed = 13f;

        private float Seed => Projectile.identity * 0.7391f % 3.7f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            //机关口径：从镖口射出的算机关弹（验工印章的减伤/哑火按 proj.trap 判定，
            //承接原 PoisonDartTrap 的既有契约，不许悄悄改语义）
            Projectile.trap = true;
        }

        public override void AI() {
            //复利微加速：出膛后越飞越快，到匀速巡航（机加工的干脆，不许飘）
            if (Projectile.velocity.Length() < MaxSpeed) {
                Projectile.velocity *= 1.012f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //偶发热尖剥落的小铁屑（客户端表现）
            if (!Main.dedServ && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    Color.Lerp(FoundryOverseer.FurnaceOrange, Color.White, 0.3f),
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(true, Main.rand.Next(6, 10));
            }

            Lighting.AddLight(Projectile.Center, 0.16f, 0.09f, 0.03f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞墙叮一声即碎（默认 return true 走 Kill）
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_OverseerIronChip>(Projectile.Center,
                    -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.25f) + Main.rand.NextVector2Circular(1.4f, 1.4f),
                    FoundryOverseer.IronMul, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制：残影拖尾 → 暗缘 → 铁镖本体 → 热尖 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.PoisonDartTrap);
            Texture2D dartTex = TextureAssets.Projectile[ProjectileID.PoisonDartTrap]?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (dartTex == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = dartTex.Size() * 0.5f;
            //原版镖贴图头朝上，速度向绘制转角补 π/2
            float drawRot = Projectile.rotation + MathHelper.PiOver2;

            //残影拖尾：本体同材质重画，向尾衰减缩小（Contract 5）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 op = Projectile.oldPos[i];
                if (op == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 gp = op + Projectile.Size * 0.5f - Main.screenPosition;
                sb.Draw(dartTex, gp, null,
                    lightColor.MultiplyRGB(FoundryOverseer.IronMul) * (0.28f * k * k),
                    drawRot, origin, 0.9f + 0.1f * k, SpriteEffects.None, 0f);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //暗缘 + 铸铁重染本体
            sb.Draw(dartTex, pos, null, FoundryOverseer.IronDeep * 0.8f,
                drawRot, origin, 1.12f, SpriteEffects.None, 0f);
            sb.Draw(dartTex, pos, null, lightColor.MultiplyRGB(FoundryOverseer.IronMul),
                drawRot, origin, 1f, SpriteEffects.None, 0f);
            //热尖：镖头一粒炉橙（A=0 加色，预乘批技法）
            float flick = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Seed * 4f);
            Vector2 tip = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 7f;
            sb.Draw(glow, tip - Main.screenPosition, null,
                (FoundryOverseer.FurnaceOrange with { A = 0 }) * (0.55f * flick), 0f,
                glow.Size() * 0.5f, 5f * 2f / glow.Width, SpriteEffects.None, 0f);
            return false;
        }
    }
}
