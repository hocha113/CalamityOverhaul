using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rainveil.Projectiles
{
    /// <summary>
    /// 「雨帷落雷」雷击柱。ai[0]=体型抖动。
    /// 生成位置即锁定落点（预告即承诺，ShouldUpdatePosition=false 永不再瞄）：
    /// 地面竖直光柱渐亮+电花尘攒聚 ≥40 帧（此窗口 CanDamage=false）→ 雷击一拍
    /// （竖直闪电视觉走 <see cref="PRT_SkyBolt"/> 各端自绘，命中挂原版 Electrified 2 秒）
    /// → 余辉散逸收场。伤害窗为雷光最亮段的子集，失败方向=安全方向；
    /// 各端由 timeLeft 确定性推演，无追加同步
    /// </summary>
    internal class RainveilThunderboltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>落雷警示帧数（公平契约 ≥40，档位一律不缩短）</summary>
        private const int OmenFrames = 46;
        /// <summary>雷击判定帧数（雷光最亮段的子集，伤害窗 ⊆ 可见窗）</summary>
        private const int StrikeFrames = 12;
        /// <summary>余辉帧数（判定关闭，只剩残光与焦烟）</summary>
        private const int LingerFrames = 18;
        /// <summary>雷柱判定高度（×体型），自地面竖直向上</summary>
        private const float BoltHeight = 680f;
        /// <summary>雷柱判定半宽（×体型），窄于可见雷光</summary>
        private const float BoltHalfWidth = 15f;
        /// <summary>警示光柱可见高度（×体型，随预告推进升高）</summary>
        private const float OmenPillarHeight = 210f;
        /// <summary>命中后的原版感电时长（2 秒）</summary>
        private const int ElectrifiedFrames = 120;
        /// <summary>天雷视觉的高空起点高度</summary>
        private const float SkySourceHeight = 920f;

        //雷光配色：冷白蓝电色
        private static readonly Color BoltGlow = new(160, 205, 255);

        private float Scale => Projectile.ai[0] <= 0f ? 1f : Projectile.ai[0];

        private const int TotalLife = OmenFrames + StrikeFrames + LingerFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>雷击判定窗（预告结束后的一拍）</summary>
        private bool InStrikeWindow => Elapsed >= OmenFrames && Elapsed < OmenFrames + StrikeFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//只在雷击窗内置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>预告窗强制无伤（双保险，hostile 门另有同款判据）</summary>
        public override bool? CanDamage() => InStrikeWindow ? null : false;

        public override void AI() {
            int elapsed = Elapsed;
            //判定窗=雷击一拍；Boss 登场瞬间已在场雷柱一并缴械（视觉走完），各端结论一致
            Projectile.hostile = GameModeSystem.BrutalActive && !CWRWorld.HasBoss && InStrikeWindow;

            if (Main.dedServ) {
                return;
            }

            float seed = Projectile.identity * 1.73f;
            if (elapsed == 0) {
                //预告起手：低鸣蓄能（听觉通道①）
                SoundEngine.PlaySound(SoundID.Item93 with {
                    Volume = 0.35f,
                    Pitch = -0.5f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            if (elapsed < OmenFrames) {
                float progress = elapsed / (float)OmenFrames;
                //预告期：断续电噼啪逐渐升调（听觉通道②）
                if (elapsed % 12 == 0 && elapsed > 0) {
                    SoundEngine.PlaySound(SoundID.Item93 with {
                        Volume = 0.16f + 0.10f * progress,
                        Pitch = -0.25f + 0.5f * progress,
                        MaxInstances = 4
                    }, Projectile.Center);
                }
                //电花尘自地面攒聚上浮（视觉通道，密度随预告推进）
                if (Main.rand.NextBool(3 - (elapsed * 2 / OmenFrames))) {
                    Dust spark = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f) * Scale, 2f),
                        DustID.Electric, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                            -Main.rand.NextFloat(0.5f, 1.9f)),
                        100, default, Main.rand.NextFloat(0.55f, 1.0f));
                    spark.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * 30f,
                    new Vector3(0.10f, 0.13f, 0.24f) * progress);
                return;
            }

            if (elapsed == OmenFrames) {
                //雷击一拍：天雷视觉各端自绘（PRT 天生端本地，落点由同步弹幕锁定）
                PRT_SkyBolt bolt = PRTLoader.NewParticle<PRT_SkyBolt>(
                    Projectile.Center, Vector2.Zero, BoltGlow, 1f);
                bolt?.Configure(Projectile.Center - new Vector2(0f, SkySourceHeight * Scale),
                    Projectile.Center);
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Volume = 0.8f,
                    Pitch = Main.rand.NextFloat(-0.1f, 0.15f),
                    MaxInstances = 3
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with {
                    Volume = 0.45f,
                    Pitch = -0.1f,
                    MaxInstances = 3
                }, Projectile.Center);
                for (int i = 0; i < 16; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center,
                        DustID.Electric,
                        new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(0.5f, 4.2f)) * Scale,
                        90, default, Main.rand.NextFloat(0.8f, 1.4f));
                    burst.noGravity = true;
                }
                for (int i = 0; i < 5; i++) {
                    //落点焦烟：雷落在了地上而非凭空闪过
                    Dust smoke = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f) * Scale, 0f),
                        DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)),
                        160, default, Main.rand.NextFloat(0.8f, 1.3f));
                    smoke.noGravity = true;
                }
            }

            if (InStrikeWindow) {
                //雷击窗：柱内残余电花闪跳
                if (Main.rand.NextBool(2)) {
                    Dust arc = Dust.NewDustPerfect(
                        Projectile.Center - new Vector2(
                            Main.rand.NextFloat(-BoltHalfWidth, BoltHalfWidth) * Scale,
                            Main.rand.NextFloat(0f, BoltHeight * 0.6f) * Scale),
                        DustID.Electric, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), 0.3f),
                        110, default, Main.rand.NextFloat(0.5f, 0.9f));
                    arc.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * 120f,
                    new Vector3(0.5f, 0.6f, 0.95f));
            }

            //余辉期：残余电花渐熄+落点微光衰减
            float linger = MathHelper.Clamp(
                (elapsed - OmenFrames - StrikeFrames) / (float)LingerFrames, 0f, 1f);
            if (linger > 0f) {
                if (Main.rand.NextBool(4)) {
                    Dust ember = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f) * Scale, 0f),
                        DustID.Electric, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)),
                        140, default, Main.rand.NextFloat(0.4f, 0.7f));
                    ember.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * 40f,
                    new Vector3(0.2f, 0.25f, 0.4f) * (1f - linger) *
                    (0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 20f + seed)));
            }
        }

        /// <summary>柱形判定：自地面竖直向上分三段取样，只在雷击窗内有效</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float height = BoltHeight * Scale;
            float halfWidth = BoltHalfWidth * Scale;
            for (int i = 0; i < 3; i++) {
                Vector2 point = Projectile.Center - new Vector2(0f, height * (0.17f + 0.33f * i));
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(halfWidth * 2f, height * 0.4f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //原版感电（受击端本机结算，原生同步）
            target.AddBuff(BuffID.Electrified, ElectrifiedFrames);
        }

        public override bool PreDraw(ref Color lightColor) {
            //豁免声明：闪电属光（镜像 MrtTeslaArcProj 的裁定）——警示柱与雷光全加色（A=0），
            //弹体遮挡像素要求不适用；雷击主体由 PRT_SkyBolt 的 ThunderTrail 承载
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || glow.IsDisposed) {
                return false;
            }
            Vector2 origin = glow.Size() * 0.5f;
            int elapsed = Elapsed;
            float time = Main.GlobalTimeWrappedHourly;
            float seed = Projectile.identity * 1.73f;
            Color core = new(BoltGlow.R, BoltGlow.G, BoltGlow.B, (byte)0);

            if (elapsed < OmenFrames) {
                float progress = elapsed / (float)OmenFrames;
                float pulse = 0.75f + 0.25f * MathF.Sin(time * 14f + seed);
                //地面警示光斑：横椭圆脉动渐亮
                Main.EntitySpriteDraw(glow,
                    Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition, null,
                    core * (0.38f * progress * pulse), 0f, origin,
                    new Vector2(1.35f * Scale, 0.34f), SpriteEffects.None, 0);
                //竖直警示光柱：自地面渐亮升高
                float pillarH = OmenPillarHeight * (0.25f + 0.75f * progress) * Scale;
                float pillarW = 30f * Scale;
                Main.EntitySpriteDraw(glow,
                    Projectile.Center - new Vector2(0f, pillarH * 0.5f) - Main.screenPosition, null,
                    core * ((0.10f + 0.26f * progress) * pulse), 0f, origin,
                    new Vector2(pillarW / glow.Width, pillarH / glow.Height),
                    SpriteEffects.None, 0);
                return false;
            }

            //雷击与余辉：落点辉光快起慢收（雷柱本体由 PRT 绘制）
            float t = (elapsed - OmenFrames) / (float)(StrikeFrames + LingerFrames);
            float env = 1f - t * t;
            if (env > 0.02f) {
                Main.EntitySpriteDraw(glow,
                    Projectile.Center - Main.screenPosition, null,
                    core * (0.5f * env), 0f, origin,
                    new Vector2(1.0f, 0.55f) * Scale * (0.8f + 0.4f * env),
                    SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //收场：残余电花散尽
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f) * Scale, -4f),
                    DustID.Electric, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.3f, 0.9f)),
                    150, default, Main.rand.NextFloat(0.4f, 0.7f));
                dust.noGravity = true;
            }
        }
    }
}
