using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishSparkling : FishSkill
    {
        internal const float RoingArc = 160f;
        public override int DefaultCooldown => 300 - 24 * HalibutData.GetDomainLayer();
        public override int ResearchDuration => 60 * 12;
        internal static int DepartureDelay => 90 - (HalibutData.GetDomainLayer() * 5);//全部发射后延迟进入离场
        internal static int DepartureDuration => 90 - (HalibutData.GetDomainLayer() * 5);//离场动画时长
        public override int UnlockFishID => CWRID.Item_SparklingEmpress;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var hp = player.GetOverride<HalibutPlayer>();
            TryTriggerSparklingVolley(item, player, hp);
            return null;
        }
        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            //仅依据存活的鱼来判断是否处于齐射状态，避免依赖未同步的玩家状态字段
            //同时不再使用 SparklingVolleyActive / SparklingVolleyTimer，所有齐射状态均存在弹幕本体上
            bool hasSparklingFish = player.CountProjectilesOfID<SparklingFishHolder>() > 0;
            return !hasSparklingFish;
        }
        internal void TryTriggerSparklingVolley(Item item, Player player, HalibutPlayer hp) {
            //该方法只在持有玩家的本地客户端运行（Shoot 由该客户端触发）
            //所有 Projectile.NewProjectile 由本地玩家创建后会通过 NetMessage 自动同步到其它端
            if (player.CountProjectilesOfID<SparklingFishHolder>() > 0) {
                return;
            }
            if (Cooldown > 0) {
                return;
            }

            SetCooldown();
            int fishCount = 4 + HalibutData.GetDomainLayer(); // 4+领域数量鱼

            //使用同步过的鼠标方向（HalibutPlayer.MouseWorld 已通过专用包广播给其它端）
            Vector2 aimDir = (hp.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
            float arc = MathHelper.ToRadians(RoingArc); //扇形总角度
            float radius = 90f;
            ShootState shootState = player.GetShootState();
            //shootDir 完全由 aimDir.X 符号推导（确定性），所有端都能算出同样的扇形朝向
            sbyte shootDir = aimDir.X >= 0 ? (sbyte)1 : (sbyte)-1;

            //中心涟漪出现特效（ai0 = -1 表示中央扩散光环）
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, Vector2.Zero
                , ModContent.ProjectileType<SparklingSpawnEffect>(), 0, 0f, player.whoAmI, -1, 0);

            for (int i = 0; i < fishCount; i++) {
                float t = fishCount == 1 ? 0.5f : i / (float)(fishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff * shootDir * -1);
                Vector2 spawnPos = player.Center + offsetDir * radius;

                //初始 velocity 用于在所有端 OnSpawn 阶段携带 AimDirection（生成包会同步 velocity）
                int proj = Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, aimDir,
                    ModContent.ProjectileType<SparklingFishHolder>(), shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI,
                    ai0: i, ai1: fishCount);

                if (Main.projectile.IndexInRange(proj)) {
                    //鱼体出现定位点爆闪（ai0 = 鱼弹幕identity，由弹幕同步保留），通过 identity 跨端定位
                    Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, Vector2.Zero
                        , ModContent.ProjectileType<SparklingSpawnEffect>(), 0, 0f, player.whoAmI, Main.projectile[proj].identity, 0);
                }
            }
            SoundEngine.PlaySound(SoundID.Item92 with { Pitch = -0.4f }, player.Center); //预热音
        }
    }

    internal class SparklingSpawnEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";//一个圆点光效灰度图，可以考虑用来丰富特效

        private ref float Index => ref Projectile.ai[0]; //-1 = 中心光环 其他=鱼弹幕identity
        private const int LifeTime = 42; //存活时间
        private float seed;
        private float startScale;
        private float endScale;
        private Color colA;
        private Color colB;

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
            if (Index < 0) { //中心扩散
                startScale = 0.4f;
                endScale = 4.2f;
            }
            else {
                startScale = 0.2f;
                endScale = 1.8f + Main.rand.NextFloat(0.4f);
            }
            float hue = Index < 0 ? 0.15f : Index % 7 / 7f;
            //粉蓝宝石色系插值
            colA = Color.Lerp(new Color(120, 180, 255), new Color(255, 170, 230), 0.35f + 0.4f * hue);
            colB = Color.Lerp(new Color(80, 120, 210), new Color(255, 120, 210), 0.55f * (1 - hue) + 0.2f);

            //初生碎光
            int dustAmt = Index < 0 ? 36 : 12;
            for (int i = 0; i < dustAmt; i++) {
                float rot = MathHelper.TwoPi * i / dustAmt;
                Vector2 dVel = rot.ToRotationVector2() * (Index < 0 ? 6f : 3.2f) * Main.rand.NextFloat(0.4f, 1.15f);
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.GemSapphire : DustID.GemAmethyst, dVel, 150,
                    Color.Lerp(colA, colB, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.4f));
                d.noGravity = true;
            }
        }

        public override void AI() {
            float t = 1f - Projectile.timeLeft / (float)LifeTime; //0->1
            float ease = MathF.Pow(t, 0.6f);
            Projectile.scale = MathHelper.Lerp(startScale, endScale, ease);

            //轻微脉动旋转
            Projectile.rotation += 0.04f + (Index < 0 ? 0.02f : 0.06f) * MathF.Sin(seed + Main.GlobalTimeWrappedHourly * 6f);

            //中心光环：持续生成少量向外渐隐宝石尘
            if (Index < 0 && Main.rand.NextBool(4)) {
                Vector2 ringPos = Projectile.Center + Main.rand.NextVector2CircularEdge(Projectile.scale * 18f, Projectile.scale * 18f);
                var d = Dust.NewDustPerfect(ringPos, DustID.GemDiamond, Vector2.Zero, 160, Color.White, Main.rand.NextFloat(0.5f, 0.9f));
                d.noGravity = true;
            }

            //鱼单点闪烁：前半段放射 outward 亮点
            if (Index >= 0 && t < 0.45f && Main.rand.NextBool(5)) {
                Vector2 dir = Main.rand.NextVector2Unit();
                var d2 = Dust.NewDustPerfect(Projectile.Center + dir * Projectile.scale * 12f, DustID.GemDiamond, dir * 2f, 120,
                    Color.Lerp(colA, colB, Main.rand.NextFloat()), Main.rand.NextFloat(0.6f, 1.1f));
                d2.noGravity = true;
            }

            //末段淡出
            if (t > 0.75f) {
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, (t - 0.75f) / 0.25f);
            }

            //通过 identity 跨端定位关联的鱼弹幕；identity 在所有客户端保持一致
            if (Index.TryGetProjectile(out var fash)) {
                Projectile.Center = fash.Center + fash.rotation.ToRotationVector2() * 32;
                if (fash.ai[2] == 0 && Projectile.timeLeft < LifeTime / 2) {
                    Projectile.timeLeft = LifeTime / 2;
                }
            }
            else if (Projectile.owner.TryGetPlayer(out var owner)) {
                Projectile.Center = owner.Center;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRUtils.GetT2DAsset(Texture).Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float fade = 1f - Projectile.alpha / 255f;
            //双层叠加：外层柔光 + 内层核心
            Color outer = Color.Lerp(colA, colB, 0.5f) * 0.55f * fade;
            outer.A = 0;
            Color inner = Color.White * 0.9f * fade;
            inner.A = 0;
            float scaleOuter = Projectile.scale * (Index < 0 ? 1.4f : 1.1f);
            Main.spriteBatch.Draw(tex, drawPos, null, outer, Projectile.rotation, origin, scaleOuter, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, inner, -Projectile.rotation * 0.6f, origin, Projectile.scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 单条闪光皇后鱼的承载弹幕，静止环绕并按顺序发射激光
    /// 多人模式下采用持有者权威 + 确定性本地推进的混合策略，
    /// 关键状态（AimDirection、ShootDir、本地计时）通过 OnSpawn 与 SendExtraAI 跨端同步
    /// </summary>
    internal class SparklingFishHolder : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Item + "Fishing/SunkenSeaCatches/SparklingEmpress";

        //ai[0] = FishIndex（自动同步）
        //ai[1] = TotalFishCount（自动同步）
        //ai[2] = Fired 标志（自动同步，供 SparklingSpawnEffect 检测）
        //localAI[0] = 离场累计位移（确定性，无需同步）

        /// <summary>
        /// 齐射时的瞄准方向（单位向量），由 OnSpawn 从初始 velocity 中读取
        /// 各端均能在弹幕生成包到达时立即得到一致值
        /// </summary>
        public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

        /// <summary>
        /// 扇形展开方向，完全由 AimDirection.X 符号推导（确定性，跨端一致）
        /// </summary>
        public sbyte ShootDir => AimDirection.X >= 0 ? (sbyte)1 : (sbyte)-1;

        /// <summary>
        /// 本地确定性计时器，从 0 开始递增，每帧 +1
        /// 由于鱼在所有端的生成时刻一致，该计时器在各端会自然保持同步
        /// 持有者会每 60 帧触发一次 netUpdate 把 LocalTimer 同步给其它端做兜底
        /// </summary>
        public int LocalTimer;

        public int FishIndex => (int)Projectile.ai[0];
        public int TotalFishCount => Math.Max(1, (int)Projectile.ai[1]);
        internal bool Fired {
            get => Projectile.ai[2] == 1f;
            set => Projectile.ai[2] = value ? 1f : 0f;
        }

        private const int PreFireDelay = 16; //鱼出现后到可能开火的最小延迟
        private const int FireInterval = 14; //两条鱼间隔
        //最后一条鱼开火后再等多久全体进入离场阶段（覆盖激光生命周期 SparklingRay.timeLeft = 40）
        private const int DepartureGuard = 50;

        private float glowPulse;
        private float fadeOut;

        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public override void SetDefaults() {
            Projectile.width = 40; Projectile.height = 40;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600; //容错
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
            if (owner.TryGetHalibutPlayer(out var halibutPlayer)) {
                AimDirection = owner.To(halibutPlayer.MouseWorld).UnitVector();
            }
            LocalTimer++;
            glowPulse = (float)Math.Sin(LocalTimer * 0.25f + FishIndex) * 0.5f + 0.5f;

            int fishFireTime = PreFireDelay + FishIndex * FireInterval;
            int allFireTime = PreFireDelay + (TotalFishCount - 1) * FireInterval;
            int departureStartTime = allFireTime + DepartureGuard;
            bool inDeparturePhase = LocalTimer >= departureStartTime;

            if (!inDeparturePhase) {
                //位置由同步过的 AimDirection 推算，跨端一致
                Vector2 behind = (-AimDirection).SafeNormalize(Vector2.UnitX);
                float arc = MathHelper.ToRadians(FishSparkling.RoingArc);
                float radius = 190f;
                float t = TotalFishCount <= 1 ? 0.5f : FishIndex / (float)(TotalFishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff * ShootDir * -1);
                Vector2 basePos = owner.Center + offsetDir * radius;
                float bob = (float)Math.Sin(LocalTimer * 0.08f + FishIndex) * 6f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, basePos + new Vector2(0, bob), 0.25f);

                //朝向使用同步的 AimDirection 来确定一个远点（避免依赖各端不一致的 Main.MouseWorld）
                Vector2 aimToward = owner.Center + AimDirection * 1500f;
                Projectile.rotation = Projectile.To(aimToward).ToRotation();

                //仅持有者执行开火逻辑，激光通过 NetMessage 自动同步给其它端
                if (!Fired && LocalTimer >= fishFireTime && Projectile.IsOwnedByLocalPlayer()) {
                    Fired = true;
                    FireLaser();
                    Projectile.netUpdate = true;
                }
            }
            else {
                int departureTimer = LocalTimer - departureStartTime;
                if (departureTimer < FishSparkling.DepartureDelay) {
                    //原地轻微旋转漂浮
                    Projectile.rotation += 0.02f * (FishIndex % 2 == 0 ? 1 : -1);
                }
                else {
                    int flyTime = departureTimer - FishSparkling.DepartureDelay;
                    //平滑加速 0-1
                    float accelProgress = MathHelper.Clamp(flyTime / (float)FishSparkling.DepartureDuration, 0f, 1f);
                    accelProgress = MathF.Pow(accelProgress, 0.65f);

                    //外向方向完全由出生参数决定，确保所有端一致地飞出去
                    Vector2 behind = (-AimDirection).SafeNormalize(Vector2.UnitX);
                    float arc = MathHelper.ToRadians(FishSparkling.RoingArc);
                    float t = TotalFishCount <= 1 ? 0.5f : FishIndex / (float)(TotalFishCount - 1);
                    float angOff = (t - 0.5f) * arc;
                    Vector2 outward = behind.RotatedBy(angOff * ShootDir * -1).SafeNormalize(Vector2.UnitY);

                    //当前帧速度（前期更慢，后期加速），再叠加一点确定性脉动
                    float baseSpeed = MathHelper.Lerp(6f, 32f, accelProgress);
                    baseSpeed *= 1f + 0.15f * (float)Math.Sin(flyTime * 0.18f + FishIndex);

                    Vector2 move = outward * baseSpeed;
                    Projectile.Center += move;

                    Projectile.localAI[0] += move.Length();

                    //使用一个固定的离场距离，避免依赖各端屏幕尺寸
                    const float exitDistance = 3200f;
                    float distProgress = MathHelper.Clamp(Projectile.localAI[0] / exitDistance, 0f, 1f);
                    fadeOut = MathHelper.Clamp((distProgress - 0.55f) / 0.45f, 0f, 1f); //55% 距离后开始淡

                    if (distProgress >= 0.98f) {
                        Projectile.Kill();
                    }
                }
            }
            Projectile.spriteDirection = Projectile.rotation.ToRotationVector2().X > 0 ? 1 : -1;

            //持有者每 60 帧广播一次状态，缓解长生命周期下可能的累积漂移
            if (Projectile.IsOwnedByLocalPlayer() && LocalTimer > 0 && LocalTimer % 60 == 0) {
                Projectile.netUpdate = true;
            }
        }

        private void FireLaser() {
            SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
            //发射方向使用同步过的 AimDirection，保证激光朝向跨端一致
            Vector2 dir = AimDirection.SafeNormalize(Vector2.UnitX);
            int damage = (int)(Projectile.damage * (1 + HalibutData.GetDomainLayer() * 0.35));
            int beam = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 10f, dir * 0.1f,
                ModContent.ProjectileType<SparklingRay>(), damage, 1f, Projectile.owner, Projectile.identity);
            if (Main.projectile.IndexInRange(beam)) {
                Main.projectile[beam].rotation = dir.ToRotation();
                Main.projectile[beam].localAI[0] = 0;
                Main.projectile[beam].localAI[1] = FishIndex; //传递颜色层次
                Main.projectile[beam].netUpdate = true;
            }
            //发射光尘
            for (int i = 0; i < 12; i++) {
                Vector2 v = dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(4f, 9f);
                var d = Dust.NewDustPerfect(Projectile.Center + dir * 16f, DustID.GemAmethyst, v, 150, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
                d.color = Color.Lerp(Color.DeepSkyBlue, Color.HotPink, Main.rand.NextFloat());
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;//获取鱼的纹理

            //计算绘制参数
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame();
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + MathHelper.PiOver4;
            float pulseScale = 1f + glowPulse * 0.15f;
            float opacity = 1f - fadeOut;
            Color baseCol = Color.Lerp(Color.DeepSkyBlue, Color.HotPink, 0.4f + 0.3f * glowPulse);
            baseCol *= opacity;
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, baseCol * 0.6f, drawRotation, origin, pulseScale * 1.25f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, Color.White * opacity, drawRotation, origin, pulseScale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 友方版本的激光
    /// </summary>
    internal class SparklingRay : ModProjectile
    {
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> MaskLaserLine = null;
        public override string Texture => CWRConstant.Placeholder;
        private readonly Vector2[] top = new Vector2[70];
        private readonly Vector2[] bot = new Vector2[70];
        private Vector2 topEnd, botEnd;
        private Color gradientStart = new(255, 170, 230);
        private Color gradientMid = new(160, 200, 255);
        private Color gradientEnd = new(90, 140, 255);
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * 2400, 120, ref p);
        }
        public override void AI() {
            //通过同步的 identity 找到对应的鱼弹幕，使激光跟随鱼的位置和朝向
            if (Projectile.ai[0].TryGetProjectile(out var projectile)) {
                Projectile.Center = projectile.Center;
                Projectile.rotation = projectile.rotation;
            }
            float fishIndex = Projectile.localAI[1];
            float hueOffset = fishIndex % 7 / 7f; //简单的层次调色
            gradientStart = Color.Lerp(new Color(255, 180, 240), new Color(240, 120, 210), hueOffset);
            gradientMid = Color.Lerp(new Color(180, 210, 255), new Color(120, 170, 255), hueOffset);
            gradientEnd = Color.Lerp(new Color(100, 160, 255), new Color(70, 110, 200), hueOffset);

            for (int i = 0; i < 70; i++) {
                float x = i * 15f;
                float y = 8f * (0.08f * Projectile.localAI[0]) * (float)Math.Pow(0.1f * x, 0.45);
                top[i] = new Vector2(x, y);
                bot[i] = new Vector2(x, -y);
            }
            float endX = 300 * 15f;
            float endY = 8f * (0.08f * Projectile.localAI[0]) * (float)Math.Pow(0.1f * 70 * 15, 0.45);
            topEnd = new Vector2(endX, endY);
            botEnd = new Vector2(endX, -endY);
            if (Projectile.localAI[0] <= 5 && Projectile.timeLeft > 10)
                Projectile.localAI[0] += 30f; //更快展开
            if (Projectile.timeLeft <= 20 && Projectile.localAI[0] > 0) Projectile.localAI[0] -= 20f;
            if (Projectile.localAI[0] < 0) Projectile.localAI[0] = 0;

            //核心光粒
            if (Main.rand.NextBool(3)) {
                Vector2 corePos = Projectile.Center + Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(40f, 400f);
                var d = Dust.NewDustPerfect(corePos, DustID.GemDiamond, Vector2.Zero, 100, Color.White, Main.rand.NextFloat(0.6f, 1.1f));
                d.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Vector2 edgePos = Projectile.Center + Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(20f, 800f) + Main.rand.NextVector2Circular(60f, 30f);
                var d2 = Dust.NewDustPerfect(edgePos, DustID.GemSapphire, Vector2.Zero, 150, Color.Lerp(Color.DeepSkyBlue, Color.HotPink, 0.5f), Main.rand.NextFloat(0.5f, 0.9f));
                d2.noGravity = true;
            }
        }
        public override bool PreDraw(ref Color lightColor) {
            List<ColoredVertex> vertices = new();
            for (int i = 0; i < 70; i++) {
                float u = i / 70f;
                Color colA = Color.Lerp(gradientStart, gradientMid, u);
                Color colB = Color.Lerp(gradientMid, gradientEnd, u);
                vertices.Add(new ColoredVertex(top[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, colA, new Vector3(u, 0, 1 - u)));
                vertices.Add(new ColoredVertex(bot[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, colB, new Vector3(u, 1, 1 - u)));
            }
            vertices.Add(new ColoredVertex(topEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, gradientEnd, new Vector3(1, 0, 1)));
            vertices.Add(new ColoredVertex(botEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, gradientEnd, new Vector3(1, 1, 1)));
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Main.graphics.GraphicsDevice.Textures[0] = MaskLaserLine.Value;
            if (vertices.Count >= 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
