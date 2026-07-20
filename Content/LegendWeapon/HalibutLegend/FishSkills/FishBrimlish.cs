using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>硫火逸散专属 shader 资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishBrimlishAssets
    {
        /// <summary>硫火球彗尾条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishBrimlishComet { get; private set; }
    }

    /// <summary>硫磺火鱼技能，开火周期召唤身后喷火鱼</summary>
    internal class FishBrimlish : FishSkill
    {
        public override int UnlockFishID => CWRID.Item_Brimlish;
        public override int DefaultCooldown => 20;
        public override int ResearchDuration => 60 * 14;

        private int shootCounter = 0;
        private static int ShootInterval => 8 - HalibutData.GetDomainLayer() / 3; //每8次开火触发一次

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            shootCounter++;

            if (shootCounter >= ShootInterval && Cooldown <= 0) {
                shootCounter = 0;
                SetCooldown();

                //在玩家身后召唤硫磺火鱼
                SpawnBrimfishSpitter(player, source, damage, knockback);
            }

            return null;
        }

        private void SpawnBrimfishSpitter(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //在玩家后方生成：Shoot 仅在持有玩家的本地客户端调用，
            //Projectile.NewProjectile 会自动通过 NetMessage 同步生成到其它端
            Vector2 behindPlayer = player.Center - new Vector2(player.direction * 120f, 60f);

            Projectile.NewProjectile(
                source,
                behindPlayer,
                Vector2.Zero,
                ModContent.ProjectileType<BrimfishSpitterProjectile>(),
                (int)(damage * (0.8f + HalibutData.GetDomainLayer() * 0.2f)),
                knockback,
                player.whoAmI
            );

            //硫磺火召唤音效
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, behindPlayer);
        }
    }

    /// <summary>
    /// 硫磺火鱼喷射弹幕
    /// 持有者权威：状态机、锁定、位移在持有者端推进，ai + SendExtraAI 同步其它端
    /// </summary>
    internal class BrimfishSpitterProjectile : ModProjectile
    {
        //外观取自灾厄的硫磺鱼贴图，绘制时经GetT2DAsset安全获取，Texture本身只挂占位资源
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const string FishTexture = "CalamityMod/Items/Fishing/BrimstoneCragCatches/Brimlish";

        private enum FishState
        {
            Appearing,   //出现
            Charging,    //蓄力
            Spitting,    //喷射
            Fading       //消失
        }

        //ai[0] = 状态枚举（自动同步）
        //ai[1] = 状态计时（自动同步）
        //ai[2] = 锁定的目标 NPC 索引 + 1（0 表示无目标，自动同步）
        //localAI[0]蓄力进度，确定性绘制
        private ref float StateRaw => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float TargetSlot => ref Projectile.ai[2];
        private ref float ChargeProgress => ref Projectile.localAI[0];

        private FishState State {
            get => (FishState)StateRaw;
            set => StateRaw = (float)value;
        }

        /// <summary>
        /// 当前锁定目标 NPC 索引（-1 表示无），通过 ai[2] 同步
        /// </summary>
        private int TargetNPCID {
            get => (int)TargetSlot - 1;
            set => TargetSlot = value + 1;
        }

        private float glowIntensity = 0f;
        private float pulsePhase = 0f;
        private FishState lastVisibleState = FishState.Appearing;

        //吸气鼓腮与喷吐脉冲，全部由同步过的状态与计时确定性推导，各端一致
        private float cheekPuff = 0f;
        private float spitPulse = 0f;
        private float spitFlash = 0f;
        private int wavesDone = 0;

        //状态持续时间
        private const int AppearDuration = 15;
        private const int ChargeDuration = 25;
        private const int SpitDuration = 40;
        private const int FadeDuration = 20;

        //攻击参数
        private const float SearchRange = 1200f;
        private static int FlameCount => 6 + HalibutData.GetDomainLayer() / 2; //喷射火焰总数量
        private const int SpitWaveCount = 3;
        //三波喷吐时刻（SpitDuration 内），弹幕总数不变只分批
        private static readonly int[] WaveTimes = [2, 14, 26];

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = AppearDuration + ChargeDuration + SpitDuration + FadeDuration + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => false; //鱼本身不造成伤害，只有火焰造成伤害

        public override void SendExtraAI(BinaryWriter writer) {
            //同步旋转角度，因为 Projectile.rotation 默认不参与 NetMessage 同步
            //同时旋转跟随的目标可能高速移动，需要持有者主导朝向
            writer.Write(Projectile.rotation);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.rotation = reader.ReadSingle();
        }

        /// <summary>真实瞄准角：存储的 rotation 带 +PiOver4 贴图补正，此处还原</summary>
        private float AimAngle => Projectile.rotation - MathHelper.PiOver4;

        private Vector2 MouthPos() => Projectile.Center + AimAngle.ToRotationVector2() * 20f * Projectile.scale;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            pulsePhase += 0.2f;

            bool isOwner = Projectile.IsOwnedByLocalPlayer();

            //状态机推进，状态切换时只在持有者侧发生
            switch (State) {
                case FishState.Appearing:
                    AppearingBehavior(owner, isOwner);
                    break;
                case FishState.Charging:
                    ChargingBehavior(owner, isOwner);
                    break;
                case FishState.Spitting:
                    SpittingBehavior(owner, isOwner);
                    break;
                case FishState.Fading:
                    FadingBehavior(isOwner);
                    break;
            }

            //侦测状态切换，便于在所有端做一次性表现
            if (State != lastVisibleState) {
                OnStateEntered(State);
                lastVisibleState = State;
            }

            //吸气鼓腮包络：蓄力渐鼓，喷吐逐波回瘪
            float cheekTarget = State == FishState.Charging ? ChargeProgress
                : State == FishState.Spitting ? MathHelper.Clamp(1f - wavesDone / (float)SpitWaveCount, 0f, 1f)
                : 0f;
            cheekPuff = MathHelper.Lerp(cheekPuff, cheekTarget, 0.22f);
            spitPulse *= 0.82f;
            spitFlash *= 0.80f;

            //硫磺火环境光照：暗红压底
            float pulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.72f * pulse * glowIntensity, 0.18f * pulse * glowIntensity, 0.06f * pulse * glowIntensity);

            //背脊余烬缓飘（视觉效果，所有端独立生成）
            if (!VaultUtils.isServer && glowIntensity > 0.3f && Main.rand.NextBool(8)) {
                var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), -6f) * Projectile.scale,
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)), default, Main.rand.NextFloat(0.4f, 0.7f));
                ember?.Configure(Main.rand.Next(12, 20), 0.015f);
            }

            //朝向目标：所有端都向同步过的目标进行同样的 Lerp 收敛，
            //避免远端在两次 netUpdate 之间出现旋转停滞，并以 SendExtraAI 周期校正漂移
            if ((State == FishState.Charging || State == FishState.Spitting) && IsTargetValid()) {
                NPC target = Main.npc[TargetNPCID];
                Vector2 toTarget = target.Center - Projectile.Center;
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    toTarget.ToRotation() + MathHelper.PiOver4,
                    0.15f
                );
            }

            //持有者周期性广播状态，让其它端的位置/旋转保持收敛
            if (isOwner && StateTimer > 0 && (int)StateTimer % 12 == 0) {
                Projectile.netUpdate = true;
            }
        }

        private void OnStateEntered(FishState newState) {
            //wavesDone 不在此重置：状态机线性单趟，字段初值 0 已保证首次进入正确，
            //且行为 switch 先于本检测执行，重置会让补发过的波次重复爆发
            switch (newState) {
                case FishState.Fading:
                    //化形起烟：一团暗红烟垫在鱼身
                    if (!VaultUtils.isServer) {
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center, new Vector2(0f, -0.3f)
                            , default, Main.rand.NextFloat(0.34f, 0.46f))
                            ?.Configure(Main.rand.Next(30, 42), new Color(104, 30, 16), new Color(26, 12, 10));
                    }
                    break;
            }
        }

        private void AppearingBehavior(Player owner, bool isOwner) {
            float progress = StateTimer / AppearDuration;

            //淡入（确定性，所有端一致），带轻微过冲的聚形
            Projectile.alpha = (int)(255 * (1f - progress));
            glowIntensity = progress;
            Projectile.scale = progress + 0.08f * (float)Math.Sin(progress * MathHelper.Pi);

            //轻微漂浮：仅持有者修改位置，避免各端独立漂浮造成位置不一致
            if (isOwner) {
                float floatY = (float)Math.Sin(pulsePhase * 0.8f) * 2f;
                Projectile.Center += new Vector2(0, floatY * 0.1f);
            }

            //余烬向心聚形（所有端独立生成）
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(36f, 36f);
                var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(Projectile.Center + offset,
                    -offset * 0.11f, default, Main.rand.NextFloat(0.5f, 0.9f));
                ember?.Configure(Main.rand.Next(10, 16), 0.008f);
            }

            if (isOwner && StateTimer >= AppearDuration) {
                State = FishState.Charging;
                StateTimer = 0;

                //搜索目标，并通过 ai[2] 同步给其它端
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                TargetNPCID = target?.whoAmI ?? -1;
                Projectile.netUpdate = true;
            }
        }

        private void ChargingBehavior(Player owner, bool isOwner) {
            float progress = StateTimer / ChargeDuration;
            ChargeProgress = progress;

            //蓄力时发光强度增加，体型不再均匀放大，吸气感交给鼓腮包络
            glowIntensity = 0.6f + progress * 0.4f;
            Projectile.scale = 1f;

            //轻微漂浮：仅持有者修改位置
            if (isOwner) {
                float floatY = (float)Math.Sin(pulsePhase * 1.2f) * 3f;
                Projectile.Center += new Vector2(0, floatY * 0.1f);
            }

            //吸气：嘴前余烬与硫尘被吸向嘴部
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                SpawnInhaleDust();
            }

            //蓄力音效（所有端按确定性的 StateTimer 播放，节奏接近）
            if ((int)StateTimer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with {
                    Volume = 0.3f * progress,
                    Pitch = -0.5f + progress * 0.3f
                }, Projectile.Center);
            }

            if (isOwner && StateTimer >= ChargeDuration) {
                State = FishState.Spitting;
                StateTimer = 0;
                Projectile.netUpdate = true;
            }
        }

        private void SpittingBehavior(Player owner, bool isOwner) {
            float progress = StateTimer / SpitDuration;

            //喷射时保持强烈发光
            glowIntensity = 1f - progress * 0.3f;

            //波驱动：视觉爆发所有端按确定性 StateTimer 触发，弹幕仅持有者生成
            while (wavesDone < SpitWaveCount && StateTimer >= WaveTimes[wavesDone]) {
                DoSpitWave(wavesDone, isOwner);
                wavesDone++;
            }

            //漂浮：仅持有者修改位置
            if (isOwner) {
                float floatY = (float)Math.Sin(pulsePhase) * 2f;
                Projectile.Center += new Vector2(0, floatY * 0.05f);
            }

            if (isOwner && StateTimer >= SpitDuration) {
                State = FishState.Fading;
                StateTimer = 0;
                Projectile.netUpdate = true;
            }
        }

        private void FadingBehavior(bool isOwner) {
            float progress = StateTimer / FadeDuration;

            //淡出（确定性，所有端一致）
            Projectile.alpha = (int)(255 * progress);
            glowIntensity = 1f - progress;
            Projectile.scale = 1f - progress * 0.5f;

            //化形剥落：鱼身余烬剥离上飘
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 off = Main.rand.NextVector2Circular(16f, 10f) * Projectile.scale;
                var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(Projectile.Center + off,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.5f, 1.6f)),
                    default, Main.rand.NextFloat(0.5f, 0.85f));
                ember?.Configure(Main.rand.Next(14, 24), 0.03f);
            }

            //缓慢下沉：仅持有者修改速度
            if (isOwner) {
                Projectile.velocity.Y += 0.2f;
            }

            if (isOwner && StateTimer >= FadeDuration) {
                Projectile.Kill();
            }
        }

        /// <summary>单波喷吐：视觉在所有端执行，弹幕生成仅持有者</summary>
        private void DoSpitWave(int waveIndex, bool isOwner) {
            spitPulse = 1f;
            spitFlash = 1f;

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.55f,
                Pitch = -0.42f + waveIndex * 0.14f
            }, Projectile.Center);

            Vector2 mouthPos = MouthPos();
            Vector2 aimDir = AimAngle.ToRotationVector2();

            //喷口爆发：定向余烬 + 少量硫尘（视觉，所有端）
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(mouthPos,
                        aimDir.RotatedByRandom(0.55f) * Main.rand.NextFloat(4f, 10f),
                        default, Main.rand.NextFloat(0.5f, 0.9f));
                    ember?.Configure(Main.rand.Next(12, 20));
                }
                for (int i = 0; i < 5; i++) {
                    Dust brimstone = Dust.NewDustPerfect(mouthPos, CWRID.Dust_Brimstone,
                        aimDir.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 9f),
                        0, default, Main.rand.NextFloat(1.6f, 2.6f));
                    brimstone.noGravity = true;
                    brimstone.fadeIn = 1.4f;
                }
            }

            //后坐：仅持有者修改位置
            if (isOwner) {
                Projectile.Center -= aimDir * 3.5f;
            }

            SpitWaveProjectiles(waveIndex);
        }

        /// <summary>单波弹幕：总数 FlameCount 均分三波，仅持有者发射并经 NetMessage 同步</summary>
        private void SpitWaveProjectiles(int waveIndex) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //蓄力期就无目标时维持旧版哑火；目标在波间死亡才重锁最近敌人，
            //保证分波不比旧版一次性齐射少喷
            if (TargetNPCID < 0) {
                return;
            }
            if (!IsTargetValid()) {
                NPC newTarget = Projectile.Center.FindClosestNPC(SearchRange);
                if (newTarget == null) {
                    return;
                }
                TargetNPCID = newTarget.whoAmI;
                Projectile.netUpdate = true;
            }

            NPC target = Main.npc[TargetNPCID];
            Vector2 mouthPos = MouthPos();
            Vector2 toTarget = (target.Center - mouthPos).SafeNormalize(Vector2.Zero);

            int total = FlameCount;
            int count = total / SpitWaveCount + (waveIndex < total % SpitWaveCount ? 1 : 0);
            if (count <= 0) {
                return;
            }

            //每波中心角带少量抖动，弹幕群更散
            Vector2 waveDir = toTarget.RotatedBy(Main.rand.NextFloat(-0.10f, 0.10f));
            for (int i = 0; i < count; i++) {
                float spreadAngle = count == 1 ? 0f : MathHelper.Lerp(-0.42f, 0.42f, i / (float)(count - 1));
                spreadAngle += Main.rand.NextFloat(-0.05f, 0.05f);
                Vector2 velocity = waveDir.RotatedBy(spreadAngle) * Main.rand.NextFloat(12f, 18f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    mouthPos,
                    velocity,
                    ModContent.ProjectileType<BrimstoneFlameProjectile>(),
                    Projectile.damage,
                    2f,
                    Projectile.owner
                );
            }
        }

        private bool IsTargetValid() {
            int id = TargetNPCID;
            if (id < 0 || id >= Main.maxNPCs) return false;
            NPC target = Main.npc[id];
            return target.active && target.CanBeChasedBy();
        }

        /// <summary>吸气尘流：嘴前硫尘被吸向嘴部</summary>
        private void SpawnInhaleDust() {
            Vector2 mouth = MouthPos();
            Vector2 spawn = mouth + AimAngle.ToRotationVector2().RotatedByRandom(0.5f) * Main.rand.NextFloat(28f, 70f);
            Dust brimstone = Dust.NewDustPerfect(spawn, CWRID.Dust_Brimstone,
                (mouth - spawn) * Main.rand.NextFloat(0.09f, 0.15f),
                0, default, Main.rand.NextFloat(1.2f, 1.9f));
            brimstone.noGravity = true;
        }

        public override void OnKill(int timeLeft) {
            //熄灭余韵：小把余烬 + 一团暗烟，克制不做二次爆发
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                        default, Main.rand.NextFloat(0.45f, 0.8f));
                    ember?.Configure(Main.rand.Next(14, 26));
                }
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center, new Vector2(0f, -0.4f)
                    , default, Main.rand.NextFloat(0.3f, 0.4f))
                    ?.Configure(Main.rand.Next(28, 40), new Color(96, 28, 16), new Color(24, 12, 10));
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.5f,
                Pitch = -0.5f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = CWRUtils.GetT2DAsset(FishTexture).Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            float alpha = (255f - Projectile.alpha) / 255f;

            //朝左时沿贴图纵轴镜像，避免鱼上下颠倒；45 度斜放贴图翻转后补正角相反
            float aim = AimAngle;
            bool faceLeft = Math.Cos(aim) < 0;
            float drawRot = faceLeft ? aim - MathHelper.PiOver4 : Projectile.rotation;
            SpriteEffects flip = faceLeft ? SpriteEffects.FlipVertically : SpriteEffects.None;

            //呼吸 + 鼓腮 + 喷吐脉冲的合成体量
            float breath = 1f + 0.03f * (float)Math.Sin(pulsePhase * 0.9f);
            float tremble = ChargeProgress > 0.8f && State == FishState.Charging
                ? 0.045f * (float)Math.Sin(pulsePhase * 3.4f) : 0f;
            float bodyScale = Projectile.scale * breath * (1f + cheekPuff * 0.13f + tremble - spitPulse * 0.06f);

            //背脊火鞘下层：夹在鱼身之下，根部锚定背脊，焰舌向上（火是世界朝向的）
            DrawBackSheath(sb, fishTex, drawPos, bodyScale, alpha, under: true);

            //底光：一层暗红光晕压底
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && glowIntensity > 0.05f) {
                sb.Draw(glow, drawPos, null, new Color(150, 40, 16, 0) * (0.4f * glowIntensity * alpha),
                    0f, glow.Size() / 2f, 1.35f * bodyScale, SpriteEffects.None, 0);
            }

            //主体绘制：暗红硫火色调，不再叠同贴图辉光堆
            Color mainColor = Color.Lerp(lightColor, new Color(255, 120, 60), glowIntensity * 0.55f);
            sb.Draw(fishTex, drawPos, null, mainColor * alpha, drawRot, origin,
                bodyScale, flip, 0);

            //背脊火鞘上层：小簇焰舌覆盖背脊上缘，完成夹心
            DrawBackSheath(sb, fishTex, drawPos, bodyScale, alpha, under: false);

            //鼓腮热芯：蓄力与喷吐期间腮部一点亮橙，禁纯白
            if (glow != null && cheekPuff > 0.08f) {
                Vector2 cheekPos = drawPos + aim.ToRotationVector2() * 8f * bodyScale;
                float cheekGlow = cheekPuff * glowIntensity * alpha;
                sb.Draw(glow, cheekPos, null, new Color(255, 150, 58, 0) * (0.75f * cheekGlow),
                    0f, glow.Size() / 2f, 0.30f * cheekPuff * bodyScale, SpriteEffects.None, 0);
            }

            //喷口闪：每波喷吐后数帧，沿瞄准方向拉伸的亮橙箭头闪光
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot != null && spitFlash > 0.1f) {
                Vector2 mouthDraw = MouthPos() - Main.screenPosition;
                sb.Draw(shot, mouthDraw, null, new Color(255, 140, 52, 0) * (0.8f * spitFlash * alpha),
                    aim, new Vector2(shot.Width * 0.18f, shot.Height * 0.5f),
                    new Vector2(0.34f * spitFlash + 0.12f, 0.13f) * bodyScale, SpriteEffects.None, 0);
            }

            return false;
        }

        /// <summary>背脊火鞘：Fire 序列帧，under 层宽暗红 + 窄橙红垫在鱼身下，上层一小簇覆背脊</summary>
        private void DrawBackSheath(SpriteBatch sb, Texture2D fishTex, Vector2 drawPos, float bodyScale, float alpha, bool under) {
            Texture2D fire = CWRAsset.Fire?.Value;
            if (fire == null || glowIntensity < 0.1f) {
                return;
            }

            int frameW = fire.Width / 4;
            int frameH = fire.Height / 4;
            int idx = (int)(Main.GameUpdateCount / 4 + Projectile.whoAmI * 3) % 16;
            Rectangle frame = new(frameW * (idx % 4), frameH * (idx / 4), frameW, frameH);
            Vector2 rootOrigin = new(frameW * 0.5f, frameH);
            //根部锚定背脊上缘
            Vector2 anchor = drawPos + new Vector2(0f, -fishTex.Height * 0.26f * bodyScale);
            float lick = 0.85f + 0.15f * (float)Math.Sin(pulsePhase * 1.7f);
            float wide = fishTex.Width * bodyScale / frameW;
            float env = glowIntensity * alpha;

            if (under) {
                //宽暗红焰体 + 窄橙红焰心
                sb.Draw(fire, anchor, frame, new Color(150, 36, 14, 0) * (0.55f * env),
                    0f, rootOrigin, new Vector2(wide * 1.05f, wide * 1.2f * lick), SpriteEffects.None, 0);
                sb.Draw(fire, anchor, frame, new Color(226, 92, 30, 0) * (0.5f * env),
                    0f, rootOrigin, new Vector2(wide * 0.6f, wide * 0.9f * lick), SpriteEffects.None, 0);
            }
            else {
                //上层小簇：部分覆盖鱼身上缘，完成夹心
                sb.Draw(fire, anchor + new Vector2(4f * bodyScale, 3f * bodyScale), frame,
                    new Color(232, 110, 40, 0) * (0.38f * env),
                    0.12f, rootOrigin, new Vector2(wide * 0.34f, wide * 0.5f * lick), SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 硫磺火焰弹幕：有形焰核（Fire 序列帧顺速度拉伸）+ 彗尾条带 + 熄灭点余燃残迹
    /// </summary>
    internal class BrimstoneFlameProjectile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.ai[0];
        //视觉种子在持有者侧随机决定，通过 ai[1] 同步给其它端，驱动彗尾相位与帧偏移
        private ref float VisualSeed => ref Projectile.ai[1];

        /// <summary>燃尽进度 0..1：后半程焰核收缩、彗尾变短变暗，飞行期始终有量在演化</summary>
        private float BurnProgress => MathHelper.Clamp((Timer - 55f) / 60f, 0f, 1f);

        public override void SetStaticDefaults() {
            //16 点轨迹 ≈ 16 帧彗尾
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void OnSpawn(IEntitySource source) {
            //仅持有者生成随机视觉种子，并通过下一次 netUpdate 同步出去
            if (Projectile.IsOwnedByLocalPlayer()) {
                VisualSeed = Main.rand.NextFloat();
                Projectile.netUpdate = true;
            }
        }

        public override void AI() {
            Timer++;

            //减速
            Projectile.velocity *= 0.98f;

            //轻微追踪：仅持有者修改速度，避免不同端追踪不同的最近敌人
            if (Projectile.IsOwnedByLocalPlayer() && Timer % 15 == 0 && Timer < 60) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.8f;

                    if (Projectile.velocity.Length() > 20f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    }
                    Projectile.netUpdate = true;
                }
            }

            //焰核朝速度方向，方向感由速度拉伸与彗尾编码
            Projectile.rotation = Projectile.velocity.ToRotation();

            float burn = BurnProgress;

            //硫磺火光照：燃尽走暗
            float lightMul = 1f - burn * 0.55f;
            Lighting.AddLight(Projectile.Center, 0.66f * lightMul, 0.17f * lightMul, 0.05f * lightMul);

            if (!VaultUtils.isServer) {
                SpawnFlightEffects(burn);
            }
        }

        /// <summary>飞行剥落：稀疏硫尘 + 偶发剥离余烬，燃尽越深剥得越多</summary>
        private void SpawnFlightEffects(float burn) {
            if (Main.rand.NextBool(3)) {
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    CWRID.Dust_Brimstone,
                    -Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    0, default, Main.rand.NextFloat(1.1f, 1.8f) * (1f - burn * 0.4f));
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.2f;
            }

            if (Main.rand.NextBool(burn > 0.4f ? 5 : 9)) {
                var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(Projectile.Center,
                    -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1f, 1f),
                    default, Main.rand.NextFloat(0.4f, 0.7f));
                ember?.Configure(Main.rand.Next(12, 22));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.35f,
                Pitch = 0.2f
            }, Projectile.Center);

            if (VaultUtils.isServer) {
                return;
            }

            //命中爆发：逆速度方向余烬迸溅 + 挂在命中点的余燃残焰
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 5; i++) {
                var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(Projectile.Center,
                    back.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 6.5f),
                    default, Main.rand.NextFloat(0.5f, 0.85f));
                ember?.Configure(Main.rand.Next(14, 24));
            }
            PRTLoader.NewParticle<PRT_FishBrimlishResidue>(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                target.velocity * 0.3f, default, Main.rand.NextFloat(0.34f, 0.46f))
                ?.Configure(Main.rand.Next(18, 26));

            for (int i = 0; i < 4; i++) {
                Dust brimstone = Dust.NewDustPerfect(Projectile.Center, CWRID.Dust_Brimstone,
                    back.RotatedByRandom(1.2f) * Main.rand.NextFloat(2f, 5f),
                    0, default, Main.rand.NextFloat(1.3f, 2f));
                brimstone.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.4f,
                Pitch = -0.25f
            }, Projectile.Center);

            if (VaultUtils.isServer) {
                return;
            }

            //熄灭点余燃：残焰活得比弹体久，尾声自行收缩熄灭
            int residues = Main.rand.Next(2, 4);
            for (int i = 0; i < residues; i++) {
                PRTLoader.NewParticle<PRT_FishBrimlishResidue>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)),
                    default, Main.rand.NextFloat(0.3f, 0.48f))
                    ?.Configure(Main.rand.Next(20, 32));
            }

            for (int i = 0; i < 6; i++) {
                var ember = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f),
                    default, Main.rand.NextFloat(0.45f, 0.8f));
                ember?.Configure(Main.rand.Next(16, 28));
            }

            //彗尾余像：沿旧轨迹布点驻留余烬，尾梢命短先蚀，条带不随弹体一帧消失
            //低速燃尽死时轨迹缩成一点，余像并入死点余烬，跳过防原地堆料
            if (Projectile.velocity.Length() > 3f) {
                Vector2 half = Projectile.Size / 2f;
                for (int k = 2; k < Projectile.oldPos.Length; k += 3) {
                    if (Projectile.oldPos[k] == Vector2.Zero) {
                        break;
                    }
                    float tailT = k / (float)Projectile.oldPos.Length;
                    var ghost = PRTLoader.NewParticle<PRT_FishBrimlishEmber>(
                        Projectile.oldPos[k] + half + Main.rand.NextVector2Circular(3f, 3f),
                        Main.rand.NextVector2Circular(0.5f, 0.5f),
                        default, Main.rand.NextFloat(0.4f, 0.6f) * (1f - tailT * 0.4f));
                    ghost?.Configure(6 + (int)((1f - tailT) * 10f), 0.02f);
                }
            }

            PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center, new Vector2(0f, -0.4f)
                , default, Main.rand.NextFloat(0.3f, 0.42f))
                ?.Configure(Main.rand.Next(28, 42), new Color(96, 30, 18), new Color(24, 12, 10));

            for (int i = 0; i < 5; i++) {
                Dust brimstone = Dust.NewDustPerfect(Projectile.Center, CWRID.Dust_Brimstone,
                    Main.rand.NextVector2Circular(4f, 4f),
                    0, default, Main.rand.NextFloat(1.4f, 2.2f));
                brimstone.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fire = CWRAsset.Fire?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float burn = BurnProgress;
            float speed = Projectile.velocity.Length();
            float coreScale = Projectile.scale * (1f - burn * 0.35f);
            float pulse = 0.9f + 0.1f * (float)Math.Sin(Timer * 0.55f + VisualSeed * MathHelper.TwoPi);

            //底光：暗红压底
            if (glow != null) {
                sb.Draw(glow, drawPos, null, new Color(140, 34, 14, 0) * (0.55f * (1f - burn * 0.5f)),
                    0f, glow.Size() / 2f, 0.42f * coreScale, SpriteEffects.None, 0);
            }

            //焰体剪影：Fire 序列帧顺速度方向 + 速度拉伸，暗红外缘裹橙红焰心
            if (fire != null) {
                int frameW = fire.Width / 4;
                int frameH = fire.Height / 4;
                int idx = (int)(Timer / 3f + VisualSeed * 16f) % 16;
                Rectangle frame = new(frameW * (idx % 4), frameH * (idx / 4), frameW, frameH);
                Vector2 origin = new(frameW * 0.5f, frameH * 0.5f);
                float faceRot = Projectile.rotation + MathHelper.PiOver2; //火苗贴图向上
                float stretch = 1f + MathHelper.Clamp(speed * 0.028f, 0f, 0.55f);
                Color outer = Color.Lerp(new Color(190, 50, 18, 0), new Color(96, 22, 12, 0), burn);
                Color inner = Color.Lerp(new Color(255, 128, 44, 0), new Color(182, 54, 18, 0), burn);

                //强度分配防焰心叠加过曝：外缘承亮、内芯与热芯收敛
                sb.Draw(fire, drawPos, frame, outer * (0.85f * pulse), faceRot, origin,
                    new Vector2(0.30f, 0.34f * stretch) * coreScale, SpriteEffects.None, 0);
                sb.Draw(fire, drawPos, frame, inner * (0.62f * pulse), faceRot, origin,
                    new Vector2(0.19f, 0.24f * stretch) * coreScale, SpriteEffects.None, 0);
            }

            //热芯：极小亮橙点，禁纯白
            if (glow != null) {
                sb.Draw(glow, drawPos, null, new Color(255, 172, 66, 0) * (0.6f * pulse * (1f - burn * 0.6f)),
                    0f, glow.Size() / 2f, 0.12f * coreScale, SpriteEffects.None, 0);
            }

            return false;
        }

        /// <summary>彗尾条带：沿 oldPos 轨迹的 TriangleStrip（头亮尾灭，热扰动撕边，嵌余烬火星）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ) {
                return;
            }
            Effect fx = FishBrimlishAssets.FishBrimlishComet;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            //采样点：当前中心打头，oldPos 依次向尾（去掉未写入的零槽与过近点）
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

            float burn = BurnProgress;
            //头段快速铺满宽度再向尾收尖，燃尽时整体变细
            float maxWidth = 12f * Projectile.scale * (1f - burn * 0.4f);
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float width = maxWidth * (0.5f + 0.5f * MathHelper.Clamp(t / 0.14f, 0f, 1f))
                    * MathF.Pow(1f - t, 0.78f);
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

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(VisualSeed);
            fx.Parameters["uFade"]?.SetValue(MathHelper.Clamp(Timer / 10f, 0f, 1f));
            fx.Parameters["uBurn"]?.SetValue(burn);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }
}
