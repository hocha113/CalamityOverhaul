using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 藤鞭挥扫实体。ai[0]=弧心朝向 ai[1]=打包(巨型|挥扫侧)，几何常量与预告体共用。
    /// 沿预告弧带一次抽打：伤害窗=挥扫窗（其后只留无判定残影）；
    /// 命中判定沿当前鞭身线段采样，与绘制同一几何。
    /// 巨型版收鞭帧在鞭梢下方地面留孢斑，间距与并发读 <see cref="MushroomSporePatchZone"/> 的具名常量
    /// </summary>
    internal class MushroomVineWhipProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>挥扫帧数（伤害窗）</summary>
        internal const int SweepFrames = 14;
        private const int FadeFrames = 8;
        /// <summary>鞭身命中采样点数与采样半宽</summary>
        private const int HitSamples = 6;
        private const int HitHalfPx = 16;
        /// <summary>鞭身绘制节数</summary>
        private const int SegCount = 9;
        /// <summary>鞭身弯曲滞后（弧度）：外节落后内节，读作鞭甩而非贴图扇形</summary>
        private const float BendLag = 0.38f;

        private float Aim => Projectile.ai[0];
        private int Packed => (int)Projectile.ai[1];
        private bool Giant => MushroomVineWhipOmen.UnpackGiant(Packed);
        private float Side => MushroomVineWhipOmen.UnpackSide(Packed);
        private float Reach => MushroomVineWhipOmen.ReachFor(Giant);
        private int TotalLife => SweepFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private float SweepT => MathHelper.Clamp(Elapsed / (float)SweepFrames, 0f, 1f);
        /// <summary>当前鞭梢角：从起扫侧到收扫侧（与预告虚影同一端点与方向）</summary>
        private float CurrentAngle => Aim + MathHelper.Lerp(-MushroomVineWhipOmen.ArcHalf,
            MushroomVineWhipOmen.ArcHalf, SweepT) * Side;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SweepFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>伤害窗=挥扫窗，残影期无判定</summary>
        public override bool? CanDamage() => Elapsed <= SweepFrames ? null : false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>命中判定：沿当前鞭身线段采样（与绘制同一几何，弧带即预告弧带）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Elapsed > SweepFrames) {
                return false;
            }
            Vector2 dir = CurrentAngle.ToRotationVector2();
            float reach = Reach;
            for (int k = 1; k <= HitSamples; k++) {
                Vector2 p = Projectile.Center + dir * (reach * k / HitSamples);
                Rectangle seg = new((int)p.X - HitHalfPx, (int)p.Y - HitHalfPx, HitHalfPx * 2, HitHalfPx * 2);
                if (seg.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void AI() {
            int elapsed = Elapsed;

            if (elapsed == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
            }

            //鞭梢拖尘（≤2 粒/帧）
            if (elapsed <= SweepFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 tip = Projectile.Center + CurrentAngle.ToRotationVector2() * Reach;
                Dust dust = Dust.NewDustPerfect(tip, DustID.GlowingMushroom,
                    (CurrentAngle + MathHelper.PiOver2 * Side).ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                    110, default, 1.1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center + CurrentAngle.ToRotationVector2() * (Reach * 0.7f),
                MushroomSporeBoltProj.SporeBright.ToVector3() * 0.2f);

            //收鞭帧：巨型版在鞭梢下方地面留孢斑（权威端决策）
            if (elapsed == SweepFrames && Giant && Main.netMode != NetmodeID.MultiplayerClient) {
                TrySpawnPatch();
            }
        }

        /// <summary>
        /// 孢斑生成：并发闸与斑间强制间距都是被本方法真正读取的具名常量
        /// （<see cref="MushroomSporePatchZone.PatchCap"/> / <see cref="MushroomSporePatchZone.PatchMinSpacingPx"/>）
        /// </summary>
        private void TrySpawnPatch() {
            int patchType = ModContent.ProjectileType<MushroomSporePatchZone>();
            if (MushroomBrutalNPC.CountActive(patchType) >= MushroomSporePatchZone.PatchCap) {
                return;
            }
            Vector2 tip = Projectile.Center + CurrentAngle.ToRotationVector2() * Reach;
            if (!TryFindGround(tip, out Vector2 basePos)) {
                return;
            }
            //斑与斑强制间距：离既有孢斑太近就不落新斑
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == patchType && proj.Distance(basePos) < MushroomSporePatchZone.PatchMinSpacingPx) {
                    return;
                }
            }
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), basePos, Vector2.Zero,
                patchType, 0, 0f, Main.myPlayer,
                MushroomSporePatchZone.PatchHalfWidth, MushroomSporePatchZone.PatchActiveFrames);
        }

        /// <summary>从鞭梢向下找可站立地表（带界检查的护栏循环，找不到视为悬空放弃）</summary>
        private static bool TryFindGround(Vector2 from, out Vector2 basePos) {
            basePos = default;
            Point start = from.ToTileCoordinates();
            for (int dy = 0; dy < 14; dy++) {
                int tileY = start.Y + dy;
                if (!WorldGen.InWorld(start.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(start.X, tileY)) {
                    basePos = new Vector2(start.X * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade = elapsed > SweepFrames
                ? MathHelper.Clamp(1f - (elapsed - SweepFrames) / (float)FadeFrames, 0f, 1f)
                : 1f;
            if (fade <= 0.01f) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float sweepT = SweepT;

            //残影拖尾：前几帧的鞭位以同材质低透重画（挥扫烟迹）
            for (int ghost = 3; ghost >= 1; ghost--) {
                float gt = MathHelper.Clamp((Elapsed - ghost * 2) / (float)SweepFrames, 0f, 1f);
                if (gt >= sweepT) {
                    continue;
                }
                float gAng = Aim + MathHelper.Lerp(-MushroomVineWhipOmen.ArcHalf,
                    MushroomVineWhipOmen.ArcHalf, gt) * Side;
                DrawLash(center, gAng, gt, fade * 0.22f * (1f - ghost * 0.25f));
            }
            //当前鞭身
            DrawLash(center, CurrentAngle, sweepT, fade);
            return false;
        }

        /// <summary>鞭身节链：外节带弯曲滞后（读作发力甩出），节珠双层孢材质</summary>
        private void DrawLash(Vector2 centerScreen, float tipAngle, float sweepT, float alpha) {
            float reach = Reach;
            //挥扫中段最直（发力峰），起收两端弯曲滞后最大
            float lag = BendLag * (1f - MathF.Sin(sweepT * MathHelper.Pi)) + 0.1f;
            for (int i = 1; i <= SegCount; i++) {
                float t = i / (float)SegCount;
                float segAng = tipAngle - lag * (1f - t) * Side;
                Vector2 pos = centerScreen + segAng.ToRotationVector2() * (reach * t);
                float thick = 0.3f - 0.12f * t;
                MushroomSporeBoltProj.DrawGlobAt(pos, segAng, alpha * (0.6f + 0.4f * t),
                    new Vector2(0.3f + 0.12f * t, thick + (Giant ? 0.05f : 0f)));
            }
        }
    }
}
