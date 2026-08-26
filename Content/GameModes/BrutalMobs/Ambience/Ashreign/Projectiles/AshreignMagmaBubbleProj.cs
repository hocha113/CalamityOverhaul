using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign.Projectiles
{
    /// <summary>
    /// 熔泡爆·鼓沸熔泡。ai[0]=体型（0.85~1.25，生成参数随包）。
    /// 生成位置即岩浆池液面锚点（喷发源头必须是液面，非崖壁非 NPC）。
    /// 预告 52 帧：泡体自液面鼓起膨大 + 表面由暗壳转红亮 + 咕噜声逐记升调
    /// → 提交帧爆裂，权威端溅出数颗岩浆珠呈抛物线 → 烟柱余韵 46 帧后消散。
    /// 泡本体全程无判定（hostile=false），杀伤全在岩浆珠上
    /// </summary>
    internal class AshreignMagmaBubbleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int SwellFrames = 52;
        /// <summary>余韵帧数（爆点烟雾）</summary>
        private const int AftermathFrames = 46;
        /// <summary>满泡半径（×体型）</summary>
        private const float BaseRadius = 26f;

        private float ScaleVar => Projectile.ai[0];
        private int TotalLife => SwellFrames + AftermathFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>鼓泡进度 0~1（前快后缓，膨大可读）</summary>
        private float Swell {
            get {
                float x = MathHelper.Clamp(Elapsed / (float)SwellFrames, 0f, 1f);
                return MathF.Pow(x, 0.8f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 200;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SwellFrames + AftermathFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            if (elapsed == SwellFrames) {
                Burst();
            }

            if (Main.dedServ) {
                return;
            }

            if (elapsed < SwellFrames) {
                SwellVisuals(elapsed);
            }
            else {
                AftermathVisuals(elapsed - SwellFrames);
            }
        }

        /// <summary>提交帧：权威端溅珠（临场重检总闸与城镇安宁，被拦则只剩演出哑火）+ 各端爆点演出</summary>
        private void Burst() {
            if (!VaultUtils.isClient
                && Ashreign.MechanicsAllowed && !Ashreign.TownCalm(Projectile.Center)) {
                int beadType = ModContent.ProjectileType<AshreignMagmaBeadProj>();
                int damage = Ashreign.BeadDamage();
                for (int i = 0; i < Ashreign.BeadCount; i++) {
                    float angle = -MathHelper.PiOver2
                        + MathHelper.Lerp(-0.55f, 0.55f, i / (float)(Ashreign.BeadCount - 1))
                        + Main.rand.NextFloat(-0.09f, 0.09f);
                    float speed = (6.4f + Main.rand.NextFloat(2.4f)) * (0.85f + 0.3f * ScaleVar);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center - new Vector2(0f, 6f), angle.ToRotationVector2() * speed,
                        beadType, damage, 1f, Main.myPlayer, Main.rand.NextFloat());
                }
            }

            if (Main.dedServ) {
                return;
            }
            //爆点：膜破声对 + 岩浆飞沫
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 5 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.45f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center,
                    DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(1.5f, 5f)) * ScaleVar,
                    60, default, Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>预告期：泡体膨大、表面转红亮、咕噜升调、缘口细沫（音画双通道）</summary>
        private void SwellVisuals(int elapsed) {
            //咕噜逐记升调：预告的听觉通道
            if (elapsed == 8 || elapsed == 22 || elapsed == 36 || elapsed == 46) {
                float progress = elapsed / (float)SwellFrames;
                SoundEngine.PlaySound(SoundID.Drip with {
                    Volume = 0.4f,
                    Pitch = -0.52f + 0.72f * progress,
                    MaxInstances = 4,
                }, Projectile.Center);
            }

            float swell = Swell;
            float radius = BaseRadius * ScaleVar * swell;

            //泡缘细沫（≤1 粒/3 帧）
            if (radius > 8f && Main.rand.NextBool(3)) {
                float ang = -Main.rand.NextFloat(0.35f, MathHelper.Pi - 0.35f);
                Vector2 rim = Projectile.Center + ang.ToRotationVector2() * radius * 0.9f;
                Dust dust = Dust.NewDustPerfect(rim, DustID.Torch,
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)), 110, default,
                    Main.rand.NextFloat(0.7f, 1.1f + 0.5f * swell));
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center - new Vector2(0f, radius * 0.4f),
                new Vector3(0.5f, 0.22f, 0.06f) * (0.3f + 0.7f * swell * swell) * ScaleVar);
        }

        /// <summary>余韵：爆点烟柱（灰烟上涌 + 零星火星），逐渐稀薄</summary>
        private void AftermathVisuals(int t) {
            if (Main.gamePaused || t > 40) {
                return;
            }
            float fade = 1f - t / 40f;
            if (t % 2 == 0) {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f) * ScaleVar, -2f),
                    DustID.Smoke,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.2f, 2.6f)),
                    (int)(90 + 60 * (1f - fade)), default, Main.rand.NextFloat(1f, 1.7f) * fade + 0.4f);
                smoke.noGravity = true;
            }
            if (t % 6 == 0 && Main.rand.NextBool()) {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2f)),
                    80, default, Main.rand.NextFloat(0.8f, 1.2f) * fade);
                ember.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            if (elapsed >= SwellFrames) {
                return false;//爆后只剩粒子余韵
            }
            float swell = Swell;
            float radius = BaseRadius * ScaleVar * swell;
            if (radius < 3f) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition - new Vector2(0f, radius * 0.22f);

            //熔壳穹体：Extra_98 真 alpha 压扁软体（暗层必须真 alpha，加色画不出壳）
            Texture2D crust = CWRAsset.Extra_98.Value;
            Vector2 crustOrigin = crust.Size() * 0.5f;
            //Extra_98 可见幅约 47px@scale1，宽轴铺满泡径，竖轴压 0.62 成穹
            Vector2 crustScale = new(radius * 2f / 47f, radius * 1.24f / 47f);
            Color crustColor = Ashreign.CrustDark * (0.55f + 0.35f * swell);
            Main.EntitySpriteDraw(crust, drawPos, null, crustColor, 0f, crustOrigin,
                crustScale, SpriteEffects.None, 0);

            //熔芯透亮：A=0 加色敷料，越临爆越红亮、脉动越急（预告的视觉通道）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly
                * MathHelper.Lerp(8f, 26f, swell) * 6.28f + Projectile.identity);
            Color core = Color.Lerp(new Color(150, 52, 22, 0), new Color(255, 150, 48, 0), swell * swell);
            Main.EntitySpriteDraw(glow, drawPos, null, core * ((0.3f + 0.6f * swell * swell) * pulse),
                0f, glow.Size() * 0.5f, radius * 2.4f / 52f, SpriteEffects.None, 0);

            //顶缘窄亮线：泡膜受光的一线（A=0，小而亮）
            Main.EntitySpriteDraw(glow, drawPos - new Vector2(0f, radius * 0.5f), null,
                new Color(255, 190, 90, 0) * (0.35f * swell * pulse), 0f, glow.Size() * 0.5f,
                new Vector2(radius * 1.4f / 52f, radius * 0.5f / 52f), SpriteEffects.None, 0);
            return false;
        }
    }
}
