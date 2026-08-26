using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Wastes.Projectiles
{
    /// <summary>
    /// 冰面打滑区（场地实体，无伤害的控制区）。ai[0]=半宽 ai[1]=存续帧。
    /// 生成位置锁定：霜纹凝结预告 36 帧 → 成型存续 → 消融。
    /// 区内玩家滚动施加原版寒颤（可见区=判定区，绘制与判定读同一几何）；
    /// 雪原兽类踏区获得滑行加速（环境武器化的双刃），速度脉冲只在权威端注入
    /// </summary>
    internal class WastesIceSlickZone : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        /// <summary>预告帧数（公平契约 ≥30，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 36;
        private const int FadeFrames = 20;
        /// <summary>判定高度（地表以上，像素）</summary>
        private const float ZoneHeightPx = 30f;
        /// <summary>滚动施加的寒颤时长</summary>
        private const int ChillFrames = 40;
        /// <summary>兽类滑行脉冲节拍（帧）</summary>
        private const int SlideInterval = 10;
        /// <summary>每次脉冲的横向附加速度</summary>
        private const float SlideAccel = 0.5f;
        /// <summary>滑行速度封顶</summary>
        private const float SlideMax = 9.5f;
        /// <summary>冰晶凸起间距（像素，绘制用）</summary>
        private const float NubSpacing = 36f;

        private float HalfWidth => Projectile.ai[0];
        private int ActiveFrames => (int)Projectile.ai[1];
        private int TotalLife => TelegraphFrames + ActiveFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯控制场地，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //存续期由 ai[1] 决定，两端以同一 ai 值各自展开时间轴
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            bool active = elapsed >= TelegraphFrames && elapsed < TelegraphFrames + ActiveFrames;

            if (elapsed == TelegraphFrames && !Main.dedServ) {
                //成型帧
                SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.8f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 0f),
                        DustID.Ice, new Vector2(0f, -Main.rand.NextFloat(1f, 3f)), 90, default, Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                }
            }

            //预告期霜纹蔓延粉尘（≤1 粒/帧）
            if (elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                float spread = elapsed / (float)TelegraphFrames;
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * spread, 2f),
                    DustID.Frost, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)), 130, default, 0.9f);
                dust.noGravity = true;
            }

            if (!active) {
                return;
            }

            //本机玩家判定：站进区内滚动施加寒颤（本机 AddBuff 原生同步）
            if (!Main.dedServ) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.active && !localPlayer.dead && InZone(localPlayer.Hitbox)) {
                    localPlayer.AddBuff(BuffID.Chilled, ChillFrames);
                }
            }

            //雪原兽类滑行加速：速度脉冲只在权威端注入，仅脉冲帧置 netUpdate
            if (Main.netMode != NetmodeID.MultiplayerClient && elapsed % SlideInterval == 0) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!WastesBrutalNPC.SlideTypes.Contains(npc.type) || npc.SpawnedFromStatue) {
                        continue;
                    }
                    if (Math.Abs(npc.velocity.X) < 0.8f || !InZone(npc.Hitbox)) {
                        continue;
                    }
                    float boosted = MathHelper.Clamp(npc.velocity.X * 1.05f + Math.Sign(npc.velocity.X) * SlideAccel,
                        -SlideMax, SlideMax);
                    if (boosted != npc.velocity.X) {
                        npc.velocity.X = boosted;
                        npc.netUpdate = true;
                    }
                }
            }

            //区内冷雾（≤1 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), -Main.rand.NextFloat(0f, 10f)),
                    DustID.Frost, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.3f), 150, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.12f, 0.2f, 0.3f));
        }

        /// <summary>判定盒与绘制共用同一几何（可见区=判定区）</summary>
        private bool InZone(Rectangle hitbox) {
            Rectangle zone = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - ZoneHeightPx),
                (int)(HalfWidth * 2f), (int)(ZoneHeightPx + 8f));
            return zone.Intersects(hitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            //预告期从中心向两端蔓延，成型后满幅，消融期整体退淡
            float spread = elapsed < TelegraphFrames ? elapsed / (float)TelegraphFrames : 1f;
            float alpha;
            if (elapsed >= TelegraphFrames + ActiveFrames) {
                alpha = MathHelper.Clamp(1f - (elapsed - TelegraphFrames - ActiveFrames) / (float)FadeFrames, 0f, 1f);
            }
            else if (elapsed < TelegraphFrames) {
                alpha = 0.45f + 0.25f * spread;
            }
            else {
                alpha = 1f;
            }
            if (alpha <= 0.01f) {
                return false;
            }

            //冰面主体（真 alpha 实体层，宽度与判定同一 HalfWidth）
            Texture2D sheet = CWRAsset.Extra_98.Value;
            float widthPx = HalfWidth * 2f * spread;
            Vector2 sheetScale = new Vector2(widthPx / sheet.Width, 14f / sheet.Height);
            Color iceBody = new Color(190, 226, 248) * (0.8f * alpha);
            Main.EntitySpriteDraw(sheet, Projectile.Center - new Vector2(0f, 4f) - Main.screenPosition,
                null, iceBody, 0f, sheet.Size() / 2f, sheetScale, SpriteEffects.None, 0);

            //冰晶凸起（实体感锚点，确定性倾角）
            Texture2D crystal = TextureAssets.Projectile[Type].Value;
            int nubs = (int)(HalfWidth / NubSpacing);
            for (int i = -nubs; i <= nubs; i++) {
                float offsetX = i * NubSpacing;
                if (Math.Abs(offsetX) > HalfWidth * spread) {
                    continue;
                }
                float lean = MathF.Sin(Projectile.identity * 1.7f + i * 2.3f) * 0.5f;
                Vector2 pos = Projectile.Center + new Vector2(offsetX, -2f) - Main.screenPosition;
                Color nub = Color.Lerp(lightColor, new Color(200, 236, 255), 0.6f) * (0.9f * alpha);
                Main.EntitySpriteDraw(crystal, pos, null, nub, lean,
                    new Vector2(crystal.Width / 2f, crystal.Height), 0.7f, SpriteEffects.None, 0);
            }

            //冷光泽（加色敷料）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float shimmer = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.identity);
            Color sheen = new Color(150, 220, 255, 0) * (0.3f * alpha * shimmer);
            Main.EntitySpriteDraw(glow, Projectile.Center - new Vector2(0f, 6f) - Main.screenPosition,
                null, sheen, 0f, glow.Size() / 2f, new Vector2(widthPx / glow.Width * 1.1f, 0.35f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 0f),
                    DustID.Ice, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), 120, default, 0.9f);
                dust.noGravity = true;
            }
        }
    }
}
