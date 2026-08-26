using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sunkendune.Projectiles
{
    /// <summary>
    /// 「流沙陷窝」场地实体（地形驱动的控制区，恒无伤害）。ai[0]=半宽。
    /// 生成位置锁定：沙面浮现旋纹 + 沙流声渐强 66 帧（公平契约 ≥45）→ 窝心向下缓拽 + 减速 240 帧
    /// （不禁锢，跳跃/钩爪可脱）→ 沙面平复 40 帧。
    /// 可见区=判定区；拽握只点名本机玩家，移动学在 <see cref="SunkendunePlayer"/> 落地；
    /// Boss 在场时位移效果暂停（时间轴照走，视觉保留）
    /// </summary>
    internal class SunkenduneSinkPitProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>预告帧数（视觉+听觉双通道，公平契约 ≥45）</summary>
        private const int TelegraphFrames = 66;
        /// <summary>拽握存续帧</summary>
        private const int ActiveFrames = 240;
        /// <summary>平复帧数</summary>
        private const int CalmFrames = 40;
        /// <summary>判定高度（沙面以上，像素）</summary>
        private const float GripHeightPx = 46f;
        /// <summary>默认半宽（生成端传入 ai[0]，机制形状不随档位改变）</summary>
        internal const float DefaultHalfWidth = 100f;

        private float HalfWidth => Projectile.ai[0];
        private int TotalLife => TelegraphFrames + ActiveFrames + CalmFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>旋涡强度 0~1：预告缓起，拽握满值，平复归零</summary>
        private float SwirlPower {
            get {
                int elapsed = Elapsed;
                if (elapsed < TelegraphFrames) {
                    return 0.35f + 0.65f * (elapsed / (float)TelegraphFrames);
                }
                if (elapsed < TelegraphFrames + ActiveFrames) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - (elapsed - TelegraphFrames - ActiveFrames) / (float)CalmFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯控制场地，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + ActiveFrames + CalmFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            bool active = elapsed >= TelegraphFrames && elapsed < TelegraphFrames + ActiveFrames;
            //Boss 在场：位移效果暂停，视觉照常收尾
            bool harmAllowed = !CWRWorld.HasBoss;

            //旋纹相位累加（纯视觉，各端本地自走）：预告慢旋、拽握加速、平复滑停，无跳变
            Projectile.localAI[0] += active ? 0.1f : (elapsed < TelegraphFrames ? 0.05f : 0.02f * SwirlPower);

            //声音节拍：预告期沙流声渐强，拽握期低频翻搅
            if (!Main.dedServ) {
                if (elapsed == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.45f, Pitch = 0.1f, MaxInstances = 5 }, Projectile.Center);
                }
                else if (elapsed < TelegraphFrames && elapsed % 16 == 0) {
                    float progress = elapsed / (float)TelegraphFrames;
                    SoundEngine.PlaySound(SoundID.WormDig with {
                        Volume = 0.22f + 0.4f * progress, Pitch = 0.25f + 0.25f * progress, MaxInstances = 5
                    }, Projectile.Center);
                }
                else if (elapsed == TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.7f, Pitch = -0.15f, MaxInstances = 5 }, Projectile.Center);
                }
                else if (active && elapsed % 26 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.3f, Pitch = 0.45f, MaxInstances = 5 }, Projectile.Center);
                }
                else if (elapsed == TelegraphFrames + ActiveFrames) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //拽握：只点名本机玩家（移动是本机权威，镜像冰面区的本机判定）
            if (active && harmAllowed && !Main.dedServ) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.active && !localPlayer.dead && InZone(localPlayer.Hitbox)) {
                    localPlayer.GetModPlayer<SunkendunePlayer>().pitGrip = 2;
                    //足下翻沙
                    if (Main.rand.NextBool(4)) {
                        Dust kick = Dust.NewDustPerfect(localPlayer.Bottom + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f),
                            DustID.Sand, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 1.4f)),
                            110, default, Main.rand.NextFloat(0.8f, 1.2f));
                        kick.noGravity = true;
                    }
                }
            }

            if (Main.dedServ) {
                return;
            }

            //粉尘预算：预告 ≤1 粒/2 帧（切向旋纹），拽握 ≤1 粒/帧（向心收拢），平复零星沉降
            int calmStart = TelegraphFrames + ActiveFrames;
            if (elapsed < TelegraphFrames) {
                if (Main.rand.NextBool(2)) {
                    SpawnSwirlDust(0.5f + 0.5f * (elapsed / (float)TelegraphFrames), 1.1f);
                }
            }
            else if (active) {
                if (Main.rand.NextBool(2)) {
                    SpawnSwirlDust(1f, 1.9f);
                }
                //窝心偶发喷沙
                if (Main.rand.NextBool(9)) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                        DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(1f, 2.4f)), 100, default,
                        Main.rand.NextFloat(1f, 1.4f));
                    burst.noGravity = true;
                }
            }
            else if (elapsed < calmStart + CalmFrames && Main.rand.NextBool(4)) {
                Dust settle = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.7f, 0f),
                    DustID.Sand, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.6f)), 140, default, 0.7f);
                settle.noGravity = true;
            }
        }

        /// <summary>切向+向心的旋纹粉尘（旋涡的运动签名）</summary>
        private void SpawnSwirlDust(float spread, float speed) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = HalfWidth * spread * Main.rand.NextFloat(0.35f, 0.95f);
            Vector2 offset = new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.22f - 2f);
            //切向为主、向心为辅：读作旋转下陷而非爆开
            Vector2 tangent = new(-MathF.Sin(angle), MathF.Cos(angle) * 0.22f);
            Vector2 inward = -offset.SafeNormalize(Vector2.Zero) * 0.4f;
            Dust dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Sand,
                tangent * speed + inward, 110, default, Main.rand.NextFloat(0.8f, 1.15f));
            dust.noGravity = true;
        }

        /// <summary>判定盒与绘制共用同一几何（可见区=判定区）</summary>
        private bool InZone(Rectangle hitbox) {
            Rectangle zone = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - GripHeightPx),
                (int)(HalfWidth * 2f), (int)(GripHeightPx + 10f));
            return zone.Intersects(hitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float swirl = SwirlPower;
            if (swirl <= 0.02f) {
                return false;
            }
            float spread = elapsed < TelegraphFrames ? elapsed / (float)TelegraphFrames : 1f;
            bool active = elapsed >= TelegraphFrames && elapsed < TelegraphFrames + ActiveFrames;

            //窝口沉陷暗带（真 alpha 暗形，宽度与判定同一 HalfWidth）
            Texture2D sheet = CWRAsset.Extra_98.Value;
            float mouthWidth = HalfWidth * 2f * spread;
            Vector2 mouthScale = new(mouthWidth / sheet.Width, 12f / sheet.Height);
            Color mouth = new Color(58, 42, 24) * (0.5f * swirl);
            Main.EntitySpriteDraw(sheet, Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition,
                null, mouth, 0f, sheet.Size() / 2f, mouthScale, SpriteEffects.None, 0);

            //窝心更深的暗涡（拽握期加深，读作"往下走"）
            float coreDepth = active ? 0.6f : 0.35f;
            Color core = new Color(30, 22, 12) * (coreDepth * swirl);
            Main.EntitySpriteDraw(sheet, Projectile.Center + new Vector2(0f, 3f) - Main.screenPosition,
                null, core, 0f, sheet.Size() / 2f,
                new Vector2(mouthWidth * 0.45f / sheet.Width, 16f / sheet.Height), SpriteEffects.None, 0);

            //旋转沙团（实体感锚点）：沿扁椭圆轨道向心缓收，相位由 AI 累加保证转速切换无跳变
            Texture2D clump = TextureAssets.Projectile[Type].Value;
            Vector2 clumpOrig = clump.Size() / 2f;
            float phase = Projectile.localAI[0];
            for (int i = 0; i < 4; i++) {
                float angle = phase + i * MathHelper.PiOver2 + Projectile.identity;
                float radius = HalfWidth * spread * (0.8f - 0.12f * MathF.Sin(phase * 0.4f + i * 1.7f));
                Vector2 pos = Projectile.Center
                    + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.2f - 3f)
                    - Main.screenPosition;
                Color clumpColor = Color.Lerp(lightColor, new Color(214, 182, 112), 0.45f) * (0.85f * swirl);
                Main.EntitySpriteDraw(clump, pos, null, clumpColor, angle * 2.4f, clumpOrig,
                    0.55f, SpriteEffects.None, 0);
            }

            //预告警示光斑（家族警示语汇：暖色 A=0 脉动，拽握期收暗）
            float warn = elapsed < TelegraphFrames ? spread : (active ? 0.35f : 0f);
            if (warn > 0.02f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);
                Color warnColor = new Color(255, 198, 108, 0) * (0.5f * warn * pulse);
                Main.EntitySpriteDraw(glow, Projectile.Center + new Vector2(0f, 1f) - Main.screenPosition,
                    null, warnColor, 0f, glow.Size() / 2f,
                    new Vector2(HalfWidth * 2.4f * spread / glow.Width, 0.4f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //沙面平复的最后一口气
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.6f, 0f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.8f)),
                    130, default, Main.rand.NextFloat(0.7f, 1f));
                dust.noGravity = true;
            }
        }
    }
}
