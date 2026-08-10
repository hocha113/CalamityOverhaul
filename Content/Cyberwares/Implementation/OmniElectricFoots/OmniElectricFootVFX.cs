using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 电磁冲击环，压扁贴在地平面上的扩张环；外冷蓝环带 + 早期白热内缘
    /// </summary>
    internal class PRT_OmniShockRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Ring01";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        private float radius;
        private float expandSpeed;
        //纵向压扁比，越小越贴地
        private float squash;

        /// <param name="radius0">起始半径</param>
        /// <param name="expand">每帧扩张量，逐帧衰减</param>
        /// <param name="squashK">纵向压扁比，1=正圆</param>
        public PRT_OmniShockRing Configure(float radius0, float expand, float squashK, int lifetime) {
            radius = radius0;
            expandSpeed = expand;
            squash = squashK;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            radius = 0f;
            expandSpeed = 0f;
            squash = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 18;
            }
            if (squash <= 0f) {
                squash = 0.3f;
            }
        }

        public override void AI() {
            radius += expandSpeed;
            //扩张软着陆，读作能量耗散而非匀速贴图放大
            expandSpeed *= 0.88f;
            float lc = LifetimeCompletion;
            Opacity = MathF.Min(lc * 9f, 1f) * (1f - lc) * (1f - lc);
            Lighting.AddLight(Position, Color.R / 900f, Color.G / 700f, Color.B / 600f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float sx = radius * 2f / tex.Width;
            Color edge = Color with { A = 0 };

            spriteBatch.Draw(tex, pos, null, edge * (0.85f * Opacity), 0f, origin
                , new Vector2(sx, sx * squash), SpriteEffects.None, 0f);
            //白热内缘只在前段存在，撑住"刚炸开"的温度
            if (LifetimeCompletion < 0.45f) {
                float hot = 1f - LifetimeCompletion / 0.45f;
                spriteBatch.Draw(tex, pos, null, Color.White with { A = 0 } * (0.7f * Opacity * hot), 0f, origin
                    , new Vector2(sx * 0.82f, sx * squash * 0.82f), SpriteEffects.None, 0f);
            }

            Texture2D glow = GlowTex?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, pos, null, edge * (0.4f * Opacity), 0f, glow.Size() * 0.5f
                    , new Vector2(radius / glow.Width * 2.2f, radius / glow.Width * 2.2f * squash)
                    , SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 全向电动义足演出集中处；一律客户端，服务器直接返回
    /// <br/>三个签名行为：折线电弧、顺速拉丝的排气、压在地平面的冲击环
    /// </summary>
    internal static class OmniElectricFootVFX
    {
        internal static readonly Color VoltCold = new(96, 190, 255);
        internal static readonly Color VoltDeep = new(38, 92, 176);
        internal static readonly Color VoltHot = new(255, 236, 170);

        /// <summary>脚底世界坐标，反重力取头顶</summary>
        internal static Vector2 Feet(Player player)
            => player.gravDir == 1f ? player.Bottom : player.Top;

        /// <summary>屏幕向上的符号，反重力为 +1</summary>
        internal static float Up(Player player) => -player.gravDir;

        /// <summary>蓄力：外围火花向脚底收束，脚下压着电弧；收束方向是"在攒"的读法</summary>
        internal static void ChargeConverge(Player player, float ratio) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 feet = Feet(player);
            float up = Up(player);

            int inbound = ratio > 0.75f ? 3 : (ratio > 0.4f ? 2 : 1);
            for (int i = 0; i < inbound; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(26f, 54f) * (1.15f - ratio * 0.35f);
                Vector2 from = feet + new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.55f) * dist;
                //吸向脚底，越满蓄吸得越急
                Vector2 vel = (feet - from) * (0.075f + ratio * 0.075f);
                PRTLoader.NewParticle<PRT_Spark>(from, vel
                    , Color.Lerp(VoltDeep, VoltCold, ratio), 0.5f + ratio * 0.45f)
                    .Configure(false, 14);
            }

            //脚下电弧，满蓄时才明显
            if (Main.rand.NextFloat() < 0.25f + ratio * 0.6f) {
                Vector2 arcDir = new Vector2(Main.rand.NextFloat(-1f, 1f), up * Main.rand.NextFloat(0.1f, 0.5f));
                PRTLoader.NewParticle<PRT_GraniteVolt>(feet + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f)
                    , arcDir * Main.rand.NextFloat(1.6f, 3.4f)
                    , Color.Lerp(VoltCold, VoltHot, ratio * 0.6f), 0.34f + ratio * 0.4f)
                    .Configure(Main.rand.Next(3, 6));
            }

            Lighting.AddLight(feet, 0.16f * ratio, 0.34f * ratio, 0.55f * ratio);
        }

        /// <summary>蓄满一次性提示，环闪一下并给一声"到位"</summary>
        internal static void ChargeFull(Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 feet = Feet(player);
            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.75f, Volume = 0.5f, MaxInstances = 2 }, feet);
            PRTLoader.NewParticle<PRT_OmniShockRing>(feet, Vector2.Zero, VoltHot, 1f)
                .Configure(14f, 2.4f, 0.26f, 12);
            for (int i = 0; i < 5; i++) {
                float ang = MathHelper.TwoPi * i / 5f + Main.rand.NextFloat(0.4f);
                PRTLoader.NewParticle<PRT_GraniteVolt>(feet
                    , new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.5f) * Main.rand.NextFloat(2.6f, 4.2f)
                    , VoltHot, 0.6f).Configure(4);
            }
        }

        /// <summary>蓄力断电：离地或失去义体，火花散掉，一声泄压</summary>
        internal static void ChargeFizzle(Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 feet = Feet(player);
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                Pitch = -0.85f,
                Volume = 0.4f,
                MaxInstances = 2
            }, feet);
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.5f, Volume = 0.3f, MaxInstances = 2 }, feet);
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.2f, 1.4f);
                PRTLoader.NewParticle<PRT_Spark>(feet + Main.rand.NextVector2Circular(6f, 4f), vel
                    , VoltDeep, Main.rand.NextFloat(0.35f, 0.6f)).Configure(true, 16);
            }
        }

        /// <summary>推进窗口内的排气；顺速拉丝，速度越快尾越长</summary>
        internal static void ThrustExhaust(Player player, float strength) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 feet = Feet(player);
            float up = Up(player);
            int count = strength > 0.7f ? 2 : 1;
            for (int i = 0; i < count; i++) {
                Vector2 pos = feet + new Vector2(Main.rand.NextFloat(-7f, 7f), 0f);
                //向后（下）喷，带一点玩家横速的偏斜
                Vector2 vel = new Vector2(player.velocity.X * -0.18f + Main.rand.NextFloat(-0.7f, 0.7f)
                    , -up * Main.rand.NextFloat(2.4f, 4.8f) * (0.6f + strength * 0.6f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel
                    , Color.Lerp(VoltCold, VoltHot, Main.rand.NextFloat(0.35f)), 0.5f + strength * 0.35f)
                    .Configure(false, 12);
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Smoke>(feet + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f)
                    , new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -up * Main.rand.NextFloat(0.6f, 1.6f))
                    , VoltDeep * 0.5f, Main.rand.NextFloat(0.2f, 0.32f)).Configure(16, 0.45f, 0.04f);
            }
            Lighting.AddLight(feet, 0.2f * strength, 0.42f * strength, 0.7f * strength);
        }

        /// <summary>空中蹬出；ExtraJump.OnStarted 在各端都跑，这里不必额外广播</summary>
        internal static void AirJumpBurst(Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 feet = Feet(player);
            float up = Up(player);

            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Pitch = 0.35f, Volume = 0.5f, MaxInstances = 3 }, feet);
            //底下垫一层原版二段跳的气声，电弧才不像凭空响
            SoundEngine.PlaySound(SoundID.DoubleJump with { Pitch = 0.45f, Volume = 0.42f, MaxInstances = 3 }, feet);

            PRTLoader.NewParticle<PRT_OmniShockRing>(feet, Vector2.Zero, VoltCold, 1f)
                .Configure(10f, 3.6f, 0.3f, 15);

            //贴地平面铺开的火花裙，不是球形爆散
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(0.25f);
                Vector2 dir = new(MathF.Cos(ang), MathF.Sin(ang) * 0.4f);
                PRTLoader.NewParticle<PRT_Spark>(feet, dir * Main.rand.NextFloat(2.4f, 4.6f)
                    , Color.Lerp(VoltCold, VoltHot, Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(0.5f, 0.8f))
                    .Configure(true, 18);
            }
            for (int i = 0; i < 4; i++) {
                Vector2 arc = new(Main.rand.NextFloat(-1f, 1f), -up * Main.rand.NextFloat(0.2f, 0.8f));
                PRTLoader.NewParticle<PRT_GraniteVolt>(feet + Main.rand.NextVector2Circular(8f, 4f)
                    , arc * Main.rand.NextFloat(2.5f, 4.5f), VoltCold, Main.rand.NextFloat(0.4f, 0.62f))
                    .Configure(Main.rand.Next(3, 6));
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(feet
                    , Vector2.UnitY * player.gravDir, 2.2f, 6f, 7, 650f, "OmniElectricFootAirJump"));
            }
        }

        /// <summary>蓄力跳蹬地；由 <see cref="OmniElectricFootBurst"/> 在各端调用</summary>
        internal static void ChargeLaunchBurst(Player player, float ratio) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 feet = Feet(player);
            float up = Up(player);

            //三层：低频蹬地 + 电磁放电 + 满蓄的那记闷响
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.5f + ratio * 0.35f,
                Pitch = -0.35f - ratio * 0.25f,
                MaxInstances = 3
            }, feet);
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                Volume = 0.38f + ratio * 0.32f,
                Pitch = 0.3f - ratio * 0.45f,
                MaxInstances = 3
            }, feet);
            if (ratio > 0.65f) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.34f, Pitch = -0.75f, MaxInstances = 2 }, feet);
            }

            PRTLoader.NewParticle<PRT_OmniShockRing>(feet, Vector2.Zero
                , Color.Lerp(VoltCold, VoltHot, ratio * 0.5f), 1f)
                .Configure(12f, 4.5f + ratio * 6.5f, 0.24f, 18 + (int)(ratio * 8f));
            if (ratio > 0.5f) {
                //第二道环慢一拍追出去，撑出"两级点火"
                PRTLoader.NewParticle<PRT_OmniShockRing>(feet, Vector2.Zero, VoltDeep, 1f)
                    .Configure(20f, 3.2f + ratio * 4f, 0.2f, 22);
            }

            int sparks = (int)MathHelper.Lerp(14f, 34f, ratio);
            for (int i = 0; i < sparks; i++) {
                float ang = MathHelper.TwoPi * i / sparks + Main.rand.NextFloat(0.3f);
                Vector2 dir = new(MathF.Cos(ang), MathF.Sin(ang) * 0.32f);
                PRTLoader.NewParticle<PRT_Spark>(feet, dir * Main.rand.NextFloat(3f, 6.5f) * (0.7f + ratio * 0.6f)
                    , Color.Lerp(VoltCold, VoltHot, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.55f, 0.95f))
                    .Configure(true, Main.rand.Next(18, 28));
            }
            //起跳瞬间的排气柱，人已经在往上走，尾迹留在原地
            for (int i = 0; i < 6 + (int)(ratio * 6f); i++) {
                PRTLoader.NewParticle<PRT_Spark>(feet + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f)
                    , new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -up * Main.rand.NextFloat(4f, 9f))
                    , VoltCold, Main.rand.NextFloat(0.5f, 0.85f)).Configure(false, 16);
            }
            int arcs = 4 + (int)(ratio * 5f);
            for (int i = 0; i < arcs; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(feet + Main.rand.NextVector2Circular(12f, 5f)
                    , Main.rand.NextVector2Circular(4f, 2f), VoltHot, Main.rand.NextFloat(0.45f, 0.75f))
                    .Configure(Main.rand.Next(3, 7));
            }
            //蹬地扬起的尘，慢层，让爆点有余韵
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(feet + new Vector2(Main.rand.NextFloat(-14f, 14f), 0f)
                    , new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -up * Main.rand.NextFloat(0.4f, 1.4f))
                    , VoltDeep * 0.6f, Main.rand.NextFloat(0.3f, 0.5f)).Configure(26, 0.5f, 0.03f);
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(feet
                    , Vector2.UnitY * player.gravDir, 3f + ratio * 4.5f, 6.5f
                    , 10 + (int)(ratio * 9f), 780f, "OmniElectricFootChargeJump"));
            }
        }

        /// <summary>推进期撞上天花板：火花贴着顶棚横向铺开，一声硬碰撞</summary>
        internal static void CeilingSlam(Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 head = player.gravDir == 1f ? player.Top : player.Bottom;
            float up = Up(player);

            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.25f, Volume = 0.5f, MaxInstances = 3 }, head);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.5f, Volume = 0.32f, MaxInstances = 3 }, head);

            //环压在顶棚平面上，方向与地面冲击环相同、位置在头顶
            PRTLoader.NewParticle<PRT_OmniShockRing>(head + new Vector2(0f, up * 4f)
                , Vector2.Zero, VoltCold, 1f).Configure(8f, 2.6f, 0.22f, 12);
            for (int i = 0; i < 9; i++) {
                float side = Main.rand.NextBool() ? -1f : 1f;
                Vector2 vel = new(side * Main.rand.NextFloat(1.6f, 4.2f)
                    , -up * Main.rand.NextFloat(0.2f, 1.2f));
                PRTLoader.NewParticle<PRT_Spark>(head + new Vector2(0f, up * 4f), vel
                    , VoltCold, Main.rand.NextFloat(0.45f, 0.7f)).Configure(true, 16);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(head + new Vector2(Main.rand.NextFloat(-9f, 9f), up * 3f)
                    , new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), 0f), VoltCold
                    , Main.rand.NextFloat(0.3f, 0.5f)).Configure(3);
            }
        }

        /// <summary>义足推起的那趟腾空落地，缓冲吸收，不是砸地</summary>
        internal static void LandingCushion(Player player, float fallSpeed) {
            if (VaultUtils.isServer) {
                return;
            }
            float weight = MathHelper.Clamp((fallSpeed - 7f) / 9f, 0f, 1f);
            Vector2 feet = Feet(player);
            float up = Up(player);

            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.35f + weight * 0.3f,
                Pitch = -0.35f - weight * 0.25f,
                MaxInstances = 3
            }, feet);
            if (weight > 0.45f) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                    Volume = 0.22f,
                    Pitch = -0.35f,
                    MaxInstances = 2
                }, feet);
            }

            PRTLoader.NewParticle<PRT_OmniShockRing>(feet, Vector2.Zero, VoltDeep, 1f)
                .Configure(8f, 2.2f + weight * 3f, 0.22f, 13);
            for (int i = 0; i < 4 + (int)(weight * 6f); i++) {
                float side = Main.rand.NextBool() ? -1f : 1f;
                PRTLoader.NewParticle<PRT_Smoke>(feet + new Vector2(side * Main.rand.NextFloat(2f, 12f), 0f)
                    , new Vector2(side * Main.rand.NextFloat(0.8f, 2.4f), -up * Main.rand.NextFloat(0.3f, 1f))
                    , VoltDeep * 0.5f, Main.rand.NextFloat(0.24f, 0.42f)).Configure(22, 0.45f, 0.03f);
            }
            if (weight > 0.3f) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(feet + Main.rand.NextVector2Circular(10f, 3f)
                        , new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), 0f), VoltCold
                        , Main.rand.NextFloat(0.3f, 0.5f)).Configure(3);
                }
            }
        }
    }

    /// <summary>
    /// 蓄力跳的演出载体：owner 端生成，弹幕生成包自然同步，各端播同一套声画
    /// <br/>无伤害，不移动，不绘制自身；<see cref="Projectile.ai"/>[0]=蓄力比例
    /// </summary>
    internal class OmniElectricFootBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool played;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>只在 owner 端调用</summary>
        internal static void Fire(Player owner, float ratio) {
            if (owner == null || !owner.active || Main.myPlayer != owner.whoAmI) {
                return;
            }
            Projectile.NewProjectile(owner.GetSource_Misc("CWR_OmniElectricFootBurst")
                , OmniElectricFootVFX.Feet(owner), Vector2.Zero
                , ModContent.ProjectileType<OmniElectricFootBurst>(), 0, 0f, owner.whoAmI
                , ai0: MathHelper.Clamp(ratio, 0f, 1f));
        }

        public override void AI() {
            if (played) {
                return;
            }
            played = true;
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) {
                return;
            }
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true) {
                return;
            }
            OmniElectricFootVFX.ChargeLaunchBurst(owner, MathHelper.Clamp(Projectile.ai[0], 0f, 1f));
        }
    }
}
