using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 地面孢斑（场地实体，无直接伤害）。ai[0]=半宽 ai[1]=存续帧。
    /// 巨型真菌球抽打后的残留：成型 10 帧 → 存续期踩入滚动施加中毒 → 消散。
    /// 可见斑=判定斑（绘制与判定读同一几何）；斑与斑的强制间距由
    /// <see cref="PatchMinSpacingPx"/> 声明，生成方（藤鞭实体）真正读取
    /// </summary>
    internal class MushroomSporePatchZone : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>成型帧（踩入判定在成型完成后才开启）</summary>
        private const int FormFrames = 10;
        private const int FadeFrames = 14;
        /// <summary>判定高度（地表以上，像素）</summary>
        private const float ZoneHeightPx = 26f;
        /// <summary>踩入滚动施加的中毒时长（2 秒）</summary>
        private const int PatchPoisonFrames = 120;

        //==== 生成方读取的公平常量 ====
        /// <summary>孢斑半宽（生成方传入 ai[0] 的标准值）</summary>
        internal const float PatchHalfWidth = 46f;
        /// <summary>孢斑存续帧（生成方传入 ai[1] 的标准值）</summary>
        internal const int PatchActiveFrames = 90;
        /// <summary>斑与斑强制间距：新斑落点距既有斑小于此值则不生成（藤鞭侧读取）</summary>
        internal const float PatchMinSpacingPx = 150f;
        /// <summary>孢斑全局并发上限</summary>
        internal const int PatchCap = 6;

        private float HalfWidth => Projectile.ai[0];
        private int ActiveFrames => (int)Projectile.ai[1];
        private int TotalLife => FormFrames + ActiveFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯场地减益，恒无直接伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //存续期由 ai[1] 决定，各端以同一 ai 值展开时间轴
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.35f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            bool active = elapsed >= FormFrames && elapsed < FormFrames + ActiveFrames;

            //本机玩家判定：踩进斑内滚动施加中毒（本机 AddBuff 原生同步，受害端结算）
            if (active && !Main.dedServ) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.active && !localPlayer.dead && InZone(localPlayer.Hitbox)) {
                    localPlayer.AddBuff(BuffID.Poisoned, PatchPoisonFrames);
                }
            }

            //斑面孢雾上浮（≤1 粒/帧）
            if (active && !Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), -Main.rand.NextFloat(0f, 8f)),
                    DustID.GlowingMushroom, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)), 140, default, 0.85f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, MushroomSporeBoltProj.SporeBright.ToVector3() * 0.12f);
        }

        /// <summary>判定盒与绘制共用同一几何（可见斑=判定斑）</summary>
        private bool InZone(Rectangle hitbox) {
            Rectangle zone = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - ZoneHeightPx),
                (int)(HalfWidth * 2f), (int)(ZoneHeightPx + 8f));
            return zone.Intersects(hitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float spread = elapsed < FormFrames ? elapsed / (float)FormFrames : 1f;
            float alpha;
            if (elapsed >= FormFrames + ActiveFrames) {
                alpha = MathHelper.Clamp(1f - (elapsed - FormFrames - ActiveFrames) / (float)FadeFrames, 0f, 1f);
            }
            else {
                alpha = 0.55f + 0.45f * spread;
            }
            if (alpha <= 0.01f) {
                return false;
            }

            //斑体：暗青绿真 alpha 实底摊平（宽度与判定同一 HalfWidth）
            Texture2D sheet = CWRAsset.Extra_98.Value;
            float widthPx = HalfWidth * 2f * spread;
            Vector2 sheetScale = new Vector2(widthPx / sheet.Width, 12f / sheet.Height);
            Main.EntitySpriteDraw(sheet, Projectile.Center - new Vector2(0f, 4f) - Main.screenPosition,
                null, MushroomSporeBoltProj.SporeDeep * (0.85f * alpha), 0f,
                sheet.Size() / 2f, sheetScale, SpriteEffects.None, 0);

            //斑面亮泽（加色敷料）与确定性孢粒凸点
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float shimmer = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity);
            Color sheen = (MushroomSporeBoltProj.SporeBright with { A = 0 }) * (0.3f * alpha * shimmer);
            Main.EntitySpriteDraw(glow, Projectile.Center - new Vector2(0f, 6f) - Main.screenPosition,
                null, sheen, 0f, glow.Size() / 2f, new Vector2(widthPx / glow.Width * 1.1f, 0.3f), SpriteEffects.None, 0);
            for (int i = -2; i <= 2; i++) {
                float offsetX = i * HalfWidth * 0.4f;
                if (Math.Abs(offsetX) > HalfWidth * spread) {
                    continue;
                }
                float bob = MathF.Sin(Projectile.identity * 1.9f + i * 2.1f);
                Vector2 pos = Projectile.Center + new Vector2(offsetX, -4f - 2f * bob) - Main.screenPosition;
                Main.EntitySpriteDraw(glow, pos, null,
                    (MushroomSporeBoltProj.SporeBright with { A = 0 }) * (0.5f * alpha * shimmer),
                    0f, glow.Size() / 2f, 0.07f + 0.02f * bob, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 0f),
                    DustID.GlowingMushroom, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)), 120, default, 0.9f);
                dust.noGravity = true;
            }
        }
    }
}
