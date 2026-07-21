using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>炼鱼硫火专属资产（域内加载器，不动 EffectLoader）</summary>
    internal class FishDrizzleAssets
    {
        /// <summary>喷吐焰锥（根部锚定鱼嘴的附着式火焰射流）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishDrizzleFlame { get; private set; }
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SoftGlow { get; private set; }
    }

    /// <summary>炼鱼硫火视觉工具</summary>
    internal static class DrizzleVFX
    {
        public static readonly Color EmberHot = new(255, 128, 36);   //余烬亮端
        public static readonly Color EmberDeep = new(198, 52, 16);   //余烬暗端
        public static readonly Color SmokeTint = new(46, 30, 26);    //烟尘染色
        public static readonly Color UnderGlow = new(200, 62, 18);   //咽部底光

        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        /// <summary>伤害宽度包络闭式（与 <see cref="DrizzleFirePillar"/> 迭代 lerp 同形）， 前 25 帧升至 ~0.983，随后 0.92^n 衰减；视觉与判定窗口共用此形状</summary>
        public static float JetWidthEnv(int age) {
            if (age < 0 || age > 85) {
                return 0f;
            }
            return age <= 25
                ? 1f - MathF.Pow(0.85f, age)
                : (1f - MathF.Pow(0.85f, 25f)) * MathF.Pow(0.92f, age - 25);
        }

        /// <summary>焰锥几何长度，点火 7 帧 easeOutBack 过冲伸出</summary>
        public static float JetLen(int age) => 1400f * EaseOutBack(MathHelper.Clamp(age / 7f, 0f, 1f));

        /// <summary>尖端燃烧边界</summary>
        public static float JetULen(float wEnv)
            => MathHelper.Lerp(0.12f, 0.98f, MathHelper.Clamp((wEnv - 0.03f) / 0.72f, 0f, 1f));

        public static float JetPower(int age, float wEnv)
            => MathHelper.Clamp(age / 6f, 0f, 1f) * MathHelper.Clamp(0.35f + wEnv * 0.75f, 0f, 1f);

        /// <summary>熄火断续</summary>
        public static float JetSputter(float wEnv)
            => 1f - MathHelper.Clamp((wEnv - 0.06f) / 0.45f, 0f, 1f);

        public static void SpawnEmber(Vector2 pos, Vector2 vel, float scale, int life, float gravity = 0.06f) {
            Color c = Color.Lerp(EmberDeep, EmberHot, Main.rand.NextFloat());
            PRTLoader.NewParticle<PRT_PallbearerEmber>(pos, vel, c, scale)?.Configure(life, gravity);
        }

        public static void SpawnSmoke(Vector2 pos, Vector2 vel, float scale) {
            var d = Dust.NewDustPerfect(pos, DustID.Smoke, vel, 170, SmokeTint, scale);
            d.noGravity = true;
        }

        /// <summary>焰锥条带</summary>
        public static void DrawFlameCone(Vector2 root, float rotation, float len, float widthScale
            , float uLen, float uPower, float uSputter, float fade, float seed) {
            Effect fx = FishDrizzleAssets.FishDrizzleFlame;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || len < 8f || fade <= 0.02f || widthScale <= 0.01f) {
                return;
            }

            const int Samples = 24;
            var verts = new VertexPositionColorTexture[Samples * 2];
            Vector2 dir = rotation.ToRotationVector2();
            Vector2 perp = new(-dir.Y, dir.X);
            for (int i = 0; i < Samples; i++) {
                float u = i / (float)(Samples - 1);
                float halfW = MathHelper.Lerp(9f, 96f, MathF.Pow(u, 0.62f)) * widthScale * 1.35f;
                Vector2 c = root + dir * (u * len);
                verts[i * 2] = new VertexPositionColorTexture((c + perp * halfW).ToVector3()
                    , Color.White, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((c - perp * halfW).ToVector3()
                    , Color.White, new Vector2(u, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uLen"]?.SetValue(uLen);
            fx.Parameters["uPower"]?.SetValue(uPower);
            fx.Parameters["uSputter"]?.SetValue(uSputter);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }
        }
    }

    internal class FishDrizzle : FishSkill
    {
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Fire = null;//火焰的纹理灰度图，总共4*4帧，也就是四行四列的帧图
        public override int UnlockFishID => CWRID.Item_DragoonDrizzlefish;
        public override int DefaultCooldown => 480 - HalibutData.GetDomainLayer() * 24;
        public override int ResearchDuration => 60 * 16;

        internal static int DepartureDelay => 90 - (HalibutData.GetDomainLayer() * 3);
        internal static int DepartureDuration => 90 - (HalibutData.GetDomainLayer() * 3);

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (Cooldown <= 0 && !HasActiveDrizzle(player)) {
                TriggerDrizzleVolley(item, player, hp);
                SetCooldown();
            }
            return null;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            return !HasActiveDrizzle(player);
        }

        private static bool HasActiveDrizzle(Player player) {
            return player.CountProjectilesOfID<DrizzleFishHolder>() > 0;
        }

        private static void TriggerDrizzleVolley(Item item, Player player, HalibutPlayer hp) {
            //仅持有者本地，Shoot触发
            //所有 Projectile.NewProjectile 由本地玩家创建后会通过 NetMessage 自动同步到其它端
            int fishCount = 3 + HalibutData.GetDomainLayer() / 2;

            //使用同步过的鼠标方向（HalibutPlayer.MouseWorld 由 InnoVault PlayerNetwork 提供）
            Vector2 aimDir = (hp.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
            float arc = MathHelper.ToRadians(140f);
            float radius = 110f;
            ShootState shootState = player.GetShootState();
            //shootDir 完全由 aimDir.X 符号推导（确定性），所有端都能算出同样的扇形朝向
            sbyte shootDir = aimDir.X >= 0 ? (sbyte)1 : (sbyte)-1;

            //中心火焰爆发特效
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, Vector2.Zero
                , ModContent.ProjectileType<DrizzleSpawnEffect>(), 0, 0f, player.whoAmI, -1, 0);

            for (int i = 0; i < fishCount; i++) {
                float t = fishCount == 1 ? 0.5f : i / (float)(fishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff * shootDir * -1);
                Vector2 spawnPos = player.Center + offsetDir * radius;

                //初始velocity跨端传AimDirection
                int proj = Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, aimDir,
                    ModContent.ProjectileType<DrizzleFishHolder>(), shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI,
                    ai0: i, ai1: fishCount);

                if (Main.projectile.IndexInRange(proj)) {
                    //鱼体出现火焰特效（ai0 = 鱼弹幕identity，由弹幕同步保留），通过 identity 跨端定位
                    Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, Vector2.Zero
                        , ModContent.ProjectileType<DrizzleSpawnEffect>(), 0, 0f, player.whoAmI, Main.projectile[proj].identity, 0);
                }
            }

            SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f, Volume = 0.7f }, player.Center);
        }
    }

    /// <summary>硫火鱼出现特效</summary>
    internal class DrizzleSpawnEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";

        private ref float Index => ref Projectile.ai[0];
        private const int LifeTime = 38;
        private float seed;
        private bool IsCenterBurst => Index < 0;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.alpha = 0;
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.NextFloat(10000f);
            if (VaultUtils.isServer) {
                return;
            }

            //点燃爆发
            int embers = IsCenterBurst ? 22 : 9;
            float speed = IsCenterBurst ? 8.5f : 5f;
            for (int i = 0; i < embers; i++) {
                Vector2 v = Main.rand.NextVector2Unit() * speed * Main.rand.NextFloat(0.35f, 1.15f);
                DrizzleVFX.SpawnEmber(Projectile.Center, v, Main.rand.NextFloat(0.5f, 0.95f), Main.rand.Next(18, 32));
            }
            int smokes = IsCenterBurst ? 10 : 5;
            for (int i = 0; i < smokes; i++) {
                DrizzleVFX.SpawnSmoke(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(1.6f, 1.6f) - Vector2.UnitY * 0.8f, Main.rand.NextFloat(1.1f, 1.9f));
            }
            int dusts = IsCenterBurst ? 8 : 4;
            for (int i = 0; i < dusts; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 150
                    , Color.Lerp(DrizzleVFX.EmberDeep, DrizzleVFX.EmberHot, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }

        public override void AI() {
            float t = 1f - Projectile.timeLeft / (float)LifeTime;
            Projectile.scale = MathHelper.Lerp(0.4f, IsCenterBurst ? 3.2f : 1.8f, MathF.Pow(t, 0.6f));

            if (t > 0.75f) {
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, (t - 0.75f) / 0.25f);
            }

            if (!VaultUtils.isServer) {
                if (IsCenterBurst && t < 0.5f && Main.rand.NextBool(3)) {
                    //扩散环缘的迟到余烬
                    Vector2 ringPos = Projectile.Center + Main.rand.NextVector2CircularEdge(Projectile.scale * 22f, Projectile.scale * 22f);
                    DrizzleVFX.SpawnEmber(ringPos, (ringPos - Projectile.Center).SafeNormalize(Vector2.UnitX) * 1.6f
                        , Main.rand.NextFloat(0.35f, 0.6f), Main.rand.Next(14, 24));
                }
                if (!IsCenterBurst && t < 0.45f && Main.rand.NextBool(5)) {
                    DrizzleVFX.SpawnEmber(Projectile.Center, Main.rand.NextVector2Circular(2f, 2f)
                        , Main.rand.NextFloat(0.3f, 0.55f), Main.rand.Next(12, 20));
                }
            }

            //通过 identity 跨端定位关联的鱼弹幕；identity 在所有客户端保持一致
            if (Index.TryGetProjectile(out var fish)) {
                Projectile.Center = fish.Center + fish.rotation.ToRotationVector2() * 30;
                if (fish.ai[2] == 0 && Projectile.timeLeft < LifeTime / 2) {
                    Projectile.timeLeft = LifeTime / 2;
                }
            }
            else if (Projectile.owner.TryGetPlayer(out var owner)) {
                Projectile.Center = owner.Center;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float t = 1f - Projectile.timeLeft / (float)LifeTime;
            float fade = 1f - Projectile.alpha / 255f;

            //≤2 帧过曝小闪
            int age = LifeTime - Projectile.timeLeft;
            if (age < 2) {
                Color flash = Color.Lerp(DrizzleVFX.EmberHot, Color.White, 0.4f) * (1f - age * 0.35f);
                flash.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, flash, seed, origin
                    , IsCenterBurst ? 0.9f : 0.55f, SpriteEffects.None, 0f);
            }

            //残留底光
            float under = MathF.Pow(1f - t, 2f) * 0.35f * fade;
            if (under > 0.02f) {
                Color glow = DrizzleVFX.UnderGlow * under;
                glow.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, glow, 0f, origin
                    , Projectile.scale * 0.55f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    internal class DrizzleFishHolder : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[0] = FishIndex（自动同步）
        //ai[1] = TotalFishCount（自动同步）
        //ai[2] = Fired 标志（自动同步，供 DrizzleSpawnEffect 检测）
        //localAI[0] = 离场累计位移（确定性，无需同步）

        /// <summary>齐射时的瞄准方向（单位向量），由 OnSpawn 从初始 velocity 中读取</summary>
        public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

        /// <summary>扇形展开方向，完全由 AimDirection.X 符号推导（确定性，跨端一致）</summary>
        public sbyte ShootDir => AimDirection.X >= 0 ? (sbyte)1 : (sbyte)-1;

        /// <summary>本地确定性计时，从 0 每帧 +1 各端生成时刻一致故自然同步；持有者每 60 帧 netUpdate 兜底</summary>
        public int LocalTimer;

        public int FishIndex => (int)Projectile.ai[0];
        public int TotalFishCount => Math.Max(1, (int)Projectile.ai[1]);
        internal bool Fired {
            get => Projectile.ai[2] == 1f;
            set => Projectile.ai[2] = value ? 1f : 0f;
        }

        private const int PreFireDelay = 18;
        private const int FireInterval = 16;
        //火柱寿命85帧+冗余，等全消散
        private const int PillarLifetime = 90;

        private float glowPulse;
        private float fadeOut;

        private float visualScale = 0.42f; //出场 easeOutBack 缩放
        private float recoil;              //点火后坐位移
        private float mouthFlash;          //嘴部过曝闪，每帧减半 ≤2 帧可见
        private float pilotLen;            //待机火苗长度
        private float pilotPower;          //待机火苗强度
        private bool departFlying;         //离场飞行中（残影链开关）

        private int FishFireTime => PreFireDelay + FishIndex * FireInterval;
        /// <summary>喷射龄，负值未点火，0 为点火帧</summary>
        private int ConeAge => LocalTimer - FishFireTime;
        private Vector2 MouthPos => Projectile.Center + Projectile.rotation.ToRotationVector2() * 30f * visualScale;
        private float ConeSeed => (FishIndex * 0.173f + 0.05f) % 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40; Projectile.height = 40;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void OnSpawn(IEntitySource source) {
            //生成包会把初始 velocity 同步到所有端，因此能在 OnSpawn 中得到一致的 AimDirection
            if (Projectile.velocity.LengthSquared() > 0.001f) {
                AimDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            }
            //鱼是静止漂浮，需要清空 velocity 防止被基类位置更新逻辑推走
            Projectile.velocity = Vector2.Zero;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((short)LocalTimer);
            //AimDirection 由 OnSpawn 中的 velocity 保证一致，但持有者偶发的 netUpdate 也带上一份做兜底
            writer.WriteVector2(AimDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            LocalTimer = reader.ReadInt16();
            Vector2 dir = reader.ReadVector2();
            if (dir.LengthSquared() > 0.5f) {
                AimDirection = dir.SafeNormalize(AimDirection);
            }
        }

        public override void AI() {
            //owner 由弹幕本身的 owner 字段决定，已是各端一致的玩家索引
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) { Projectile.Kill(); return; }
            if (owner.TryGetHalibutPlayer(out var halibutPlayer)
                && halibutPlayer.TryGetMouseWorld(out Vector2 mouseWorld)) {
                AimDirection = owner.To(mouseWorld).UnitVector();
            }
            LocalTimer++;
            glowPulse = (float)Math.Sin(LocalTimer * 0.28f + FishIndex) * 0.5f + 0.5f;

            UpdateVisualBeats();

            int fishFireTime = FishFireTime;
            int allFireTime = PreFireDelay + (TotalFishCount - 1) * FireInterval;
            //最后一条鱼开火后再等火柱寿命的时间，全体进入离场阶段（确定性，无需共享状态）
            int departureStartTime = allFireTime + PillarLifetime;
            bool inDeparturePhase = LocalTimer >= departureStartTime;

            if (!inDeparturePhase) {
                //位置由同步过的 AimDirection 推算，跨端一致
                Vector2 behind = (-AimDirection).SafeNormalize(Vector2.UnitX);
                float arc = MathHelper.ToRadians(140f);
                float radius = 210f;
                float t = TotalFishCount <= 1 ? 0.5f : FishIndex / (float)(TotalFishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff * ShootDir * -1);
                Vector2 basePos = owner.Center + offsetDir * radius;
                float bob = (float)Math.Sin(LocalTimer * 0.09f + FishIndex) * 8f;
                //点火后坐
                Projectile.Center = Vector2.Lerp(Projectile.Center
                    , basePos + new Vector2(0, bob) - AimDirection * recoil, 0.28f);

                //朝向使用同步的 AimDirection 来确定一个远点（避免依赖各端不一致的 Main.MouseWorld）
                Vector2 aimToward = owner.Center + AimDirection * 1500f;
                Projectile.rotation = Projectile.To(aimToward).ToRotation();

                //仅持有者执行开火逻辑，火柱通过 NetMessage 自动同步给其它端
                if (!Fired && LocalTimer >= fishFireTime && Projectile.IsOwnedByLocalPlayer()) {
                    Fired = true;
                    FirePillar();
                    Projectile.netUpdate = true;
                }
            }
            else {
                int departureTimer = LocalTimer - departureStartTime;

                //先等待一段时间再真正离场
                if (departureTimer < FishDrizzle.DepartureDelay) {
                    //原地轻微浮动
                    Projectile.rotation += 0.02f * (FishIndex % 2 == 0 ? 1 : -1);
                    float idleBob = (float)Math.Sin(LocalTimer * 0.1f + FishIndex) * 4f;
                    Projectile.Center += new Vector2(0, idleBob * 0.05f);
                }
                else {
                    int flyTime = departureTimer - FishDrizzle.DepartureDelay;
                    float progress = MathHelper.Clamp(flyTime / (float)FishDrizzle.DepartureDuration, 0f, 1f);
                    progress = MathF.Pow(progress, 0.65f);

                    //外向方向出生参数定，跨端一致
                    Vector2 behind = (-AimDirection).SafeNormalize(Vector2.UnitX);
                    float arc = MathHelper.ToRadians(140f);
                    float t = TotalFishCount <= 1 ? 0.5f : FishIndex / (float)(TotalFishCount - 1);
                    float angOff = (t - 0.5f) * arc;
                    Vector2 outward = behind.RotatedBy(angOff * ShootDir * -1).SafeNormalize(Vector2.UnitY);

                    float baseSpeed = MathHelper.Lerp(3f, 18f, progress);
                    baseSpeed *= 1f + 0.15f * (float)Math.Sin(flyTime * 0.18f + FishIndex);

                    Vector2 move = outward * baseSpeed;
                    Projectile.Center += move;

                    Projectile.localAI[0] += move.Length();
                    departFlying = true;

                    fadeOut = MathHelper.Clamp((progress - 0.5f) / 0.5f, 0f, 1f);

                    //离场撒烬，飞行轨迹上剥落余烬
                    if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                        DrizzleVFX.SpawnEmber(Projectile.Center - move * 0.5f
                            , -outward * 1.2f + Main.rand.NextVector2Circular(1f, 1f)
                            , Main.rand.NextFloat(0.3f, 0.55f) * (1f - fadeOut), Main.rand.Next(14, 22));
                    }

                    //使用一个固定的离场距离，避免依赖各端屏幕尺寸
                    const float exitDistance = 3000f;
                    if (Projectile.localAI[0] >= exitDistance || fadeOut >= 0.99f) {
                        Projectile.Kill();
                        return;
                    }
                }
            }

            UpdateJetParticles();

            Projectile.spriteDirection = Projectile.rotation.ToRotationVector2().X > 0 ? 1 : -1;

            //持有者每 60 帧广播一次状态，缓解长生命周期下可能的累积漂移
            if (Projectile.IsOwnedByLocalPlayer() && LocalTimer > 0 && LocalTimer % 60 == 0) {
                Projectile.netUpdate = true;
            }
        }

        /// <summary>视觉节拍</summary>
        private void UpdateVisualBeats() {
            //出场
            float baseScale = MathHelper.Lerp(0.42f, 1f, DrizzleVFX.EaseOutBack(MathHelper.Clamp(LocalTimer / 14f, 0f, 1f)));

            int untilFire = FishFireTime - LocalTimer;
            int coneAge = ConeAge;

            //待机火苗
            pilotLen = 26f + 6f * MathF.Sin(LocalTimer * 0.11f + FishIndex * 1.7f);
            pilotPower = 0.5f;
            float beatScale = 1f;
            if (untilFire > 0 && untilFire <= 12) {
                float inhale = (12 - untilFire) / 12f;
                pilotLen += inhale * 36f;
                pilotPower += inhale * 0.4f;
                beatScale = MathHelper.Lerp(1f, 0.955f, inhale);//吸气微缩
            }
            if (coneAge >= 0 && coneAge < 4) {
                beatScale = 1.07f - coneAge * 0.02f;//释放过冲回落
            }
            visualScale = baseScale * beatScale;

            if (coneAge == 0) {
                //点火拍
                recoil = 13f;
                mouthFlash = 1f;
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.75f, Pitch = -0.15f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
                if (!VaultUtils.isServer) {
                    //点火喷发，嘴口锥形余烬 + 烟
                    Vector2 dir = Projectile.rotation.ToRotationVector2();
                    for (int i = 0; i < 10; i++) {
                        DrizzleVFX.SpawnEmber(MouthPos, dir.RotatedByRandom(0.38) * Main.rand.NextFloat(6f, 13f)
                            , Main.rand.NextFloat(0.45f, 0.85f), Main.rand.Next(16, 28));
                    }
                    for (int i = 0; i < 4; i++) {
                        DrizzleVFX.SpawnSmoke(MouthPos, dir.RotatedByRandom(0.6) * Main.rand.NextFloat(1f, 3f)
                            - Vector2.UnitY * 0.6f, Main.rand.NextFloat(1f, 1.6f));
                    }
                }
            }
            else if (untilFire == 12) {
                //吸气起点的进气声
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.28f, Pitch = 0.55f }, Projectile.Center);
            }
            recoil *= 0.86f;
            mouthFlash *= 0.5f;
        }

        /// <summary>喷射期余烬剥离与熄火余烟（client-only）</summary>
        private void UpdateJetParticles() {
            if (VaultUtils.isServer || fadeOut > 0.9f) {
                return;
            }
            int coneAge = ConeAge;
            float wEnv = DrizzleVFX.JetWidthEnv(coneAge);

            if (wEnv > 0.12f) {
                //焰锥中后段剥离余烬
                float len = DrizzleVFX.JetLen(coneAge);
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                Vector2 perp = new(-dir.Y, dir.X);
                int shed = wEnv < 0.5f ? (Main.rand.NextBool(3) ? 2 : 1) : (Main.rand.NextBool() ? 2 : 1);
                for (int i = 0; i < shed; i++) {
                    float u = Main.rand.NextFloat(0.15f, 0.85f);
                    Vector2 pos = MouthPos + dir * (u * len) + perp * Main.rand.NextFloat(-0.35f, 0.35f) * 96f * wEnv;
                    Vector2 vel = dir * Main.rand.NextFloat(6f, 11f) * (1f - u * 0.45f)
                        + perp * Main.rand.NextFloat(-1.4f, 1.4f);
                    DrizzleVFX.SpawnEmber(pos, vel, Main.rand.NextFloat(0.4f, 0.8f), Main.rand.Next(18, 30));
                }
                //嘴部火光照明
                Lighting.AddLight(MouthPos, 0.9f, 0.34f, 0.08f);
            }
            else {
                if (coneAge > 0 && coneAge < 130 && Main.rand.NextBool(6)) {
                    //喷息余韵
                    DrizzleVFX.SpawnSmoke(MouthPos, -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.4f)
                        + Main.rand.NextVector2Circular(0.4f, 0.4f), Main.rand.NextFloat(0.8f, 1.4f));
                }
                if (Main.rand.NextBool(9)) {
                    //待机火苗滴落余烬
                    DrizzleVFX.SpawnEmber(MouthPos, Projectile.rotation.ToRotationVector2() * 1.6f
                        + Main.rand.NextVector2Circular(0.8f, 0.8f), Main.rand.NextFloat(0.28f, 0.5f)
                        , Main.rand.Next(14, 22), 0.08f);
                }
                Lighting.AddLight(MouthPos, 0.4f, 0.14f, 0.03f);
            }
        }

        private void FirePillar() {
            //发射方向使用同步过的 AimDirection，保证火柱朝向跨端一致
            Vector2 dir = AimDirection.SafeNormalize(Vector2.UnitX);
            int damage = (int)(Projectile.damage * (0.6f + HalibutData.GetDomainLayer() * 0.15f));

            int beam = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 12f, dir * 0.1f,
                ModContent.ProjectileType<DrizzleFirePillar>(), damage, Projectile.knockBack * 1.6f, Projectile.owner, Projectile.identity);
            if (Main.projectile.IndexInRange(beam)) {
                Main.projectile[beam].rotation = dir.ToRotation();
                Main.projectile[beam].netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value;
            if (CWRID.Item_DragoonDrizzlefish > 0) {
                value = TextureAssets.Item[CWRID.Item_DragoonDrizzlefish].Value;//获取鱼的纹理
            }
            else {
                Main.instance.LoadItem(ItemID.CrimsonTigerfish);
                value = TextureAssets.Item[ItemID.CrimsonTigerfish].Value;//获取鱼的纹理
            }
            float opacity = 1f - fadeOut;
            if (opacity <= 0.02f) {
                return false;
            }

            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame();
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + MathHelper.PiOver4;
            float breathScale = visualScale * (1f + glowPulse * 0.04f);

            int coneAge = ConeAge;
            float wEnv = DrizzleVFX.JetWidthEnv(coneAge);
            bool jetAlive = coneAge >= 0 && wEnv > 0.02f;
            Vector2 mouth = MouthPos;
            float rot = Projectile.rotation;

            Main.spriteBatch.End();
            if (jetAlive) {
                DrizzleVFX.DrawFlameCone(mouth, rot, DrizzleVFX.JetLen(coneAge), wEnv
                    , DrizzleVFX.JetULen(wEnv), DrizzleVFX.JetPower(coneAge, wEnv)
                    , DrizzleVFX.JetSputter(wEnv), opacity, ConeSeed);
            }
            else {
                //待机火苗
                DrizzleVFX.DrawFlameCone(mouth, rot, pilotLen * visualScale, 0.16f
                    , 0.9f, pilotPower, 0.15f, 0.85f * opacity, ConeSeed);
            }
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D glowTex = FishDrizzleAssets.SoftGlow?.Value;
            if (glowTex != null) {
                Vector2 gullet = Projectile.Center + rot.ToRotationVector2() * 12f * visualScale;
                Color under = DrizzleVFX.UnderGlow * (0.30f * opacity * (0.7f + 0.3f * glowPulse));
                under.A = 0;
                Main.spriteBatch.Draw(glowTex, gullet - Main.screenPosition, null, under
                    , 0f, glowTex.Size() * 0.5f, 0.5f * visualScale, SpriteEffects.None, 0f);
            }

            if (departFlying) {
                //暗红剪影残影
                for (int k = 2; k < 8; k += 2) {
                    Vector2 gp = Projectile.oldPos[k];
                    if (gp == Vector2.Zero) {
                        continue;
                    }
                    float ga = (0.30f - k * 0.034f) * opacity;
                    Color gc = new Color(120, 38, 18) * ga;
                    Main.spriteBatch.Draw(value, gp + Projectile.Size * 0.5f - Main.screenPosition, sourceRect
                        , gc, Projectile.oldRot[k] + MathHelper.PiOver4, origin, breathScale, SpriteEffects.None, 0f);
                }
            }
            Color bodyCol = Color.Lerp(lightColor, Color.White, 0.75f) * opacity;
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, bodyCol, drawRotation, origin
                , breathScale, SpriteEffects.None, 0f);

            if (opacity > 0.05f) {
                Main.spriteBatch.End();
                if (jetAlive) {
                    DrizzleVFX.DrawFlameCone(mouth - rot.ToRotationVector2() * 6f, rot
                        , MathF.Min(DrizzleVFX.JetLen(coneAge) * 0.10f, 140f)
                        , MathF.Max(wEnv * 0.35f, 0.10f), 0.85f
                        , DrizzleVFX.JetPower(coneAge, wEnv) * 0.8f, DrizzleVFX.JetSputter(wEnv)
                        , 0.55f * opacity, ConeSeed + 0.31f);
                }
                else {
                    DrizzleVFX.DrawFlameCone(mouth - rot.ToRotationVector2() * 6f, rot
                        , pilotLen * 0.6f * visualScale, 0.10f, 0.85f, pilotPower * 0.8f
                        , 0.15f, 0.5f * opacity, ConeSeed + 0.31f);
                }
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            if (mouthFlash > 0.2f && FishDrizzle.Fire != null) {
                Texture2D fireTex = FishDrizzle.Fire;
                int fi = LocalTimer % 16;
                Rectangle frame = new(fi % 4 * (fireTex.Width / 4), fi / 4 * (fireTex.Height / 4)
                    , fireTex.Width / 4, fireTex.Height / 4);
                Color flash = Color.Lerp(DrizzleVFX.EmberHot, Color.White, 0.5f) * mouthFlash;
                flash.A = 0;
                Main.spriteBatch.Draw(fireTex, mouth - Main.screenPosition, frame, flash
                    , rot, frame.Size() * 0.5f, 0.6f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>硫火柱弹幕，判定与增益承载体 视觉焰锥由 <see cref="DrizzleFishHolder"/> 以确定性时序绘制（保证夹心层序），本体不绘制 宽度包络（决定判定粗细）与 <see cref="DrizzleVFX.JetWidthEnv"/> 闭式同形</summary>
    internal class DrizzleFirePillar : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float pillarWidth = 0f;
        private float targetWidth = 140f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.timeLeft = 85;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * 2800, pillarWidth * 2f, ref p);
        }

        public override void AI() {
            //通过同步的 identity 找到对应的鱼弹幕，使火柱跟随鱼的位置和朝向
            if (Projectile.ai[0].TryGetProjectile(out var projectile)) {
                Projectile.Center = projectile.Center;
                Projectile.rotation = projectile.rotation;
            }

            //火柱宽度动画（判定包络）
            if (Projectile.timeLeft > 60) {
                pillarWidth = MathHelper.Lerp(pillarWidth, targetWidth, 0.15f);
            }
            else {
                pillarWidth *= 0.92f;
            }

            Projectile.localAI[0] += 1f;

            //沿射流的硫火照明
            float lightPulse = (float)Math.Sin(Projectile.localAI[0] * 0.3f) * 0.2f + 1f;
            float wNorm = pillarWidth / targetWidth;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 4; i++) {
                float along = i / 3f;
                Vector2 lp = Projectile.Center + dir * (along * 1200f * wNorm);
                float falloff = 1f - along * 0.55f;
                Lighting.AddLight(lp, 1.1f * lightPulse * falloff * wNorm
                    , 0.4f * lightPulse * falloff * wNorm, 0.1f * lightPulse * falloff * wNorm);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240 + HalibutData.GetDomainLayer() * 30);

            if (VaultUtils.isServer) {
                return;
            }
            //命中迸溅
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 5; i++) {
                Vector2 pos = target.Center + Main.rand.NextVector2Circular(target.width * 0.35f, target.height * 0.35f);
                DrizzleVFX.SpawnEmber(pos, dir * Main.rand.NextFloat(2f, 5f) + Main.rand.NextVector2Circular(3f, 3f)
                    , Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(14, 24));
            }
            for (int i = 0; i < 3; i++) {
                DrizzleVFX.SpawnSmoke(target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f)
                    , dir * Main.rand.NextFloat(0.5f, 1.5f) - Vector2.UnitY * 0.5f, Main.rand.NextFloat(0.9f, 1.5f));
            }
        }

        //视觉焰锥由 DrizzleFishHolder 绘制，判定体不出图
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
