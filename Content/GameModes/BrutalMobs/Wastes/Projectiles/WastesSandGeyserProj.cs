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
    /// 遁地伏击地涌沙柱。ai[0]=体型 ai[1]=来源NPC+1 ai[2]=来源NPC类型。
    /// 生成位置即锁定落点（预告即承诺）：地表沙沸预告 34 帧 → 破土喷发 22 帧（仅此窗口有判定）→ 消散。
    /// 预告期来源蠕虫被击杀则取消喷发（反制有效）；判定沿柱轴取样，窗口与可见喷发精确对齐
    /// </summary>
    internal class WastesSandGeyserProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>预告帧数（公平契约 ≥30，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 34;
        /// <summary>喷发帧数（判定窗=可见喷发窗）</summary>
        private const int EruptFrames = 22;
        private const int FadeFrames = 12;
        /// <summary>柱高（×体型）</summary>
        private const float BaseHeight = 150f;
        /// <summary>柱半宽（×体型）</summary>
        private const float BaseHalfWidth = 22f;
        /// <summary>喷发爆出用时（帧）</summary>
        private const int EruptRiseFrames = 7;

        private float Scale => Projectile.ai[0];
        private int TotalLife => TelegraphFrames + EruptFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>破土程度 0~1（快速爆出）</summary>
        private float EruptProgress {
            get {
                int t = Elapsed - TelegraphFrames;
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
                int t = Elapsed - TelegraphFrames - EruptFrames;
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
            Projectile.timeLeft = TelegraphFrames + EruptFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //预告期来源检查：蠕虫被击杀则取消（已开始喷发则不回收）；各端读同步的 npc.active，结论一致。
            //类型比对防槽位复用：原虫死后同槽刷出新怪时不放行
            if (!Cancelled && elapsed < TelegraphFrames) {
                int src = (int)Projectile.ai[1] - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != (int)Projectile.ai[2]) {
                    Cancelled = true;
                }
            }
            if (Cancelled && elapsed >= TelegraphFrames) {
                Projectile.Kill();
                return;
            }

            //判定窗=可见喷发窗
            Projectile.hostile = !Cancelled && elapsed >= TelegraphFrames && elapsed < TelegraphFrames + EruptFrames;

            if (elapsed == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.7f, Pitch = 0.35f, MaxInstances = 5 }, Projectile.Center);
            }

            if (Cancelled || Main.dedServ) {
                return;
            }

            if (elapsed < TelegraphFrames) {
                //预告期：地表沙沸（≤2 粒/帧）
                float progress = elapsed / (float)TelegraphFrames;
                if (Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale, 2f),
                        DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(1f, 2.5f + 3f * progress)),
                        110, default, 1f + progress * 0.8f);
                    dust.noGravity = true;
                }
                return;
            }

            if (elapsed == TelegraphFrames) {
                //破土帧：爆发
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(4f, 9f)) * Scale,
                        90, default, Main.rand.NextFloat(1.2f, 1.9f));
                    dust.noGravity = Main.rand.NextBool();
                }
            }
            else if (elapsed < TelegraphFrames + EruptFrames && Main.rand.NextBool(2)) {
                //喷发期：持续沙浪（≤2 粒/帧）
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale * 0.7f, 0f),
                    DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(3f, 7f)) * Scale, 100, default,
                    Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }

            float bodyLight = EruptProgress * RetractFactor;
            if (bodyLight > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * BaseHeight * 0.5f * Scale,
                    new Vector3(0.32f, 0.26f, 0.12f) * bodyLight);
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
            float height = BaseHeight * Scale * erupt;
            float halfWidth = BaseHalfWidth * Scale;
            for (int i = 0; i < 3; i++) {
                Vector2 point = Projectile.Center - new Vector2(0f, height * (0.17f + 0.33f * i));
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(halfWidth * 2f, height * 0.4f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float cancelDim = Cancelled ? 0.4f : 1f;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;

            if (elapsed < TelegraphFrames) {
                //预告期：地表警示光斑 + 鼓包沙块（实体感锚点）
                float progress = elapsed / (float)TelegraphFrames;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 markPos = Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition;
                Color warn = new Color(255, 200, 110, 0) * (0.55f * progress * pulse * cancelDim);
                Main.EntitySpriteDraw(glow, markPos, null, warn, 0f, glow.Size() / 2f,
                    new Vector2(1.5f * Scale, 0.4f), SpriteEffects.None, 0);

                for (int i = 0; i < 3; i++) {
                    float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.identity + i * 2.1f);
                    Vector2 pos = Projectile.Center + new Vector2((i - 1) * 12f * Scale + jig * 2f, 2f - 5f * progress)
                        - Main.screenPosition;
                    Color mound = Color.Lerp(lightColor, new Color(226, 196, 120), 0.5f) * (0.85f * progress * cancelDim);
                    Main.EntitySpriteDraw(tex, pos, null, mound, jig * 0.4f, orig,
                        0.7f + 0.3f * progress, SpriteEffects.None, 0);
                }
                return false;
            }

            float eruptVis = EruptProgress;
            float retract = RetractFactor;
            if (eruptVis <= 0.01f || retract <= 0.01f) {
                return false;
            }
            float height = BaseHeight * Scale * eruptVis * MathHelper.Clamp(retract * 1.3f, 0f, 1f);

            //暗沿衬底（真 alpha，柱体轮廓）
            Texture2D under = CWRAsset.Extra_98.Value;
            Vector2 underScale = new Vector2(BaseHalfWidth * 2.6f * Scale / under.Width, height * 1.15f / under.Height);
            Color underColor = new Color(112, 88, 46) * (0.7f * retract);
            Main.EntitySpriteDraw(under, Projectile.Center - new Vector2(0f, height * 0.5f) - Main.screenPosition,
                null, underColor, 0f, under.Size() / 2f, underScale, SpriteEffects.None, 0);

            //沙柱段：原版沙块贴图堆叠（实体层），确定性抖动
            for (int i = 0; i < 6; i++) {
                float seg = (i + 0.5f) / 6f;
                float jig = MathF.Sin(Projectile.identity * 1.31f + i * 2.7f + Main.GlobalTimeWrappedHourly * 26f);
                Vector2 pos = Projectile.Center - new Vector2(-jig * 5f * Scale, height * seg) - Main.screenPosition;
                float segScale = (1.2f - 0.5f * seg) * Scale;
                Color segColor = Color.Lerp(lightColor, new Color(232, 202, 126), 0.55f) * (retract * (1f - 0.25f * seg));
                Main.EntitySpriteDraw(tex, pos, null, segColor, jig * 0.9f + i, orig, segScale, SpriteEffects.None, 0);
            }

            //顶冠加色光（敷料）
            Texture2D crownGlow = CWRAsset.SoftGlow.Value;
            Color crown = new Color(255, 224, 150, 0) * (0.45f * retract * eruptVis);
            Main.EntitySpriteDraw(crownGlow, Projectile.Center - new Vector2(0f, height) - Main.screenPosition,
                null, crown, 0f, crownGlow.Size() / 2f, new Vector2(0.9f * Scale, 0.6f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || Cancelled) {
                return;
            }
            //落沙收场
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center - new Vector2(Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale,
                        Main.rand.NextFloat(0f, BaseHeight * 0.6f) * Scale),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f)),
                    100, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = false;
            }
        }
    }
}
