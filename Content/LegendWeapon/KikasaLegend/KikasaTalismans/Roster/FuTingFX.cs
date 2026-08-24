using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霆的演出集中处：雷泉喷发雷冠与存续裹电，全部端本地纯表现</summary>
    internal static class FuTingFX
    {
        /// <summary>链电弧亮芯色</summary>
        internal static readonly Color ThunderCore = new(232, 218, 255);

        /// <summary>自泉的 ai[2] 还原柱高倍率（与 KikasaInkGeyser 同口径）</summary>
        private static float GeyserHeightMul(Projectile geyser)
            => geyser.ai[2] > 0.5f ? geyser.ai[2] / 1000f : 1f;

        /// <summary>
        /// 喷发拍雷冠：柱顶预落一记短雷+电环+火花群，近距轻震屏。
        /// OnGeyserErupt 各端同拍调用（标签随泉生成包同步，旁观端不缺席）
        /// </summary>
        internal static void EruptCrown(Projectile geyser, Color accent) {
            if (Main.dedServ) {
                return;
            }
            //柱高与判定同源（236 是泉的满柱高常量），雷冠落在柱头将至之处
            Vector2 top = geyser.Center - new Vector2(0f, 236f * GeyserHeightMul(geyser));

            //顶端短雷：自更高处劈到柱头，读作"雷给水柱加了冕"
            PRTLoader.NewParticle<PRT_SkyBolt>(top, Vector2.Zero, accent, 1f)
                ?.Configure(top - new Vector2(0f, 170f), top, 18);

            //雷冠电环+火花
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(top, Vector2.Zero,
                accent * 0.6f, 0.08f)?.Configure(0.08f, 0.6f, 12);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(top + Main.rand.NextVector2Circular(14f, 8f),
                    Main.rand.NextVector2Circular(2.6f, 2.2f) - Vector2.UnitY * 1.2f,
                    Color.Lerp(accent, ThunderCore, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(8, 14));
            }

            KikasaInk.Play(SoundID.Item93, geyser.Center, 0.4f, -0.15f, 3);
            KikasaInk.Play(SoundID.Item122, geyser.Center, 0.3f, -0.4f, 3);
            //轻震屏：本地观看者按距离衰减自决
            if (Vector2.Distance(Main.LocalPlayer.Center, geyser.Center) < 900f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(2.2f);
            }
        }

        /// <summary>
        /// 存续期泉体裹紫电：柱身爬电火花+偶发沿柱细雷。
        /// UpdateWhileHeld 逐帧调用（各端本地），延迟期（ai[0] 未走完）不裹
        /// </summary>
        internal static void GeyserWrap(Projectile geyser, Color accent) {
            if (geyser.ai[0] > 0f) {
                return;
            }
            float h = 236f * GeyserHeightMul(geyser);
            //爬电火花：随机高度贴着柱面跳
            if (Main.rand.NextBool(2)) {
                Vector2 pos = geyser.Center
                    + new Vector2(Main.rand.NextFloat(-0.55f, 0.55f) * 46f,
                        -Main.rand.NextFloat(0f, h));
                PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Circular(1.4f, 1.4f),
                    accent * 0.85f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(false, Main.rand.Next(6, 11));
            }
            //沿柱细雷：整根柱身偶尔亮一道电脉
            if (Main.rand.NextBool(16)) {
                PRTLoader.NewParticle<PRT_SkyBolt>(geyser.Center, Vector2.Zero,
                    accent * 0.55f, 1f)?.Configure(geyser.Center - new Vector2(0f, h),
                    geyser.Center, 10);
            }
        }
    }

    /// <summary>
    /// 霆的链电弧：霆标泉命中时自被击者跳向旁敌，仅所有者端生成（伤害自然同步），
    /// 各端凭生成包自绘 ThunderTrail 双层弧。<br/>
    /// ai[0]=锁定目标 whoAmI、ai[1]/ai[2]=弧起点世界坐标（被击者位置）
    /// </summary>
    internal class FuTingChainZap : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 14;

        /// <summary>伤害窗帧数：只在跳电瞬间咬人，余下寿命全给弧的退场</summary>
        private const int DamageWindow = 3;

        private ref float TargetAi => ref Projectile.ai[0];

        private Vector2 ArcStart => new(Projectile.ai[1], Projectile.ai[2]);

        private float life;
        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;

        private float Seed => Projectile.identity * 0.7391f % 4.3f;

        private float Envelope => MathHelper.Clamp((LifeFrames - life) / (float)LifeFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.DamageType = Terraria.ModLoader.DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //一道弧对同一目标只结算一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => life <= DamageWindow ? null : false;

        public override void AI() {
            life++;
            Projectile.velocity = Vector2.Zero;
            //伤害窗内贴着目标走，防它一帧挪走
            int who = (int)TargetAi;
            if (life <= DamageWindow && who >= 0 && who < Main.maxNPCs) {
                NPC target = Main.npc[who];
                if (target?.active == true) {
                    Projectile.Center = target.Center;
                }
            }

            if (Main.dedServ) {
                return;
            }
            if ((int)life == 1) {
                KikasaInk.Play(SoundID.Item93, Projectile.Center, 0.32f, 0.1f, 3);
            }
            //沿弧火花
            if (life <= 8 && Main.rand.NextBool(2)) {
                Vector2 sparkPos = Vector2.Lerp(ArcStart, Projectile.Center, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(sparkPos, Main.rand.NextVector2Circular(2f, 2f),
                    Color.Lerp(new Color(182, 138, 244), FuTingFX.ThunderCore, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(false, Main.rand.Next(7, 12));
            }
            Lighting.AddLight(Projectile.Center, 0.22f * Envelope, 0.14f * Envelope, 0.34f * Envelope);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(2.6f, 2.6f), new Color(182, 138, 244),
                    Main.rand.NextFloat(0.6f, 0.9f))?.Configure(false, Main.rand.Next(8, 13));
            }
        }

        //====绘制：两端锚定+中段正弦摆的折线，ThunderTrail 双层====

        private void BuildArcPath() {
            const int pointCount = 10;
            Vector2[] points = new Vector2[pointCount];
            Vector2 start = ArcStart;
            Vector2 end = Projectile.Center;
            Vector2 dir = end - start;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            float waveSeed = Main.GlobalTimeWrappedHourly * 12f + Seed;
            float power = Envelope;
            for (int i = 0; i < pointCount; i++) {
                float t = i / (float)(pointCount - 1);
                float envelope = MathF.Sin(t * MathHelper.Pi);
                points[i] = start + dir * t
                    + perp * (MathF.Sin(waveSeed + t * 8f) * 11f * envelope * power);
            }

            if (mainTrail == null && CWRAsset.ThunderTrail != null) {
                mainTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetMainWidth, GetMainColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                };
                mainTrail.SetRange((0, 7));
                mainTrail.SetExpandWidth(4);
                coreTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetCoreWidth, GetCoreColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0, 4));
                coreTrail.SetExpandWidth(2);
            }
            if (mainTrail == null) {
                return;
            }
            mainTrail.BasePositions = points;
            coreTrail.BasePositions = points;
            if ((int)life % 3 == 0) {
                mainTrail.RandomThunder();
                coreTrail.RandomThunder();
            }
        }

        private float GetMainWidth(float factor) => (10f + 5f * MathF.Sin(factor * MathHelper.Pi)) * Envelope;
        private float GetCoreWidth(float factor) => (4f + 2f * MathF.Sin(factor * MathHelper.Pi)) * Envelope;
        private Color GetMainColor(float factor) => new(182, 138, 244);
        private Color GetCoreColor(float factor) => FuTingFX.ThunderCore;
        private float GetArcAlpha(float factor) => Envelope;

        public override bool PreDraw(ref Color lightColor) {
            if (Envelope <= 0.02f) {
                return false;
            }
            BuildArcPath();
            mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
            coreTrail?.DrawThunder(Main.instance.GraphicsDevice);

            //两端接点辉光：弧不凭空亮起
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color c = new Color(182, 138, 244) with { A = 0 };
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    c * (0.5f * Envelope), 0f, glow.Size() * 0.5f, 0.4f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(glow, ArcStart - Main.screenPosition, null,
                    c * (0.3f * Envelope), 0f, glow.Size() * 0.5f, 0.3f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
