using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 双生彼岸 — 双子魔眼飞镰
    /// 左键: 交替投掷魔焰眼/激光眼飞镰，沿正弦波轨迹往返切割
    /// 右键: 同时释放双镰，两镰交汇处生成融合魔眼对全场敌人发动同步攻击
    /// </summary>
    internal class GeminisTribute : ModItem
    {
        public override string Texture => CWRConstant.Item + "Melee/GeminisTribute";

        /// <summary>
        /// 下一次投掷使用哪只眼睛: 0=激光眼(Retinazer) 1=魔焰眼(Spazmatism)
        /// </summary>
        private static int nextEyeMode = 0;

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 64;
            Item.height = 64;
            Item.damage = 312;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 22;
            Item.useTime = 22;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GeminisTributeProj>();
            Item.shootSpeed = 21f;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 25, 0, 0);
            Item.crit = 8;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                //右键限制: 不能有未回收的融合魔眼
                return player.CountProjectilesOfID<GeminisTributeFusion>() == 0;
            }
            //左键限制: 场上最多两把飞镰
            return player.CountProjectilesOfID<GeminisTributeProj>() < 2;
        }

        public override float UseSpeedMultiplier(Player player) {
            //右键攻击稍慢，给予一定的冷却感
            return player.altFunctionUse == 2 ? 0.55f : 1f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX * player.direction);

            if (player.altFunctionUse == 2) {
                //右键 - 同时发射双镰，并设置交汇标记
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.85f, Pitch = -0.1f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.75f, Pitch = 0.25f }, player.Center);

                //生成两把飞镰，角度对称错开
                float spread = MathHelper.ToRadians(18);
                Vector2 vLeft = dir.RotatedBy(-spread) * velocity.Length();
                Vector2 vRight = dir.RotatedBy(spread) * velocity.Length();

                Projectile.NewProjectile(source, position, vLeft, type
                    , (int)(damage * 0.85f), knockback, player.whoAmI, ai0: 0f, ai1: 1f);
                Projectile.NewProjectile(source, position, vRight, type
                    , (int)(damage * 0.85f), knockback, player.whoAmI, ai0: 1f, ai1: 1f);

                if (CWRServerConfig.Instance.ScreenVibration) {
                    var modifier = new PunchCameraModifier(player.Center, dir, 3.5f, 5f, 8, 600f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
                return false;
            }

            //左键 - 交替发射，每次切换眼模式
            int eyeMode = nextEyeMode;
            nextEyeMode = 1 - nextEyeMode;

            SoundEngine.PlaySound(SoundID.Item71 with {
                Volume = 0.75f,
                Pitch = eyeMode == 0 ? 0.25f : -0.1f
            }, player.Center);

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, ai0: eyeMode, ai1: 0f);

            return false;
        }
    }

    /// <summary>
    /// 双生彼岸飞镰主体弹幕
    /// 阶段:
    ///   0 (Outbound) 抛出阶段, 沿正弦波路径冲向鼠标方向
    ///   1 (Hover)    悬停切割, 在敌人/远端附近来回振荡
    ///   2 (Recall)   回收阶段, 加速返回玩家
    /// </summary>
    internal class GeminisTributeProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Melee/GeminisTribute";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<GeminisTribute>()).DisplayName;

        [VaultLoaden(CWRConstant.Item + "Melee/GeminisTributeAlt")]
        private static Asset<Texture2D> AltScythe = null;

        //AI 入参
        /// <summary>0=激光眼 1=魔焰眼</summary>
        private ref float EyeMode => ref Projectile.ai[0];
        /// <summary>0=普通投掷 1=融合连击</summary>
        private ref float FusionFlag => ref Projectile.ai[1];

        //本地状态
        private int phase;
        private int phaseTimer;
        private int totalTimer;
        private float waveOffset;
        private Vector2 originPos;
        private Vector2 anchorPos;
        private float swingDir;
        private float spinRate;
        private float scytheRotation;

        //阶段时长
        private const int OutboundDuration = 28;
        private const int HoverDuration = 36;
        private const int RecallMaxDuration = 70;
        private const float MaxFlightSpeed = 26f;
        private const float HoverRange = 360f;
        private const float WaveAmplitude = 90f;
        private const float WaveFrequency = 0.18f;

        //追踪
        private NPC trackedTarget;
        private int targetRefreshTimer;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 22;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OutboundDuration + HoverDuration + RecallMaxDuration + 12;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.ArmorPenetration = 25;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //首帧初始化
            if (totalTimer == 0) {
                originPos = Owner.Center;
                waveOffset = Main.rand.NextFloat(MathHelper.TwoPi);
                swingDir = Main.rand.NextBool() ? 1f : -1f;
                spinRate = 0.55f * (Projectile.velocity.X > 0 ? 1f : -1f);
                Projectile.direction = Projectile.velocity.X > 0 ? 1 : -1;
                scytheRotation = Projectile.velocity.ToRotation();

                SpawnLaunchBurst();
            }

            totalTimer++;
            phaseTimer++;

            switch (phase) {
                case 0:
                    OutboundPhaseAI();
                    break;
                case 1:
                    HoverPhaseAI();
                    break;
                case 2:
                    RecallPhaseAI();
                    break;
            }

            //飞镰持续旋转
            scytheRotation += spinRate;

            //光照
            Vector3 lightColor = EyeMode > 0.5f
                ? new Vector3(1.1f, 0.35f, 0.15f)  // 魔焰眼:橙
                : new Vector3(0.4f, 0.6f, 1.15f);  // 激光眼:蓝
            Lighting.AddLight(Projectile.Center, lightColor);

            //追踪刷新
            if (--targetRefreshTimer <= 0) {
                trackedTarget = Projectile.Center.FindClosestNPC(900f);
                targetRefreshTimer = 12;
            }

            //每帧产生少量粒子
            EmitTrailParticle();
        }

        private void OutboundPhaseAI() {
            float t = phaseTimer / (float)OutboundDuration;

            //速度从满速 → 0.4倍, 缓出
            float speedT = 1f - (1f - t) * (1f - t);
            float currentSpeed = MathHelper.Lerp(MaxFlightSpeed, MaxFlightSpeed * 0.4f, speedT);

            //主方向
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            //侧向偏移(正弦波摆动)
            Vector2 perp = new Vector2(-forward.Y, forward.X);
            float swing = (float)Math.Sin(totalTimer * WaveFrequency + waveOffset) * WaveAmplitude * swingDir;
            float swingDelta = swing - (Projectile.localAI[0]);
            Projectile.localAI[0] = swing;

            Vector2 desiredVel = forward * currentSpeed + perp * swingDelta * 0.1f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.25f);

            //旋转加速
            float spinTarget = 0.55f * Math.Sign(Projectile.velocity.X != 0 ? Projectile.velocity.X : 1);
            spinRate = MathHelper.Lerp(spinRate, spinTarget, 0.1f);

            //切换到悬停阶段:超过悬停半径或时间到
            if (phaseTimer >= OutboundDuration || Projectile.Distance(Owner.Center) > HoverRange) {
                phase = 1;
                phaseTimer = 0;
                anchorPos = Projectile.Center;
                spinRate *= 1.4f; //切入悬停时旋转加速

                SoundEngine.PlaySound(SoundID.Item92 with {
                    Volume = 0.45f,
                    Pitch = EyeMode > 0.5f ? -0.15f : 0.2f
                }, Projectile.Center);
            }
        }

        private void HoverPhaseAI() {
            float t = phaseTimer / (float)HoverDuration;

            //优先锚定到最近敌人
            Vector2 anchor = anchorPos;
            if (trackedTarget != null && trackedTarget.active && trackedTarget.CanBeChasedBy(this)) {
                anchor = Vector2.Lerp(anchor, trackedTarget.Center, 0.5f);
            }

            //围绕锚点8字摆动
            Vector2 toOwner = (Owner.Center - anchor).SafeNormalize(Vector2.UnitX);
            Vector2 perp = new Vector2(-toOwner.Y, toOwner.X);
            float hoverPhase = totalTimer * 0.28f + waveOffset;
            float lateral = (float)Math.Sin(hoverPhase) * 95f * swingDir;
            float forward = (float)Math.Sin(hoverPhase * 0.5f) * 35f;

            Vector2 desiredPos = anchor + perp * lateral - toOwner * forward;
            Projectile.velocity = (desiredPos - Projectile.Center) * 0.25f;

            //旋转加速到峰值
            spinRate = MathHelper.Lerp(spinRate, 0.95f * Math.Sign(spinRate == 0 ? 1 : spinRate), 0.06f);

            //结束 → 回收
            if (phaseTimer >= HoverDuration) {
                phase = 2;
                phaseTimer = 0;

                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void RecallPhaseAI() {
            //加速回到玩家
            Vector2 toOwner = (Owner.Center - Projectile.Center);
            float dist = toOwner.Length();
            Vector2 dir = toOwner.SafeNormalize(Vector2.UnitX);

            float t = MathHelper.Clamp(phaseTimer / 18f, 0f, 1f);
            float speed = MathHelper.Lerp(8f, MaxFlightSpeed * 1.15f, t);

            //保留摆动尾迹(尺度更小)
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float swing = (float)Math.Sin(totalTimer * 0.22f + waveOffset) * 35f * swingDir;
            float swingDelta = swing - Projectile.localAI[0];
            Projectile.localAI[0] = swing;

            Projectile.velocity = dir * speed + perp * swingDelta * 0.1f;

            //接近玩家时收束
            if (dist < 40f || phaseTimer >= RecallMaxDuration) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                }
            }
        }

        private void EmitTrailParticle() {
            if (Main.dedServ || Main.rand.NextBool(2)) return;

            int mode = (int)EyeMode;
            Vector2 perp = Projectile.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero);
            Vector2 spawnPos = Projectile.Center + perp * Main.rand.NextFloat(-18f, 18f);
            Vector2 vel = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.5f, 3.5f);
            vel += perp * Main.rand.NextFloat(-1.5f, 1.5f);

            PRT_TwinsSpark spark = new PRT_TwinsSpark(
                spawnPos, vel,
                Main.rand.Next(14, 22),
                Main.rand.NextFloat(1.4f, 2.2f),
                mode
            );
            PRTLoader.AddParticle(spark);
        }

        private void SpawnLaunchBurst() {
            if (Main.dedServ) return;

            int mode = (int)EyeMode;
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            //定向锥形粒子
            for (int i = 0; i < 14; i++) {
                float spread = Main.rand.NextFloat(-0.55f, 0.55f);
                Vector2 vel = forward.RotatedBy(spread) * Main.rand.NextFloat(4f, 11f);
                PRT_TwinsSpark spark = new PRT_TwinsSpark(
                    Projectile.Center, vel,
                    Main.rand.Next(18, 28),
                    Main.rand.NextFloat(1.6f, 2.6f),
                    mode
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int mode = (int)EyeMode;

            SoundEngine.PlaySound(SoundID.NPCHit4 with {
                Volume = 0.5f,
                Pitch = mode == 1 ? -0.15f : 0.25f
            }, target.Center);

            //命中debuff
            if (mode == 1) {
                target.AddBuff(BuffID.OnFire, 240);
                target.AddBuff(BuffID.OnFire3, 180);
            }
            else {
                target.AddBuff(BuffID.CursedInferno, 180);
                target.AddBuff(BuffID.Frostburn, 180);
            }

            //命中爆发粒子
            Vector2 hitDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 9; i++) {
                float spread = Main.rand.NextFloat(-1f, 1f);
                Vector2 vel = hitDir.RotatedBy(spread) * Main.rand.NextFloat(4f, 9f);
                PRT_TwinsSpark spark = new PRT_TwinsSpark(
                    target.Center, vel,
                    Main.rand.Next(14, 22),
                    Main.rand.NextFloat(1.5f, 2.3f),
                    mode
                );
                PRTLoader.AddParticle(spark);
            }

            //融合连击在命中时生成融合魔眼
            if (FusionFlag > 0.5f && Projectile.IsOwnedByLocalPlayer() && Projectile.numHits == 1) {
                Vector2 fusionPos = target.Center;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), fusionPos, Vector2.Zero,
                    ModContent.ProjectileType<GeminisTributeFusion>(),
                    (int)(Projectile.damage * 0.6f), 0f, Owner.whoAmI);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;

            //回收时的环形粒子
            int mode = (int)EyeMode;
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5f);
                PRT_TwinsSpark spark = new PRT_TwinsSpark(
                    Projectile.Center, vel,
                    Main.rand.Next(12, 18),
                    Main.rand.NextFloat(1.2f, 1.8f),
                    mode
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            int mode = (int)EyeMode;
            Texture2D scytheTex = mode == 1 && AltScythe?.Value != null
                ? AltScythe.Value
                : TextureAssets.Projectile[Projectile.type].Value;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = scytheTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            SpriteEffects effects = SpriteEffects.None;

            Color twinColor = mode == 1
                ? new Color(255, 110, 35)
                : new Color(120, 200, 255);

            //残影拖尾
            DrawAfterimages(sb, scytheTex, sourceRect, origin, twinColor);

            //外圈柔光
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float glowPulse = 0.55f + (float)Math.Sin(totalTimer * 0.18f) * 0.15f;
            sb.Draw(glow, drawPos, null,
                twinColor with { A = 0 } * glowPulse * 0.55f,
                0f, glow.Size() / 2f, Projectile.scale * 0.75f,
                SpriteEffects.None, 0f);

            //飞镰本体
            sb.Draw(scytheTex, drawPos, sourceRect, Color.White,
                scytheRotation + MathHelper.PiOver4, origin,
                Projectile.scale, effects, 0f);

            //镰刀辉光叠加
            sb.Draw(scytheTex, drawPos, sourceRect,
                twinColor with { A = 0 } * 0.45f,
                scytheRotation + MathHelper.PiOver4, origin,
                Projectile.scale * 1.04f, effects, 0f);

            //核心魔眼(由 shader 程序化生成)
            DrawTwinsEye(sb, drawPos, mode);

            return false;
        }

        private void DrawAfterimages(SpriteBatch sb, Texture2D tex, Rectangle src, Vector2 origin, Color tint) {
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                float alpha = progress * progress * 0.55f;
                float scale = Projectile.scale * (0.6f + progress * 0.45f);
                Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterRot = scytheRotation - spinRate * i + MathHelper.PiOver4;

                sb.Draw(tex, afterPos, src,
                    tint with { A = 0 } * alpha,
                    afterRot, origin, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 在飞镰中心绘制由 TwinsEyeOverlay shader 程序化生成的魔眼
        /// </summary>
        private void DrawTwinsEye(SpriteBatch sb, Vector2 drawPos, int mode) {
            Effect shader = EffectLoader.TwinsEyeOverlay?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            if (shader == null || canvas == null) return;

            //计算瞳孔朝向(锁定目标方向，否则朝飞行方向)
            Vector2 lookTarget = trackedTarget != null && trackedTarget.active
                ? trackedTarget.Center
                : Projectile.Center + Projectile.velocity * 20f;
            Vector2 pupilDir = (lookTarget - Projectile.Center).SafeNormalize(Vector2.Zero);

            //标准化瞳孔偏移到 shader 输入 (-0.15..0.15)
            Vector2 pupilOffset = pupilDir * 0.085f;

            //生命周期进度
            float lifeProgress = MathHelper.Clamp(totalTimer / 110f, 0f, 1f);

            //充能/愤怒度: 融合模式下最高，悬停阶段稍高
            float bloodshot = 0.18f;
            if (FusionFlag > 0.5f) bloodshot = 0.85f;
            else if (phase == 1) bloodshot = 0.55f;

            //颜色配置
            Vector3 iris, glow, sclera;
            if (mode == 1) {
                //魔焰眼 - 橙红
                iris = new Vector3(1.0f, 0.32f, 0.12f);
                glow = new Vector3(1.0f, 0.85f, 0.45f);
                sclera = new Vector3(0.28f, 0.10f, 0.05f);
            }
            else {
                //激光眼 - 青蓝
                iris = new Vector3(0.30f, 0.65f, 1.0f);
                glow = new Vector3(0.75f, 0.85f, 1.0f);
                sclera = new Vector3(0.08f, 0.10f, 0.22f);
            }

            shader.CurrentTechnique = shader.Techniques["TwinsEye"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.whoAmI * 0.13f);
            shader.Parameters["uIntensity"]?.SetValue(1.35f + (phase == 1 ? 0.2f : 0f));
            shader.Parameters["uProgress"]?.SetValue(lifeProgress);
            shader.Parameters["uEyeMode"]?.SetValue((float)mode);
            shader.Parameters["uPupilDilation"]?.SetValue(phase == 1 ? 0.85f : 0.35f);
            shader.Parameters["uBloodshot"]?.SetValue(bloodshot);
            shader.Parameters["uPupilOffset"]?.SetValue(pupilOffset);
            //眼睛保持水平不随镰刀转动 — 提供视觉锚点
            shader.Parameters["uRotation"]?.SetValue(0f);
            shader.Parameters["uIrisColor"]?.SetValue(iris);
            shader.Parameters["uPupilGlow"]?.SetValue(glow);
            shader.Parameters["uScleraColor"]?.SetValue(sclera);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            //画布尺寸 — 魔眼在镰刀刀身的中央装饰位置
            float eyeSize = 44f;
            sb.Draw(canvas, drawPos, null, Color.White, 0f,
                canvas.Size() / 2f,
                eyeSize / canvas.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    /// <summary>
    /// 双镰交汇时生成的融合魔眼 — 在敌人身上短暂寄生并向四周轰击双色脉冲
    /// </summary>
    internal class GeminisTributeFusion : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<GeminisTribute>()).DisplayName;

        private const int Duration = 90;
        private int eyeTimer;
        private int pulseCounter;
        private Vector2 lockPos;

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
            Projectile.ArmorPenetration = 30;
        }

        public override void AI() {
            if (eyeTimer == 0) {
                lockPos = Projectile.Center;
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.9f, Pitch = -0.4f }, lockPos);
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f, Pitch = 0.3f }, lockPos);

                if (CWRServerConfig.Instance.ScreenVibration) {
                    var modifier = new PunchCameraModifier(lockPos, Vector2.UnitX, 5f, 6f, 12, 700f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                //初始爆发粒子
                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi * i / 24f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 11f);
                    PRT_TwinsSpark spark = new PRT_TwinsSpark(
                        lockPos, vel,
                        Main.rand.Next(20, 32),
                        Main.rand.NextFloat(1.8f, 2.8f),
                        i % 2
                    );
                    PRTLoader.AddParticle(spark);
                }
            }

            eyeTimer++;

            //轻微漂浮
            Projectile.Center = lockPos + new Vector2(0, (float)Math.Sin(eyeTimer * 0.12f) * 6f);

            //周期性发射脉冲攻击
            int pulseInterval = 12;
            if (eyeTimer % pulseInterval == 0 && eyeTimer < Duration - 8) {
                FirePulse();
                pulseCounter++;
            }

            //持续粒子辉光
            if (eyeTimer % 3 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);
                PRT_TwinsSpark spark = new PRT_TwinsSpark(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    vel,
                    Main.rand.Next(16, 24),
                    Main.rand.NextFloat(1.2f, 1.8f),
                    pulseCounter % 2
                );
                PRTLoader.AddParticle(spark);
            }

            Lighting.AddLight(Projectile.Center, 1.0f, 0.7f, 1.0f);
        }

        private void FirePulse() {
            int eyeMode = pulseCounter % 2; //交替发射双色脉冲

            //追踪最近敌人或随机角度
            NPC target = Projectile.Center.FindClosestNPC(1100f);
            Vector2 dir;
            if (target != null) {
                dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            }
            else {
                dir = (MathHelper.TwoPi * pulseCounter / 8f).ToRotationVector2();
            }

            //音效:激光眼尖锐 魔焰眼低沉
            SoundEngine.PlaySound((eyeMode == 0 ? SoundID.Item33 : SoundID.Item73) with {
                Volume = 0.4f,
                Pitch = eyeMode == 0 ? 0.3f : -0.2f
            }, Projectile.Center);

            //射出多发条状能量粒子(纯视觉)
            for (int i = 0; i < 5; i++) {
                float spread = Main.rand.NextFloat(-0.18f, 0.18f);
                Vector2 vel = dir.RotatedBy(spread) * Main.rand.NextFloat(14f, 22f);
                PRT_TwinsSpark spark = new PRT_TwinsSpark(
                    Projectile.Center, vel,
                    Main.rand.Next(22, 32),
                    Main.rand.NextFloat(1.8f, 2.6f),
                    eyeMode
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override bool? CanHitNPC(NPC target) {
            //仅在脉冲帧前后短窗口内可造成伤害，避免每帧持续命中
            return eyeTimer % 12 == 1 ? null : false;
        }

        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            //每次脉冲时使融合魔眼具备半径220的范围伤害
            const int range = 220;
            hitbox.Inflate(range, range);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中时随机施加双色 debuff
            if (Main.rand.NextBool()) {
                target.AddBuff(BuffID.OnFire3, 180);
            }
            else {
                target.AddBuff(BuffID.CursedInferno, 180);
            }

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                PRT_TwinsSpark spark = new PRT_TwinsSpark(
                    target.Center, vel,
                    Main.rand.Next(12, 20),
                    Main.rand.NextFloat(1.4f, 2.2f),
                    Main.rand.Next(2)
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;

            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = 0.1f }, Projectile.Center);

            //最终爆发 - 大量双色火花
            for (int i = 0; i < 36; i++) {
                float angle = MathHelper.TwoPi * i / 36f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);
                PRT_TwinsSpark spark = new PRT_TwinsSpark(
                    Projectile.Center, vel,
                    Main.rand.Next(22, 35),
                    Main.rand.NextFloat(1.8f, 3f),
                    i % 2
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //外晕(融合色 - 紫色)
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float glowPulse = 0.6f + (float)Math.Sin(eyeTimer * 0.18f) * 0.25f;
            sb.Draw(glow, drawPos, null,
                new Color(220, 150, 255, 0) * glowPulse * 0.85f,
                0f, glow.Size() / 2f, 1.6f, SpriteEffects.None, 0f);

            //双色双层光环
            Texture2D circle = CWRAsset.DiffusionCircle.Value;
            float ringScale = 0.6f + (float)Math.Sin(eyeTimer * 0.1f) * 0.18f;
            sb.Draw(circle, drawPos, null,
                new Color(255, 90, 30, 0) * 0.55f,
                Main.GlobalTimeWrappedHourly * 2f, circle.Size() / 2f,
                ringScale, SpriteEffects.None, 0f);
            sb.Draw(circle, drawPos, null,
                new Color(80, 170, 255, 0) * 0.55f,
                -Main.GlobalTimeWrappedHourly * 1.6f, circle.Size() / 2f,
                ringScale * 0.85f, SpriteEffects.None, 0f);

            //中心融合魔眼(shader)
            DrawFusionEye(sb, drawPos);

            return false;
        }

        private void DrawFusionEye(SpriteBatch sb, Vector2 drawPos) {
            Effect shader = EffectLoader.TwinsEyeOverlay?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            if (shader == null || canvas == null) return;

            //眼睛模式在 0 和 1 之间脉动以混合 — 在 shader 内部不直接支持，但通过双绘
            float lifeProgress = eyeTimer / (float)Duration;

            //先绘魔焰眼层
            DrawEyeWithMode(sb, shader, canvas, drawPos, lifeProgress, 1, 0.7f);
            //再叠加激光眼层
            DrawEyeWithMode(sb, shader, canvas, drawPos, lifeProgress, 0, 0.7f);
        }

        private void DrawEyeWithMode(SpriteBatch sb, Effect shader, Texture2D canvas
            , Vector2 drawPos, float lifeProgress, int mode, float alphaMul) {
            Vector3 iris, glow, sclera;
            if (mode == 1) {
                iris = new Vector3(1.0f, 0.32f, 0.12f);
                glow = new Vector3(1.0f, 0.85f, 0.45f);
                sclera = new Vector3(0.28f, 0.10f, 0.05f);
            }
            else {
                iris = new Vector3(0.30f, 0.65f, 1.0f);
                glow = new Vector3(0.75f, 0.85f, 1.0f);
                sclera = new Vector3(0.08f, 0.10f, 0.22f);
            }

            //瞳孔朝向最近敌人
            NPC target = Projectile.Center.FindClosestNPC(1500f);
            Vector2 pupilDir = target != null
                ? (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero)
                : Vector2.Zero;
            Vector2 pupilOffset = pupilDir * 0.085f;

            shader.CurrentTechnique = shader.Techniques["TwinsEye"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + mode * 0.5f);
            shader.Parameters["uIntensity"]?.SetValue(1.8f * alphaMul);
            shader.Parameters["uProgress"]?.SetValue(lifeProgress);
            shader.Parameters["uEyeMode"]?.SetValue((float)mode);
            shader.Parameters["uPupilDilation"]?.SetValue(0.95f);
            shader.Parameters["uBloodshot"]?.SetValue(0.95f);
            shader.Parameters["uPupilOffset"]?.SetValue(pupilOffset);
            //两只眼睛错位放置形成"双瞳"
            float offsetSide = mode == 1 ? 14f : -14f;
            Vector2 offsetPos = drawPos + new Vector2(offsetSide, 0f);
            shader.Parameters["uRotation"]?.SetValue(0f);
            shader.Parameters["uIrisColor"]?.SetValue(iris);
            shader.Parameters["uPupilGlow"]?.SetValue(glow);
            shader.Parameters["uScleraColor"]?.SetValue(sclera);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            float eyeSize = 58f;
            sb.Draw(canvas, offsetPos, null, Color.White, 0f,
                canvas.Size() / 2f,
                eyeSize / canvas.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
