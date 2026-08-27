using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil.Projectiles
{
    /// <summary>
    /// 沙龙卷阵的喷发沙涌柱。ai[0]=行进延迟帧（外推沙浪）ai[1]=来源NPC+1|类型&lt;&lt;8。
    /// 生成位置即锁定柱位（由阵列 omen 的 ≥40 帧仪式统一预告）：延迟蛰伏 → 自带 10 帧涌沙
    /// 二次预告 → 喷发 20 帧（判定窗=可见喷发窗）→ 消散。喷发前来源死亡则取消（反制有效），
    /// 已开始喷发则不回收（镜像地涌口径）
    /// </summary>
    internal class SandveilSurgeColumnProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>柱位自带的涌沙二次预告帧（阵列仪式之外的贴柱读秒）</summary>
        private const int BoilFrames = 10;
        /// <summary>喷发帧数（判定窗=可见喷发窗）</summary>
        private const int EruptFrames = 20;
        private const int FadeFrames = 10;
        /// <summary>柱高与柱半宽</summary>
        private const float ColumnHeight = 170f;
        private const float ColumnHalfWidth = 20f;
        /// <summary>喷发爆出用时（帧）</summary>
        private const int EruptRiseFrames = 6;

        //色板参考 DuneStorm 沙漠色板，数值抄色、代码独立
        private static readonly Color SandDeepDim = new(112, 88, 46);
        private static readonly Color SandBrightLerp = new(232, 202, 126);
        private static readonly Color CrownGlow = new(255, 224, 150, 0);

        private int Delay => Math.Max(0, (int)Projectile.ai[0]);
        private int SrcPacked => (int)Projectile.ai[1];
        private int SourceIndex => (SrcPacked & 255) - 1;
        private int SourceType => (SrcPacked >> 8) & 0xFFF;
        private int TotalLife => Delay + BoilFrames + EruptFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        /// <summary>喷发起始帧（相对生成）</summary>
        private int EruptAt => Delay + BoilFrames;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>破土程度 0~1（快速爆出）</summary>
        private float EruptProgress {
            get {
                int t = Elapsed - EruptAt;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= EruptRiseFrames) {
                    return 1f;
                }
                float x = t / (float)EruptRiseFrames;
                return 1f - (1f - x) * (1f - x) * (1f - x);
            }
        }

        /// <summary>退场收缩 1→0</summary>
        private float RetractFactor {
            get {
                int t = Elapsed - EruptAt - EruptFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//喷发窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                //总寿命从同步的 ai[0] 各端确定性展开（行进延迟随距离递增=外推沙浪）
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
            }
            int elapsed = Elapsed;

            //喷发前来源校验：沙元素死亡/槽位复用则取消；已开始喷发则不回收（镜像地涌）
            if (!Cancelled && elapsed < EruptAt) {
                if (SourceIndex < 0 || SourceIndex >= Main.maxNPCs || !Main.npc[SourceIndex].active
                    || Main.npc[SourceIndex].type != SourceType) {
                    Cancelled = true;
                }
            }
            if (Cancelled && elapsed >= EruptAt) {
                Projectile.Kill();
                return;
            }

            //判定窗=可见喷发窗
            Projectile.hostile = !Cancelled && elapsed >= EruptAt && elapsed < EruptAt + EruptFrames;

            if (Cancelled || Main.dedServ) {
                return;
            }

            if (elapsed < Delay) {
                //蛰伏期：极低频的表面沙星（阵列 omen 已做主预告，此处只留存在感）
                if (Main.rand.NextBool(8)) {
                    Dust idle = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 2f),
                        DustID.Sand, new Vector2(0f, -0.6f), 140, default, 0.7f);
                    idle.noGravity = true;
                }
                return;
            }

            if (elapsed < EruptAt) {
                //涌沙读秒：贴柱位的沙沸（≤2 粒/帧）
                float boil = (elapsed - Delay) / (float)BoilFrames;
                if (Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-ColumnHalfWidth, ColumnHalfWidth), 2f),
                        DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(1.5f, 3f + 3f * boil)),
                        110, default, 1f + boil * 0.7f);
                    dust.noGravity = true;
                }
                return;
            }

            if (elapsed == EruptAt) {
                //破土帧：爆发（各端本地播放）
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.25f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(4f, 9f)),
                        90, default, Main.rand.NextFloat(1.2f, 1.8f));
                    burst.noGravity = Main.rand.NextBool();
                }
            }
            else if (elapsed < EruptAt + EruptFrames && Main.rand.NextBool(2)) {
                //喷发期：持续沙浪（≤2 粒/帧）
                Dust wave = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-ColumnHalfWidth, ColumnHalfWidth) * 0.7f, 0f),
                    DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(3f, 7f)), 100, default,
                    Main.rand.NextFloat(1f, 1.5f));
                wave.noGravity = true;
            }

            float bodyLight = EruptProgress * RetractFactor;
            if (bodyLight > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * ColumnHeight * 0.5f,
                    new Vector3(0.3f, 0.24f, 0.11f) * bodyLight);
            }
        }

        /// <summary>柱形判定：沿柱轴分三段取样（判定窗已由 hostile 门控）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float erupt = EruptProgress;
            if (erupt < 0.2f) {
                return false;
            }
            float height = ColumnHeight * erupt;
            for (int i = 0; i < 3; i++) {
                Vector2 point = Projectile.Center - new Vector2(0f, height * (0.17f + 0.33f * i));
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(ColumnHalfWidth * 2f, height * 0.4f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float cancelDim = Cancelled ? 0.4f : 1f;

            if (elapsed < EruptAt) {
                //蛰伏+读秒期：柱位小标（暗沿+微光+鼓包沙块，读秒期渐强）
                float boil = elapsed < Delay ? 0.25f
                    : 0.25f + 0.75f * (elapsed - Delay) / (float)BoilFrames;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                Vector2 markPos = Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition;
                Main.EntitySpriteDraw(glowTex, markPos, null, CrownGlow * (0.5f * boil * pulse * cancelDim), 0f,
                    glowTex.Size() / 2f, new Vector2(1.1f, 0.35f), SpriteEffects.None, 0);
                for (int i = 0; i < 2; i++) {
                    float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.identity + i * 2.6f);
                    Vector2 pos = markPos + new Vector2((i * 2 - 1) * 8f + jig * 2f, -4f * boil);
                    Color mound = Color.Lerp(lightColor, SandBrightLerp, 0.45f) * (0.8f * boil * cancelDim);
                    Main.EntitySpriteDraw(tex, pos, null, mound, jig * 0.4f, orig, 0.55f + 0.3f * boil, SpriteEffects.None, 0);
                }
                return false;
            }

            float eruptVis = EruptProgress;
            float retract = RetractFactor;
            if (eruptVis <= 0.01f || retract <= 0.01f) {
                return false;
            }
            float height = ColumnHeight * eruptVis * MathHelper.Clamp(retract * 1.3f, 0f, 1f);

            //暗沿衬底（真 alpha，柱体轮廓）
            Texture2D under = CWRAsset.Extra_98.Value;
            Vector2 underScale = new Vector2(ColumnHalfWidth * 2.6f / under.Width, height * 1.15f / under.Height);
            Main.EntitySpriteDraw(under, Projectile.Center - new Vector2(0f, height * 0.5f) - Main.screenPosition,
                null, SandDeepDim * (0.7f * retract), 0f, under.Size() / 2f, underScale, SpriteEffects.None, 0);

            //沙柱段：原版沙块贴图堆叠（实体层），确定性抖动
            for (int i = 0; i < 6; i++) {
                float seg = (i + 0.5f) / 6f;
                float jig = MathF.Sin(Projectile.identity * 1.31f + i * 2.7f + Main.GlobalTimeWrappedHourly * 26f);
                Vector2 pos = Projectile.Center - new Vector2(-jig * 4f, height * seg) - Main.screenPosition;
                float segScale = 1.1f - 0.45f * seg;
                Color segColor = Color.Lerp(lightColor, SandBrightLerp, 0.55f) * (retract * (1f - 0.25f * seg));
                Main.EntitySpriteDraw(tex, pos, null, segColor, jig * 0.9f + i, orig, segScale, SpriteEffects.None, 0);
            }

            //顶冠加色光（敷料）
            Texture2D crown = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(crown, Projectile.Center - new Vector2(0f, height) - Main.screenPosition,
                null, CrownGlow * (0.45f * retract * eruptVis), 0f, crown.Size() / 2f,
                new Vector2(0.8f, 0.55f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || Cancelled || Elapsed < EruptAt) {
                return;
            }
            //落沙收场
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center - new Vector2(Main.rand.NextFloat(-ColumnHalfWidth, ColumnHalfWidth),
                        Main.rand.NextFloat(0f, ColumnHeight * 0.6f)),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f)),
                    100, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = false;
            }
        }
    }
}
