using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
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
            //仅持有者本地，Shoot触发
            //所有 Projectile.NewProjectile 由本地玩家创建后会通过 NetMessage 自动同步到其它端
            if (player.CountProjectilesOfID<SparklingFishHolder>() > 0) {
                return;
            }
            if (Cooldown > 0) {
                return;
            }

            SetCooldown();
            int fishCount = 4 + HalibutData.GetDomainLayer(); //4+领域数量鱼

            //使用同步过的鼠标方向（HalibutPlayer.MouseWorld 由 InnoVault PlayerNetwork 提供）
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

                //初始velocity跨端传AimDirection
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

    /// <summary>
    /// 光学定位闪：鱼位物化耀斑 + 蓄束收束指示，中心模式(-1)为施法涟漪。<br/>
    /// 层次 = SoftGlow 压底 / 沿束拉丝 / 星芒核 / 环几何，弃用裸光球本体
    /// </summary>
    internal class SparklingSpawnEffect : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Index => ref Projectile.ai[0]; //-1 = 中心光环 其他=鱼弹幕identity
        private const int LifeTime = 42; //存活时间
        private float seed;
        private int hueIndex = -1;
        private float chargeT;   //0~1蓄束进度，由鱼的确定性计时推出
        private float fishRot;   //跟随鱼的朝向，拉丝沿束对齐
        private bool fishFired;

        private Color Hue => Index < 0 ? new Color(214, 110, 240) : SparklingVFX.BeamHue(hueIndex < 0 ? 0 : hueIndex);
        private float Age => LifeTime - Projectile.timeLeft;

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
            if (Index < 0) {
                //施法涟漪：外放火花 + 少量缓浮微尘余韵
                for (int i = 0; i < 10; i++) {
                    float rot = MathHelper.TwoPi * i / 10f;
                    Vector2 vel = rot.ToRotationVector2() * Main.rand.NextFloat(3f, 6.5f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel
                        , Color.Lerp(Hue, SparklingVFX.CoreOf(Hue), Main.rand.NextFloat(0.6f))
                        , Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(10, 16));
                }
                SparklingVFX.SpawnIonBurst(Projectile.Center, -Vector2.UnitY, Hue, 4);
            }
            else {
                //鱼位物化：光向内凝聚成点
                SparklingVFX.SpawnConvergeSparks(Projectile.Center, Hue, 8, 46f);
            }
        }

        public override void AI() {
            float t = 1f - Projectile.timeLeft / (float)LifeTime; //0->1
            Projectile.rotation += 0.03f + 0.04f * MathF.Sin(seed + Main.GlobalTimeWrappedHourly * 5f);

            //中心涟漪：零散缓浮微尘，涟漪散去后仍短暂存续
            if (Index < 0 && Main.rand.NextBool(6)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(40f + t * 60f, 26f);
                PRTLoader.NewParticle<PRT_FishSparklingIon>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f)
                    , Hue, Main.rand.NextFloat(0.12f, 0.2f))?.Configure(Main.rand.Next(26, 40));
            }

            //通过 identity 跨端定位关联的鱼弹幕；identity 在所有客户端保持一致
            if (Index.TryGetProjectile(out var fash)) {
                Projectile.Center = fash.Center + fash.rotation.ToRotationVector2() * 32;
                fishRot = fash.rotation;
                if (fash.ModProjectile is SparklingFishHolder holder) {
                    hueIndex = holder.FishIndex;
                    fishFired = holder.Fired;
                    //蓄束窗口：击发前 ChargeLeadFrames 帧内收束渐强(确定性计时，各端一致)
                    int untilFire = holder.FireTime - holder.LocalTimer;
                    chargeT = !fishFired && untilFire > 0 && untilFire <= SparklingFishHolder.ChargeLeadFrames
                        ? 1f - untilFire / (float)SparklingFishHolder.ChargeLeadFrames
                        : 0f;
                }
                if (fash.ai[2] == 0 && Projectile.timeLeft < LifeTime / 2) {
                    Projectile.timeLeft = LifeTime / 2;
                }
            }
            else if (Projectile.owner.TryGetPlayer(out var owner)) {
                Projectile.Center = owner.Center;
            }

            Lighting.AddLight(Projectile.Center, Hue.ToVector3() * (0.14f + 0.2f * chargeT));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D streak = CWRAsset.Extra_98?.Value;
            Texture2D flare = CWRAsset.StarFlare02?.Value;
            Texture2D ring = CWRAsset.Ring01?.Value;
            if (glow == null || streak == null || flare == null || ring == null) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float age = Age;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            float burst = MathHelper.Clamp(1f - age / 9f, 0f, 1f); //物化爆帧包络
            float breath = 1f + 0.1f * MathF.Sin(seed + Main.GlobalTimeWrappedHourly * 9f);
            Color hueA0 = Hue with { A = 0 };
            Color coreA0 = SparklingVFX.CoreOf(Hue) with { A = 0 };

            if (Index < 0) {
                //施法涟漪：扩散细环 + 横向透镜拉丝 + 小星芒
                float t = age / (float)LifeTime;
                float ringR = MathHelper.Lerp(26f, 150f, VaultUtils.EaseOutCubic(t));
                float ringAlpha = (1f - t) * 0.75f;
                Main.spriteBatch.Draw(ring, drawPos, null, hueA0 * ringAlpha, 0f
                    , ring.Size() * 0.5f, ringR * 2f / ring.Width, SpriteEffects.None, 0f);
                float streakFade = MathHelper.Clamp(1f - age / 14f, 0f, 1f);
                if (streakFade > 0f) {
                    Main.spriteBatch.Draw(streak, drawPos, null, coreA0 * (0.8f * streakFade)
                        , MathHelper.PiOver2, streak.Size() * 0.5f
                        , new Vector2(0.22f, 2.6f * (0.5f + streakFade)), SpriteEffects.None, 0f);
                }
                Main.spriteBatch.Draw(flare, drawPos, null, coreA0 * (0.7f * (1f - t))
                    , Projectile.rotation, flare.Size() * 0.5f, 0.4f * breath * (1f - t * 0.5f), SpriteEffects.None, 0f);
                return false;
            }

            //鱼位耀斑：压底柔光(仅底层) + 沿束拉丝 + 星芒核
            float energy = (0.4f + 0.6f * chargeT) * fade * (1f + burst * 0.8f);
            Main.spriteBatch.Draw(glow, drawPos, null, hueA0 * (0.32f * energy), 0f
                , glow.Size() * 0.5f, (0.5f + 0.3f * chargeT) * breath, SpriteEffects.None, 0f);
            float streakLen = 0.9f * burst + 0.7f * chargeT;
            if (streakLen > 0.05f) {
                Main.spriteBatch.Draw(streak, drawPos, null, coreA0 * (0.75f * energy)
                    , fishRot + MathHelper.PiOver2, streak.Size() * 0.5f
                    , new Vector2(0.2f, streakLen), SpriteEffects.None, 0f);
            }
            Main.spriteBatch.Draw(flare, drawPos, null, coreA0 * (0.8f * energy)
                , Projectile.rotation, flare.Size() * 0.5f
                , (0.2f + 0.14f * chargeT + 0.22f * burst) * breath, SpriteEffects.None, 0f);

            //蓄束收束环：向内坍缩读出"正在聚束"
            if (chargeT > 0f && !fishFired) {
                float rs = MathHelper.Lerp(56f, 9f, chargeT) * 2f / ring.Width;
                Main.spriteBatch.Draw(ring, drawPos, null, hueA0 * (0.85f * chargeT * fade), 0f
                    , ring.Size() * 0.5f, rs, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 闪光皇后鱼承载弹幕，静止环绕并按序发射激光
    /// 持有者权威 + 确定性推进；AimDirection、ShootDir、LocalTimer 经 OnSpawn/SendExtraAI 同步
    /// </summary>
    internal class SparklingFishHolder : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
        /// 本地确定性计时，从 0 每帧 +1
        /// 各端生成时刻一致故自然同步；持有者每 60 帧 netUpdate 兜底
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
        /// <summary>击发前的蓄束提示帧数，仅表现层</summary>
        internal const int ChargeLeadFrames = 12;

        /// <summary>本鱼的确定性击发时刻，供收束指示与蓄束窗口推算</summary>
        internal int FireTime => PreFireDelay + FishIndex * FireInterval;
        /// <summary>嘴部锚点，蓄束与击发特效定位</summary>
        internal Vector2 MouthPos => Projectile.Center + Projectile.rotation.ToRotationVector2() * 32f;

        private float glowPulse;
        private float fadeOut;
        private float recoil;      //击发后坐0~1，指数回弹，纯绘制侧
        private Vector2 lastMove;  //离场帧位移，残影链与蜕散方向

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
            if (owner.TryGetHalibutPlayer(out var halibutPlayer)
                && halibutPlayer.TryGetMouseWorld(out Vector2 mouseWorld)) {
                AimDirection = owner.To(mouseWorld).UnitVector();
            }
            LocalTimer++;
            glowPulse = (float)Math.Sin(LocalTimer * 0.25f + FishIndex) * 0.5f + 0.5f;

            int fishFireTime = FireTime;
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

                //蓄束提示音：确定性时刻，各端一致，音阶随序号上行
                if (!Fired && fishFireTime - LocalTimer == ChargeLeadFrames) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.32f, Pitch = -0.05f + FishIndex * 0.04f }, Projectile.Center);
                }
                //蓄束期向心收束火花
                int untilFire = fishFireTime - LocalTimer;
                if (!Fired && untilFire > 0 && untilFire <= ChargeLeadFrames && Main.rand.NextBool(2)) {
                    SparklingVFX.SpawnConvergeSparks(MouthPos, SparklingVFX.BeamHue(FishIndex), 1, 30f);
                }
                //击发拍：后坐 + 电离迸发 + 轮射音阶(全端同帧，激光实体仍由持有者权威生成)
                if (LocalTimer == fishFireTime) {
                    recoil = 1f;
                    SoundEngine.PlaySound(SoundID.Item33 with {
                        Volume = 0.75f,
                        Pitch = MathF.Min(0.12f + FishIndex * 0.05f, 0.55f)
                    }, Projectile.Center);
                    SparklingVFX.SpawnIonBurst(MouthPos, AimDirection, SparklingVFX.BeamHue(FishIndex), 6);
                }

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

                    //外向方向出生参数定，跨端一致
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
                    lastMove = move;

                    //渐转向运动方向，光速离场的读向(纯表现)
                    Projectile.rotation = Projectile.rotation.AngleLerp(outward.ToRotation(), 0.12f);

                    //离场光痕蜕散
                    if (Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - move * 0.5f, -move * 0.08f
                            , SparklingVFX.BeamHue(FishIndex) * 0.8f, Main.rand.NextFloat(0.4f, 0.7f))
                            ?.Configure(false, 10);
                    }

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
            recoil *= 0.8f;
            Projectile.spriteDirection = Projectile.rotation.ToRotationVector2().X > 0 ? 1 : -1;
            Lighting.AddLight(Projectile.Center, SparklingVFX.BeamHue(FishIndex).ToVector3() * (0.2f * (1f - fadeOut)));

            //持有者每 60 帧广播一次状态，缓解长生命周期下可能的累积漂移
            if (Projectile.IsOwnedByLocalPlayer() && LocalTimer > 0 && LocalTimer % 60 == 0) {
                Projectile.netUpdate = true;
            }
        }

        private void FireLaser() {
            //发射方向使用同步过的 AimDirection，保证激光朝向跨端一致
            Vector2 dir = AimDirection.SafeNormalize(Vector2.UnitX);
            int damage = (int)(Projectile.damage * (1 + HalibutData.GetDomainLayer() * 0.35));
            int beam = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 10f, dir * 0.1f,
                ModContent.ProjectileType<SparklingRay>(), damage, 1f, Projectile.owner, Projectile.identity);
            if (Main.projectile.IndexInRange(beam)) {
                Main.projectile[beam].rotation = dir.ToRotation();
                Main.projectile[beam].netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value;
            if (CWRID.Item_SparklingEmpress > 0) {
                value = TextureAssets.Item[CWRID.Item_SparklingEmpress].Value;//获取鱼的纹理
            }
            else {
                Main.instance.LoadItem(ItemID.Jewelfish);
                value = TextureAssets.Item[ItemID.Jewelfish].Value;//获取鱼的纹理
            }

            //计算绘制参数
            Color hue = SparklingVFX.BeamHue(FishIndex);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame();
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + MathHelper.PiOver4;
            float pulseScale = 1f + glowPulse * 0.15f;
            float opacity = 1f - fadeOut;

            //入场物化：缩放展开带轻微过冲，禁pop-in
            float matT = MathHelper.Clamp(LocalTimer / 10f, 0f, 1f);
            pulseScale *= VaultUtils.EaseOutCubic(matT) * (1f + 0.12f * MathF.Sin(matT * MathHelper.Pi));

            //蓄力后仰与击发后坐(纯绘制侧偏移，发射点保持稳定让束体笔直)
            Vector2 offset = Vector2.Zero;
            int untilFire = FireTime - LocalTimer;
            if (!Fired && untilFire > 0 && untilFire <= ChargeLeadFrames) {
                float leanT = 1f - untilFire / (float)ChargeLeadFrames;
                offset -= AimDirection * leanT * leanT * 6f;
            }
            offset -= AimDirection * recoil * 14f;
            drawPosition += offset;

            //离场残影链：速度越快拖越长，读出光速离场而非贴图平移
            float ghostSpeed = lastMove.Length();
            if (ghostSpeed > 3f) {
                Color ghostCol = hue with { A = 0 };
                float speedFactor = MathHelper.Clamp(ghostSpeed / 18f, 0f, 1f);
                for (int k = 1; k <= 3; k++) {
                    float ga = (0.42f - k * 0.12f) * opacity * speedFactor;
                    Main.spriteBatch.Draw(value, drawPosition - lastMove * (k * 2.4f), sourceRect, ghostCol * ga
                        , drawRotation, origin, pulseScale * (1f - k * 0.08f), SpriteEffects.None, 0f);
                }
            }

            //底层色相辉边 + 本体
            Color rim = Color.Lerp(Color.White, hue, 0.55f) with { A = 0 };
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, rim * (0.5f * opacity), drawRotation, origin, pulseScale * 1.18f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, Color.White * opacity, drawRotation, origin, pulseScale, SpriteEffects.None, 0f);

            //击发帧白色过冲(recoil指数衰减，仅头两帧过阈)
            if (recoil > 0.6f) {
                Main.spriteBatch.Draw(value, drawPosition, sourceRect, (Color.White with { A = 0 }) * ((recoil - 0.6f) * 1.6f * opacity)
                    , drawRotation, origin, pulseScale * 1.05f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 友方版本的激光：相干激光三拍节奏，蓄束导引线(无伤害)→击发过冲→束径坍缩离场。<br/>
    /// 束体 = FishSparklingBeam.fx 静态quad三层(暗外晕/饱和单色中层/热芯)；判定线与旧版一致(2400×120)
    /// </summary>
    internal class SparklingRay : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ChargeTime = 6;    //蓄束导引线，无伤害帧
        private const int OvershootTime = 3; //击发过冲衰减帧
        private const int DecayTime = 10;    //束径坍缩离场帧
        private const int TotalLife = 40;    //与旧版timeLeft一致
        private const float BeamLength = 2400f;  //与Colliding判定线等长
        private const float FullHalfWidth = 34f; //满宽quad半宽像素

        private int age;
        private int hueIndex = -1;
        private float widthMul;
        private float overshoot;

        private float SeedF => Projectile.whoAmI * 0.173f % 1f;
        private bool InCharge => age <= ChargeTime;
        private Color Hue => SparklingVFX.BeamHue(hueIndex < 0 ? 0 : hueIndex);

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
        //蓄束期为导引线，不结算伤害(三拍节奏的预告拍)
        public override bool? CanDamage() => InCharge ? false : null;

        public override void AI() {
            //通过同步的 identity 找到对应的鱼弹幕，使激光跟随鱼的位置和朝向
            if (Projectile.ai[0].TryGetProjectile(out var projectile)) {
                Projectile.Center = projectile.Center;
                Projectile.rotation = projectile.rotation;
                if (projectile.ModProjectile is SparklingFishHolder holder) {
                    hueIndex = holder.FishIndex;
                }
            }
            age = TotalLife - Projectile.timeLeft;

            //束径包络：导引线16% → 击发快照(轻微过冲) → 满宽 → 坍缩收窄(非alpha渐隐)
            if (InCharge) {
                widthMul = 0.16f;
            }
            else if (Projectile.timeLeft <= DecayTime) {
                float x = Projectile.timeLeft / (float)DecayTime;
                widthMul = x * x;
            }
            else {
                float sinceFire = age - ChargeTime;
                float snap = sinceFire < 3f ? 1f - sinceFire / 3f : 0f;
                widthMul = 1f + snap * 0.18f;
            }
            overshoot = age > ChargeTime
                ? MathHelper.Clamp(1f - (age - ChargeTime - 1) / (float)OvershootTime, 0f, 1f)
                : 0f;

            //沿束光照，蓄束期压暗
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float lightGain = InCharge ? 0.08f : 0.3f * MathHelper.Clamp(widthMul, 0f, 1f);
            for (int i = 0; i < 15; i++) {
                Lighting.AddLight(Projectile.Center + dir * (i * 160f), Hue.ToVector3() * lightGain);
            }

            //电离微尘沿束蜕散：贴中层束带生成，熄束后仍缓浮存续(aftermath)
            if (!VaultUtils.isServer && !InCharge && Projectile.timeLeft > DecayTime && Main.rand.NextBool(2)) {
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(50f, 900f) + perp * Main.rand.NextFloat(-9f, 9f);
                PRTLoader.NewParticle<PRT_FishSparklingIon>(pos, perp * Main.rand.NextFloat(-0.5f, 0.5f) + dir * 0.4f
                    , Hue, Main.rand.NextFloat(0.13f, 0.24f))?.Configure(Main.rand.Next(28, 46));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中迸发：顺束向火花 + 电离残迹留在目标处
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center, dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(4f, 8f)
                    , Color.Lerp(Hue, SparklingVFX.CoreOf(Hue), Main.rand.NextFloat(0.6f)), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
            SparklingVFX.SpawnIonBurst(target.Center, dir, Hue, 3);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (widthMul < 0.02f) {
                return;
            }
            Effect effect = FishSparklingAssets.FishSparklingBeam;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //起点回缩进鱼体内，束体从鱼嘴内部涌出而非硬切
            Vector2 muzzle = Projectile.Center - dir * 12f;
            Vector2 tip = Projectile.Center + dir * BeamLength;
            float halfWidth = FullHalfWidth * widthMul;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((muzzle + perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((muzzle - perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uWidthMul"]?.SetValue(widthMul);
            effect.Parameters["uCharge"]?.SetValue(InCharge ? 1f : 0f);
            effect.Parameters["uOvershoot"]?.SetValue(overshoot);
            effect.Parameters["uHalfWidthPx"]?.SetValue(halfWidth);
            effect.Parameters["seed"]?.SetValue(SeedF);
            effect.Parameters["uColor"]?.SetValue(Hue.ToVector3());
            effect.Parameters["uDarkColor"]?.SetValue(SparklingVFX.DarkOf(Hue).ToVector3());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (widthMul < 0.02f) {
                return;
            }
            Vector2 muzzleScreen = Projectile.Center - Main.screenPosition;

            //着色器缺失时的CPU兜底束线：暗晕+单色窄带，效果不至于消失
            if (FishSparklingAssets.FishSparklingBeam == null && CWRAsset.Extra_98?.Value is Texture2D line) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                Vector2 mid = muzzleScreen + dir * (BeamLength * 0.5f);
                float len = BeamLength / line.Height;
                spriteBatch.Draw(line, mid, null, (SparklingVFX.DarkOf(Hue) with { A = 0 }) * 0.6f
                    , Projectile.rotation + MathHelper.PiOver2, line.Size() * 0.5f
                    , new Vector2(0.5f * widthMul, len), SpriteEffects.None, 0f);
                spriteBatch.Draw(line, mid, null, (Hue with { A = 0 }) * 0.9f
                    , Projectile.rotation + MathHelper.PiOver2, line.Size() * 0.5f
                    , new Vector2(0.2f * widthMul, len), SpriteEffects.None, 0f);
            }

            //发射端棱镜耀斑：蓄束期微光，击发过冲期白闪+色散微扇
            float energy = InCharge ? 0.35f : MathHelper.Clamp(widthMul, 0f, 1f);
            SparklingVFX.DrawMuzzleFlare(spriteBatch, muzzleScreen, Projectile.rotation, Hue
                , energy, overshoot, Main.GlobalTimeWrappedHourly + SeedF * 40f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
