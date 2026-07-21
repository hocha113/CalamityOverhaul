using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>地狱炎爆共置资源加载器，缺 .fxc 时属性为 null，使用前判空</summary>
    internal class FishDemonicHellAssets
    {
        /// <summary>恶魔符环</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishDemonicHellRing { get; private set; }
        /// <summary>地狱火球彗尾条带（重烟版 OniMacheteComet 范式）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishDemonicHellComet { get; private set; }
    }

    internal class FishDemonicHell : FishSkill
    {
        public override int UnlockFishID => ItemID.DemonicHellfish;
        public override int DefaultCooldown => 60 * (12 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 18;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown <= 0 && player.CountProjectilesOfID<HellRitualCircle>() == 0) {
                Use(item, player);
            }
            return null;
        }

        public override void Use(Item item, Player player) {
            SetCooldown();
            //在玩家前方生成法阵（与鼠标方向）
            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 spawnPos = player.Center + dir * 160f; //距离玩家 160
            int circle = Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, dir,
                ModContent.ProjectileType<HellRitualCircle>(), 0, 0f, player.whoAmI, ai0: player.direction);

            //符环显现预告
            SpawnSummonParticles(player.Center, circle);

            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.8f, Pitch = -0.7f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = -0.4f }, player.Center);
        }

        private static void SpawnSummonParticles(Vector2 position, int circleIndex) {
            //收拢暗环
            PRTLoader.NewParticle<PRT_DWave>(position, Vector2.Zero, new Color(190, 60, 25), 1.35f)
                ?.Configure(new Vector2(1f, 1f), 0f, 0.3f, 16);
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(0.5f);
                Vector2 pos = position + ang.ToRotationVector2() * Main.rand.NextFloat(180f, 250f);
                Vector2 vel = (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f);
                PRTLoader.NewParticle<PRT_FishDemonicHellEmber>(pos, vel,
                    new Color(255, 118, 38), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.ConfigureSuction(circleIndex, 0.16f);
            }
        }
    }

    /// <summary>地狱法阵，充能后发射炎爆</summary>
    internal class HellRitualCircle : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private Player Owner => Main.player[Projectile.owner];
        private ref float ChargeTimer => ref Projectile.ai[0];
        private const int ChargeTime = 60; //1s 充能
        private const int FadeTime = 20; //消散
        private const int RevealEnd = 12; //预告拍终点
        private const int OvershootStart = 46; //过冲拍起点
        private float progress => MathHelper.Clamp(ChargeTimer / ChargeTime, 0f, 1f);
        /// <summary>累计自旋弧度，过冲段角加速</summary>
        private ref float SpinAngle => ref Projectile.localAI[0];
        /// <summary>释放暖金闪，1→指数衰减</summary>
        private ref float PopFlash => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = 300;
            Projectile.height = 300;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChargeTime + FadeTime + 2;
        }

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Owner.Center;

            ChargeTimer++;

            //自旋积分
            float spinRate;
            if (ChargeTimer <= OvershootStart) {
                spinRate = 0.008f + progress * 0.006f;
            }
            else if (ChargeTimer <= ChargeTime) {
                float ot = (ChargeTimer - OvershootStart) / (float)(ChargeTime - OvershootStart);
                spinRate = 0.014f + ot * ot * 0.030f;
            }
            else {
                float ft = MathHelper.Clamp((ChargeTimer - ChargeTime) / (float)FadeTime, 0f, 1f);
                spinRate = MathHelper.Lerp(0.030f, 0.004f, ft);
            }
            SpinAngle += spinRate;
            PopFlash *= 0.58f;

            if (ChargeTimer < ChargeTime) {
                SpawnChargeParticles();
            }

            if (ChargeTimer == OvershootStart) {
                //过冲拍入点
                SoundEngine.PlaySound(SoundID.DD2_DarkMageAttack with { Volume = 0.5f, Pitch = -0.55f }, Projectile.Center);
            }

            if (ChargeTimer == ChargeTime) {
                FireBlast();
            }

            //照明
            float lightIntensity = ChargeVisual() * 2.5f;
            Lighting.AddLight(Projectile.Center,
                1.2f * lightIntensity,
                0.4f * lightIntensity,
                0.2f * lightIntensity);
        }

        /// <summary>聚焦环半径系数，收束缓降→过冲加速下探 0.16→释放弹性回弹</summary>
        private float FocusFactor() {
            float t = ChargeTimer;
            if (t <= RevealEnd) {
                return 1f;
            }
            if (t <= OvershootStart) {
                float k = (t - RevealEnd) / (float)(OvershootStart - RevealEnd);
                k = k * k * (3f - 2f * k);
                return MathHelper.Lerp(1f, 0.55f, k);
            }
            if (t <= ChargeTime) {
                float k = (t - OvershootStart) / (float)(ChargeTime - OvershootStart);
                return MathHelper.Lerp(0.55f, 0.16f, k * k);
            }
            float f = MathHelper.Clamp((t - ChargeTime) / 8f, 0f, 1f);
            return MathHelper.Lerp(0.16f, 0.42f, 1f - (1f - f) * (1f - f));
        }

        /// <summary>符环亮度，蓄力爬升，过冲段超压>1，释放后冷却回落</summary>
        private float ChargeVisual() {
            if (ChargeTimer <= OvershootStart) {
                return progress;
            }
            if (ChargeTimer <= ChargeTime) {
                float ot = (ChargeTimer - OvershootStart) / (float)(ChargeTime - OvershootStart);
                return 1f + ot * 0.35f;
            }
            float ft = MathHelper.Clamp((ChargeTimer - ChargeTime) / (float)FadeTime, 0f, 1f);
            return MathHelper.Lerp(0.9f, 0.2f, ft);
        }

        private void SpawnChargeParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            //吸入余烬
            int perFrame = ChargeTimer < RevealEnd
                ? (Main.rand.NextBool(3) ? 1 : 0)
                : ChargeTimer < OvershootStart ? 2 : 3;
            for (int i = 0; i < perFrame; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                //过冲段从更近处生成，保证释放帧前坠入阵心
                float dist = ChargeTimer >= OvershootStart
                    ? Main.rand.NextFloat(90f, 150f)
                    : Main.rand.NextFloat(170f, 270f);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * dist;
                //切向初速
                Vector2 vel = (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f);
                PRTLoader.NewParticle<PRT_FishDemonicHellEmber>(pos, vel,
                    new Color(255, 116, 36), Main.rand.NextFloat(0.55f, 0.95f))
                    ?.ConfigureSuction(Projectile.whoAmI, ChargeTimer >= OvershootStart ? 0.34f : 0.20f);
            }

            //阵心暗核
            if (ChargeTimer >= 18 && ChargeTimer % 6 == 0) {
                PRTLoader.NewParticle<PRT_FishDemonicHellSmoke>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    Main.rand.NextVector2Circular(0.4f, 0.4f) - Vector2.UnitY * 0.3f,
                    new Color(24, 10, 12, 210),
                    Main.rand.NextFloat(0.20f, 0.30f) * (0.7f + progress * 0.6f))
                    ?.Configure(Main.rand.Next(26, 40), Main.rand.NextFloat(-0.02f, 0.02f));
            }
        }

        private void FireBlast() {
            //发射主爆炸弹幕（伤害公式保持不变）
            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitY);
            int damage = (int)(Owner.GetShootState().WeaponDamage * (2f + HalibutData.GetDomainLayer() * 0.5f));
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 6f,
                ModContent.ProjectileType<HellFireBlast>(), damage, 6f, Owner.whoAmI);

            PopFlash = 1f;

            if (VaultUtils.isServer) {
                return;
            }

            //定向后坐震屏（幅度克制）
            if (CWRServerConfig.Instance.ScreenVibration && !Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Projectile.Center, dir, 4f, 5f, 9, 800f, FullName));
            }

            //枪口拍
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center + dir * 24f, Vector2.Zero,
                new Color(255, 150, 60), 0.42f)
                ?.Configure(new Vector2(1.55f, 0.5f), dir.ToRotation(), 1.5f, 14);
            for (int i = 0; i < 12; i++) {
                Vector2 v = dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(5f, 13f);
                PRTLoader.NewParticle<PRT_FishDemonicHellEmber>(Projectile.Center + dir * 18f, v,
                    new Color(255, 128, 42), Main.rand.NextFloat(0.5f, 0.85f))
                    ?.ConfigureFree(Main.rand.Next(14, 24), 0.05f);
            }
            for (int i = 0; i < 4; i++) {
                var prt = PRTLoader.NewParticle<PRT_HellFlame>(Projectile.Center + dir * 12f,
                    dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 5f),
                    Color.White, Main.rand.NextFloat(0.8f, 1.2f));
                if (prt != null) {
                    prt.ai[0] = 1;
                    prt.ai[1] = 1.3f;
                    prt.ai[2] = 26;
                    prt.ai[3] = 44;
                }
            }

            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawRitualRing();
            return false;
        }

        /// <summary>符环 quad</summary>
        private void DrawRitualRing() {
            Effect fx = FishDemonicHellAssets.FishDemonicHellRing;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            float reveal = MathHelper.Clamp(ChargeTimer / (float)RevealEnd, 0f, 1f);
            float erode = ChargeTimer <= ChargeTime + 2 ? 0f
                : MathHelper.Clamp((ChargeTimer - ChargeTime - 2) / (float)FadeTime, 0f, 1f);

            const float quadSize = 470f;
            Vector2 c = Projectile.Center;
            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((c + new Vector2(-quadSize, -quadSize) / 2f).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((c + new Vector2(quadSize, -quadSize) / 2f).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((c + new Vector2(-quadSize, quadSize) / 2f).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((c + new Vector2(quadSize, quadSize) / 2f).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.317f % 1f);
            fx.Parameters["uCharge"]?.SetValue(ChargeVisual());
            fx.Parameters["uReveal"]?.SetValue(reveal);
            fx.Parameters["uErode"]?.SetValue(erode);
            fx.Parameters["uFocus"]?.SetValue(FocusFactor());
            fx.Parameters["uSpin"]?.SetValue(SpinAngle);
            fx.Parameters["uPop"]?.SetValue(PopFlash);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }

    /// <summary>地狱炎爆弹幕</summary>
    internal class HellFireBlast : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const int FlyTime = 24;
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> CrescentSoft01 = null;
        private bool explode;
        private float stripFade = 1f;

        public override void SetStaticDefaults() {
            //22 点 ≈ 22 帧彗尾（无 extraUpdates，速度 6-17px/f → 尾长 150-380px）
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 22;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
        }

        public override void AI() {
            int age = 120 - Projectile.timeLeft; //生成后帧数（timeLeft 同步，各端一致）
            float life = 90 - Projectile.timeLeft; //原速度曲线时间基（前 30 帧为加速段前摇，保持不动）

            if (age == 1) {
                SpawnLaunchBurst();
            }

            if (life < FlyTime) {
                //飞行加速段
                float k = MathHelper.Clamp(age / 54f, 0f, 1f);
                Projectile.scale = MathHelper.Lerp(0.55f, 1.35f, 1f - (1f - k) * (1f - k));
                Projectile.velocity *= 1.02f;
            }
            else {
                Projectile.velocity *= 0.96f;
                if (Projectile.scale < 2f) {
                    Projectile.scale *= 1.01f;
                }

                if (Projectile.timeLeft == 10) {
                    Explode();
                }
            }

            Projectile.rotation += 0.15f;

            if (explode) {
                //爆后余寿
                stripFade *= 0.70f;
                float dim = Projectile.timeLeft / 10f;
                Lighting.AddLight(Projectile.Center, 1.6f * dim, 0.6f * dim, 0.2f * dim);
                return;
            }

            if (!VaultUtils.isServer) {
                SpawnFlightDressing(age, life);
            }

            Lighting.AddLight(Projectile.Center, 1.6f, 0.6f, 0.2f);
        }

        /// <summary>出膛拍</summary>
        private void SpawnLaunchBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_FishDemonicHellEmber>(Projectile.Center,
                    dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 8f),
                    new Color(255, 126, 40), Main.rand.NextFloat(0.45f, 0.75f))
                    ?.ConfigureFree(Main.rand.Next(12, 20), 0.05f);
            }
            PRTLoader.NewParticle<PRT_FishDemonicHellSmoke>(Projectile.Center - dir * 14f,
                -dir * 0.8f, new Color(26, 11, 12, 215), 0.18f)
                ?.Configure(Main.rand.Next(30, 42), Main.rand.NextFloat(-0.02f, 0.02f));
        }

        /// <summary>飞行期持续演出</summary>
        private void SpawnFlightDressing(int age, float life) {
            float speed = Projectile.velocity.Length();

            //余烬剥落，快时每帧，慢时隔帧
            if (Main.rand.NextBool(speed > 11f ? 1 : 2)) {
                PRTLoader.NewParticle<PRT_FishDemonicHellEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f) * Projectile.scale,
                    -Projectile.velocity * 0.18f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                    new Color(255, 118, 36), Main.rand.NextFloat(0.4f, 0.7f))
                    ?.ConfigureFree(Main.rand.Next(16, 26), 0.045f);
            }

            //烟雾尾
            if (age % 5 == 0) {
                Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);
                PRTLoader.NewParticle<PRT_FishDemonicHellSmoke>(
                    Projectile.Center + back * 18f * Projectile.scale + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.06f - Vector2.UnitY * 0.25f,
                    new Color(26, 11, 12, 215),
                    Main.rand.NextFloat(0.15f, 0.24f) * Projectile.scale)
                    ?.Configure(Main.rand.Next(34, 50), Main.rand.NextFloat(-0.025f, 0.025f));
            }

            //廉价底噪，火把尘
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.Torch, -Projectile.velocity * 0.1f, 120,
                    new Color(255, 110, 30), Main.rand.NextFloat(1.0f, 1.6f));
                d.noGravity = true;
            }

            //临爆倒吸
            if (life >= FlyTime && Projectile.timeLeft <= 30 && Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(34f, 56f) * Projectile.scale;
                PRTLoader.NewParticle<PRT_FishDemonicHellEmber>(pos,
                    (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 5f),
                    new Color(255, 130, 45), Main.rand.NextFloat(0.35f, 0.6f))
                    ?.ConfigureFree(Main.rand.Next(10, 15), 0f);
            }
        }

        private void Explode() {
            explode = true;
            //伤害区域扩大（机制保持不变）
            Projectile.Explode(620, default, false);

            if (CWRServerConfig.Instance.ScreenVibration && !Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Projectile.Center, Projectile.velocity.SafeNormalize(Main.rand.NextVector2Unit()),
                    6.5f, 7f, 13, 1100f, FullName));
            }

            if (VaultUtils.isServer) {
                return;
            }

            //双冲击环
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, new Color(255, 185, 85), 0.5f)
                ?.Configure(new Vector2(1f, 1f), 0f, 2.6f, 14);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, new Color(170, 40, 22), 0.3f)
                ?.Configure(new Vector2(1f, 1f), 0f, 3.4f, 26);

            //余烬迸散，顺速度拉丝+重力坠落
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 17f),
                    new Color(255, 120, 38), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(18, 30), 0.06f);
            }

            for (int i = 0; i < 6; i++) {
                var prt = PRTLoader.NewParticle<PRT_HellFlame>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 10f),
                    Color.White, Main.rand.NextFloat(1.0f, 1.6f));
                if (prt != null) {
                    prt.ai[0] = 1;
                    prt.ai[1] = 1.6f;
                    prt.ai[2] = 40;
                    prt.ai[3] = 70;
                }
            }

            //黑烟座
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f);
                PRTLoader.NewParticle<PRT_FishDemonicHellSmoke>(
                    Projectile.Center + Main.rand.NextVector2Circular(24f, 24f), vel,
                    new Color(30, 13, 13, 220), Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(55, 90), Main.rand.NextFloat(-0.03f, 0.03f));
            }

            //余燃 aftermath
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_LavaFire>(
                    Projectile.Center + Main.rand.NextVector2Circular(70f, 70f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f),
                    Color.White, Main.rand.NextFloat(0.4f, 0.8f))
                    ?.SetLifetime(70, 110);
            }

            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 14f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 80,
                    new Color(255, 140, 50), Main.rand.NextFloat(1.3f, 2.1f));
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.85f, Pitch = -0.7f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (explode) {
                return;
            }
            Explode();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 300);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FishDemonicHellEmber>(Projectile.Center,
                    Main.rand.NextVector2Circular(5f, 5f),
                    new Color(255, 124, 40), Main.rand.NextFloat(0.4f, 0.65f))
                    ?.ConfigureFree(Main.rand.Next(10, 16), 0.05f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (explode) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float scale = Projectile.scale;
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 velDir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            float velRot = Projectile.velocity.ToRotation();
            float pulse = (float)Math.Sin(time * 9f + Projectile.whoAmI) * 0.5f + 0.5f;

            //第1层
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                float speedStretch = 1.5f + Projectile.velocity.Length() * 0.02f;
                sb.Draw(glow, center, null, new Color(140, 28, 16, 0) * 0.45f,
                    velRot, glow.Size() / 2f,
                    new Vector2(2.2f * speedStretch, 2.0f) * scale, SpriteEffects.None, 0f);
            }

            //第2层
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            if (smear != null) {
                float sc = 110f * scale / smear.Width;
                sb.Draw(smear, center, null, new Color(255, 105, 38, 0) * 0.42f,
                    Projectile.rotation * 1.6f, smear.Size() / 2f, sc, SpriteEffects.None, 0f);
            }

            //第3层
            Texture2D smokeSheet = CWRAsset.SmokeSheet01?.Value;
            if (smokeSheet != null) {
                int frameSize = smokeSheet.Width / 2;
                int f1 = Projectile.whoAmI % 4;
                int f2 = (Projectile.whoAmI + 2) % 4;
                Rectangle r1 = new(f1 % 2 * frameSize, f1 / 2 * frameSize, frameSize, frameSize);
                Rectangle r2 = new(f2 % 2 * frameSize, f2 / 2 * frameSize, frameSize, frameSize);
                Vector2 origin = new(frameSize * 0.5f);
                float wob = (float)Math.Sin(time * 5f + Projectile.whoAmI) * 3f * scale;
                sb.Draw(smokeSheet, center + new Vector2(wob, -wob * 0.5f), r1,
                    new Color(30, 12, 13, 235), Projectile.rotation,
                    origin, 0.22f * scale, SpriteEffects.None, 0f);
                sb.Draw(smokeSheet, center - new Vector2(wob * 0.7f, wob * 0.4f), r2,
                    new Color(46, 17, 15, 205), -Projectile.rotation * 0.6f,
                    origin, 0.17f * scale, SpriteEffects.None, 0f);
            }

            //第4层
            Texture2D crescent = CrescentSoft01?.Value;
            if (crescent != null) {
                float sc = 105f * scale / crescent.Width * (0.92f + pulse * 0.14f);
                sb.Draw(crescent, center + velDir * 20f * scale, null,
                    new Color(255, 120, 35, 0) * 0.85f,
                    velRot, crescent.Size() / 2f, sc, SpriteEffects.None, 0f);
            }

            //第5层
            if (glow != null) {
                sb.Draw(glow, center + velDir * 8f * scale, null,
                    new Color(255, 150, 55, 0) * 0.85f,
                    0f, glow.Size() / 2f, 0.52f * scale * (0.9f + pulse * 0.16f), SpriteEffects.None, 0f);
                sb.Draw(glow, center + velDir * 10f * scale, null,
                    new Color(255, 208, 110, 0) * 0.95f,
                    0f, glow.Size() / 2f, 0.24f * scale, SpriteEffects.None, 0f);
            }

            return false;
        }

        /// <summary>彗尾条带</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || stripFade < 0.05f) {
                return;
            }
            Effect fx = FishDemonicHellAssets.FishDemonicHellComet;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            //采样点
            Vector2 half = Projectile.Size / 2f;
            Span<Vector2> pts = stackalloc Vector2[1 + Projectile.oldPos.Length];
            int count = 0;
            pts[count++] = Projectile.Center;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    break;
                }
                Vector2 p = Projectile.oldPos[k] + half;
                if (Vector2.DistanceSquared(p, pts[count - 1]) < 4f) {
                    continue;
                }
                pts[count++] = p;
            }
            if (count < 3) {
                return;
            }

            //条带顶点
            float maxWidth = 26f * Projectile.scale;
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float width = maxWidth * (0.55f + 0.45f * MathHelper.Clamp(t / 0.15f, 0f, 1f))
                    * MathF.Pow(1f - t, 0.72f);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , Color.White, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            int age = 120 - Projectile.timeLeft;
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.61f % 1f);
            fx.Parameters["uFade"]?.SetValue(MathHelper.Clamp(age / 10f, 0f, 1f) * stripFade);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }

    /// <summary>地狱余烬</summary>
    internal class PRT_FishDemonicHellEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> StreakTex = null;

        private int attractor;   //吸附目标弹幕索引，-1=自由余烬
        private float accel;     //向心加速度
        private float gravity;
        private float swirlDir;
        private float flickerSeed;

        public PRT_FishDemonicHellEmber ConfigureSuction(int projIndex, float accelStrength) {
            attractor = projIndex;
            accel = accelStrength;
            Lifetime = 90; //到心即灭，此为兜底
            swirlDir = Main.rand.NextBool() ? 1f : -1f;
            return this;
        }

        public PRT_FishDemonicHellEmber ConfigureFree(int lifetime, float gravityStrength) {
            attractor = -1;
            Lifetime = lifetime;
            gravity = gravityStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            attractor = -1;
            accel = 0f;
            gravity = 0f;
            swirlDir = 1f;
            flickerSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            attractor = -1;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(16, 26);
            }
        }

        public override void AI() {
            if (attractor >= 0 && attractor < Main.maxProjectiles) {
                Projectile proj = Main.projectile[attractor];
                if (!proj.active || proj.type != ModContent.ProjectileType<HellRitualCircle>()) {
                    attractor = -1; //阵没了就转自由余烬飘落
                }
                else {
                    Vector2 toC = proj.Center - Position;
                    float dist = toC.Length();
                    if (dist < 15f) {
                        active = false;
                        return;
                    }
                    Vector2 dir = toC / dist;
                    //向心加速+随距衰减的切向分量
                    Velocity += dir * accel * (1f + Time * 0.05f);
                    Vector2 tangent = new(-dir.Y, dir.X);
                    Velocity += tangent * swirlDir * accel * 0.55f * MathHelper.Clamp(dist / 220f, 0.2f, 1f);
                    if (Velocity.Length() > 17f) {
                        Velocity = Velocity.SafeNormalize(Vector2.Zero) * 17f;
                    }
                    Opacity = Math.Min(Time / 5f, 1f)
                        * (0.8f + 0.2f * MathF.Sin(Time * 0.8f + flickerSeed));
                    Scale *= 0.996f;
                    return;
                }
            }

            //自由余烬，急减速后下坠
            Velocity *= 0.92f;
            if (Velocity.Length() < 3f) {
                Velocity.Y += gravity;
            }
            float lc = LifetimeCompletion;
            float flicker = 0.78f + 0.22f * MathF.Sin(Time * 0.9f + flickerSeed);
            Opacity = MathF.Min(lc * 8f, 1f) * (1f - lc * lc) * flicker;
            Scale *= 0.968f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D core = TexValue;
            Texture2D streak = StreakTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //顺速度拉丝，速度快时余烬呈线
            float speed = Velocity.Length();
            if (streak != null && speed > 1.5f) {
                float stretch = MathHelper.Clamp(speed * 0.14f, 0.3f, 1.5f);
                spriteBatch.Draw(streak, pos, null, col * (0.75f * Opacity),
                    Velocity.ToRotation() + MathHelper.PiOver2, streak.Size() * 0.5f,
                    new Vector2(0.22f, stretch) * Scale, SpriteEffects.None, 0f);
            }

            Vector2 origin = core.Size() * 0.5f;
            //同色双层叠亮
            spriteBatch.Draw(core, pos, null, col * (0.55f * Opacity), 0f, origin, 0.3f * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(core, pos, null, col * (0.95f * Opacity), 0f, origin, 0.13f * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>地狱黑烟</summary>
    internal class PRT_FishDemonicHellSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private int frame;

        public PRT_FishDemonicHellSmoke Configure(int lifetime, float rotSpeed) {
            Lifetime = lifetime;
            spin = rotSpeed;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            frame = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            frame = Main.rand.Next(4);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(40, 60);
            }
            if (spin == 0f) {
                spin = Main.rand.NextFloat(-0.02f, 0.02f);
            }
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            //先胀后缓收，热升气流缓慢上浮
            if (lc < 0.25f) {
                Scale *= 1.03f;
            }
            else {
                Scale *= 0.998f;
            }
            Velocity *= 0.94f;
            Velocity.Y -= 0.02f;
            Rotation += spin;
            Opacity = MathF.Min(lc * 6f, 1f) * (1f - lc * lc);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int frameSize = tex.Width / 2;
            Rectangle rect = new(frame % 2 * frameSize, frame / 2 * frameSize, frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, rect, Color * Opacity,
                Rotation, rect.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
