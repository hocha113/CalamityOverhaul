using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaBloomSerpent
{
    /// <summary>
    /// 鬼奴荒花沙蟒的水化钉刺：红花节涟漪齐射的仙人掌刺，泡透血水后
    /// 飞行中沿途甩细血珠，后段微坠走弧。命中/贴壁溅一蓬血花碎瓣。
    /// 弹体只在 owner 端生成，贴图借 boss 钉刺素材、血水衣着色
    /// </summary>
    internal class KikasaBloomNeedle : ModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/Needle";

        /// <summary>贴图尖端朝向（素材尖朝左下，与 boss 钉刺同约定）</summary>
        private const float AuthoredTipAngle = 2.356f;

        /// <summary>出手多少帧后开始吃重力：钉刺有重量，不是激光</summary>
        private const int GravityDelay = 18;

        private ref float Life => ref Projectile.localAI[0];

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        /// <summary>出生 3 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
            Projectile.scale = 1.35f;
        }

        public override void AI() {
            Life++;
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + 0.09f, -20f, 12f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() - AuthoredTipAngle;

            //沿途甩细血珠：水化钉刺在飞行里持续淌
            if (!Main.dedServ && Life % 4 == 1) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.1f, 0.5f),
                    -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * 0.55f, Main.rand.NextFloat(0.22f, 0.4f))?.Configure(Main.rand.Next(10, 18), 0.2f);
            }
            Lighting.AddLight(Projectile.Center, 0.14f * VisualFade, 0.04f * VisualFade, 0.05f * VisualFade);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //血花碎裂：小血珠扇 + 一两枚绯瓣
            Vector2 normal = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    normal.RotatedByRandom(1f) * Main.rand.NextFloat(1.2f, 3.6f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }
            if (Main.rand.NextBool(2)) {
                CalamityOverhaul.Content.NPCs.BloomsandSerpents.BssVfx.PetalDrift(
                    Projectile.Center, normal * Main.rand.NextFloat(0.8f, 1.6f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.25f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float fade = VisualFade;

            //同素材递缩残影，血色渐隐
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, BloodDeep * (0.3f * t * fade), Projectile.rotation,
                    origin, Projectile.scale * (0.75f + 0.25f * t), SpriteEffects.None, 0);
            }

            //本体：血水浸染主体 + 湿面亮芯
            Vector2 center = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, center, null,
                Color.Lerp(lightColor, BloodMain, 0.55f) * fade, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, center, null,
                (BloodBright with { A = 0 }) * (0.35f * fade), Projectile.rotation,
                origin, Projectile.scale * 0.8f, SpriteEffects.None, 0);
            return false;
        }
    }
}
