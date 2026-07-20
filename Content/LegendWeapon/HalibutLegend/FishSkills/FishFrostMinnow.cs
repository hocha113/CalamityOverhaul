using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>寒霜鲦鱼技能，周期召唤并喷射雪花</summary>
    internal class FishFrostMinnow : FishSkill
    {
        public override int UnlockFishID => ItemID.FrostMinnow;
        public override int DefaultCooldown => (int)(90 - HalibutData.GetDomainLayer() * 4.5);
        public override int ResearchDuration => 60 * 16;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown <= 0) {
                SetCooldown();

                //在玩家侧方召唤寒霜鲦鱼
                SpawnFrostMinnowSpitter(player, source, damage, knockback);
            }

            return null;
        }

        private static void SpawnFrostMinnowSpitter(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //在玩家侧方生成
            float sideOffset = player.direction * 100f;
            Vector2 spawnPos = player.Center + new Vector2(sideOffset, -80f);

            int frostProj = Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<FrostMinnowSpitterProjectile>(),
                (int)(damage * (0.8f + HalibutData.GetDomainLayer() * 0.2f)),
                knockback,
                player.whoAmI
            );

            if (frostProj >= 0) {
                Main.projectile[frostProj].netUpdate = true;
            }

            //寒霜召唤音效
            SoundEngine.PlaySound(SoundID.Item28 with {
                Volume = 0.5f,
                Pitch = -0.4f
            }, spawnPos);

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, spawnPos);
        }
    }

    /// <summary>
    /// 寒霜鲦鱼喷射器弹幕。
    /// 实体生命周期：凝华入场（霜雾收束+镜面闪落定）、蓄力（嘴前六角晶核成型+寒气收束）、
    /// 三口连喷（按脉冲后坐+缩身）、化雾退场（碎晶剥落，禁 pop-out）
    /// </summary>
    internal class FrostMinnowSpitterProjectile : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FrostMinnow;

        private enum FishState
        {
            Appearing,   //出现
            Charging,    //蓄力
            Spitting,    //喷射
            Fading       //消失
        }

        private ref float StateRaw => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeProgress => ref Projectile.localAI[0];

        private FishState State {
            get => (FishState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private float glowIntensity = 0f;
        private float pulsePhase = 0f;
        private float burstKick = 0f;
        private bool chargeGlintFired = false;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 6;

        //状态持续时间
        private const int AppearDuration = 18;
        private const int ChargeDuration = 28;
        private const int SpitDuration = 45;
        private const int FadeDuration = 22;

        //攻击参数
        private const float SearchRange = 1400f;
        private const int VolleyCount = 3; //连喷口数, 总弹量不变按 i%VolleyCount 分口
        private static int SnowflakeCount => 6 + HalibutData.GetDomainLayer() / 2; //喷射雪花数量

        private Vector2 MouthPos => Projectile.Center + Projectile.rotation.ToRotationVector2() * 18f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 38;
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

        public override bool? CanDamage() => false; //鱼本身不造成伤害，只有雪花造成伤害

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            pulsePhase += 0.18f;
            burstKick *= 0.86f;

            //状态机
            switch (State) {
                case FishState.Appearing:
                    AppearingBehavior(Owner);
                    break;
                case FishState.Charging:
                    ChargingBehavior();
                    break;
                case FishState.Spitting:
                    SpittingBehavior();
                    break;
                case FishState.Fading:
                    FadingBehavior();
                    break;
            }

            //更新拖尾
            UpdateTrail();

            //寒霜环境光照: 冷蓝压低明度
            float pulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.2f * pulse * glowIntensity, 0.4f * pulse * glowIntensity, 0.62f * pulse * glowIntensity);

            //低伏霜雾绕体下淌
            if (glowIntensity > 0.3f) {
                if (Main.rand.NextBool(9)) {
                    FrostMinnowVFX.Mist(Projectile.Center + Main.rand.NextVector2Circular(24f, 18f)
                        , new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), 0.1f), Main.rand.NextFloat(0.2f, 0.28f)
                        , Main.rand.Next(26, 34), 0.16f);
                }
                if (Main.rand.NextBool(12)) {
                    Dust frost = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(22f, 22f)
                        , DustID.IceTorch, Main.rand.NextVector2Circular(0.8f, 0.8f), 0
                        , new Color(200, 230, 255), Main.rand.NextFloat(0.9f, 1.3f));
                    frost.noGravity = true;
                }
            }

            //旋转朝向目标
            if (State == FishState.Charging || State == FishState.Spitting) {
                if (IsTargetValid()) {
                    NPC target = Main.npc[targetNPCID];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.rotation = toTarget.ToRotation();
                }
                else {
                    Vector2 toTarget = InMousePos - Projectile.Center;
                    Projectile.rotation = toTarget.ToRotation();
                }
            }
        }

        private void AppearingBehavior(Player owner) {
            float progress = StateTimer / AppearDuration;

            //淡入, 尺寸带过冲落定
            Projectile.alpha = (int)(255 * (1f - progress));
            glowIntensity = progress;
            Projectile.scale = FrostMinnowVFX.EaseOutBack(progress) * 0.8f;

            //出场即面向鼠标侧
            Projectile.rotation = InMousePos.X >= Projectile.Center.X ? 0f : MathHelper.Pi;

            //凝华上浮
            Projectile.Center += new Vector2(0f, -(1f - progress) * 0.5f);
            float floatY = (float)Math.Sin(pulsePhase * 1.2f) * 2.5f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.1f);

            //霜雾向体心收束凝华
            if (StateTimer % 3 == 0) {
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2CircularEdge(38f, 38f) * Main.rand.NextFloat(0.85f, 1.2f);
                    Dust frost = Dust.NewDustPerfect(pos, DustID.IceTorch, (Projectile.Center - pos) * 0.11f
                        , 0, new Color(200, 230, 255), Main.rand.NextFloat(1.2f, 1.9f));
                    frost.noGravity = true;
                }
            }
            if (Main.rand.NextBool(6)) {
                FrostMinnowVFX.Mist(Projectile.Center + Main.rand.NextVector2Circular(18f, 18f)
                    , new Vector2(0f, 0.1f), 0.22f, 24, 0.15f);
            }

            //凝华完成拍: 镜面闪+扩散环落定
            if (StateTimer == AppearDuration - 1) {
                FrostMinnowVFX.Glint(Projectile.Center, 0.55f, 8);
                FrostMinnowVFX.ImpactRing(Projectile.Center, 0f, 0.05f, 0.22f, 10);
            }

            if (StateTimer >= AppearDuration) {
                State = FishState.Charging;
                StateTimer = 0;

                //搜索目标
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                }
            }
        }

        private void ChargingBehavior() {
            float progress = StateTimer / ChargeDuration;
            ChargeProgress = progress;

            //蓄力时发光强度增加
            glowIntensity = 0.6f + progress * 0.4f;

            //寒气凝聚效果
            float floatY = (float)Math.Sin(pulsePhase * 1.5f) * 3f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.08f);

            //冰晶逐渐聚集
            Projectile.scale = 0.8f + progress * 0.4f;

            //寒气向嘴部收束
            Vector2 mouthPos = MouthPos;
            if (Main.rand.NextBool(2)) {
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = mouthPos + Main.rand.NextVector2CircularEdge(30f, 30f) * Main.rand.NextFloat(0.8f, 1.3f);
                    Dust frost = Dust.NewDustPerfect(pos, DustID.IceTorch, (mouthPos - pos) * 0.1f
                        , 0, new Color(200, 230, 255), Main.rand.NextFloat(1.2f, 2f));
                    frost.noGravity = true;
                }
            }
            if (Main.rand.NextBool(9)) {
                FrostMinnowVFX.Mist(mouthPos + Main.rand.NextVector2Circular(14f, 14f)
                    , new Vector2(0f, 0.12f), 0.2f, 24, 0.14f);
            }

            //锁定拍: 晶核成型一记镜面闪+脆响
            if (!chargeGlintFired && progress >= 0.85f) {
                chargeGlintFired = true;
                FrostMinnowVFX.Glint(MouthPos, 0.5f, 8);
                FrostMinnowVFX.CrystalTink(MouthPos, 0.6f, 0.3f);
            }

            //蓄力音效
            if (StateTimer % 12 == 0) {
                SoundEngine.PlaySound(SoundID.Item30 with {
                    Volume = 0.3f * progress,
                    Pitch = -0.6f + progress * 0.4f
                }, Projectile.Center);
            }

            if (StateTimer >= ChargeDuration) {
                State = FishState.Spitting;
                StateTimer = 0;

                //开始喷射: 第一口
                SpitVolley(0);

                //喷射音效
                SoundEngine.PlaySound(SoundID.Item28 with {
                    Volume = 0.9f,
                    Pitch = -0.2f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.Item120 with {
                    Volume = 0.7f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
        }

        private void SpittingBehavior() {
            float progress = StateTimer / SpitDuration;

            //喷射时保持强烈发光
            glowIntensity = 1f - progress * 0.3f;

            //三口连喷拍点
            if (StateTimer == 6f) {
                SpitVolley(1);
            }
            else if (StateTimer == 12f) {
                SpitVolley(2);
            }

            //按脉冲后坐: 每口一次后挫再回弹
            Vector2 aim = Projectile.rotation.ToRotationVector2();
            Projectile.Center -= aim * burstKick * 0.9f;

            //持续漂浮
            float floatY = (float)Math.Sin(pulsePhase) * 2f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.05f);

            //喷吐余尘
            if (StateTimer < 22 && Main.rand.NextBool(2)) {
                Vector2 mouthPos = MouthPos;
                Dust frost = Dust.NewDustPerfect(mouthPos, DustID.IceTorch
                    , aim.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 8f), 0
                    , new Color(200, 230, 255), Main.rand.NextFloat(1.2f, 2f));
                frost.noGravity = true;
                if (Main.rand.NextBool(4)) {
                    FrostMinnowVFX.Mist(mouthPos, aim * Main.rand.NextFloat(1f, 2.2f), 0.22f, 20, 0.16f);
                }
            }

            if (StateTimer >= SpitDuration) {
                State = FishState.Fading;
                StateTimer = 0;
            }
        }

        private void FadingBehavior() {
            float progress = StateTimer / FadeDuration;

            //淡出
            Projectile.alpha = (int)(255 * progress);
            glowIntensity = 1f - progress;
            Projectile.scale = 1.2f - progress * 0.6f;

            //淡出消散
            Projectile.velocity.Y -= 0.15f;

            //碎晶剥落一次
            if (StateTimer == 2f) {
                FrostMinnowVFX.ChipBurst(Projectile.Center, new Vector2(0f, -0.4f), 3, 1.6f);
            }
            //身形化雾
            if (StateTimer % 4 == 0) {
                FrostMinnowVFX.Mist(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , new Vector2(0f, 0.16f), 0.26f, 22, 0.18f);
            }

            if (StateTimer >= FadeDuration) {
                Projectile.Kill();
            }
        }

        /// <summary>一口喷射：弹幕仅主人客户端生成，总量与散布角保持原公式，按 i%VolleyCount 分口</summary>
        private void SpitVolley(int volleyIndex) {
            Vector2 targetCenter = InMousePos;
            if (IsTargetValid()) {
                targetCenter = Main.npc[targetNPCID].Center;
            }
            Vector2 mouthPos = MouthPos;
            Vector2 toTarget = (targetCenter - mouthPos).SafeNormalize(Vector2.Zero);

            if (Main.myPlayer == Projectile.owner) {
                int count = SnowflakeCount;
                for (int i = 0; i < count; i++) {
                    if (i % VolleyCount != volleyIndex) {
                        continue;
                    }
                    float spreadAngle = MathHelper.Lerp(-0.6f, 0.6f, i / (float)(count - 1));
                    Vector2 velocity = toTarget.RotatedBy(spreadAngle) * Main.rand.NextFloat(10f, 16f);

                    int proj = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        mouthPos,
                        velocity,
                        ModContent.ProjectileType<FrostSnowflakeProjectile>(),
                        Projectile.damage,
                        2f,
                        Projectile.owner
                    );
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = true;
                    }
                }
            }

            //喷口演出全客户端可见
            burstKick = 1f;
            FrostMinnowVFX.Glint(mouthPos, 0.6f, 7);
            FrostMinnowVFX.ImpactRing(mouthPos, toTarget.ToRotation(), 0.06f, 0.26f, 10);
            for (int i = 0; i < 2; i++) {
                FrostMinnowVFX.Mist(mouthPos, toTarget.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.5f, 3f), 0.24f, 22, 0.18f);
            }
            for (int i = 0; i < 5; i++) {
                Dust frost = Dust.NewDustPerfect(mouthPos, DustID.IceTorch
                    , toTarget.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 10f), 0
                    , new Color(200, 230, 255), Main.rand.NextFloat(1.4f, 2.2f));
                frost.noGravity = true;
            }
            if (volleyIndex > 0) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.4f, Pitch = -0.05f + volleyIndex * 0.12f }, mouthPos);
            }
        }

        private void UpdateTrail() {
            //仅位移足够时记录, 悬停不叠影
            if (trailPositions.Count == 0 || Vector2.DistanceSquared(trailPositions[0], Projectile.Center) > 12f) {
                trailPositions.Insert(0, Projectile.Center);
                if (trailPositions.Count > MaxTrailLength) {
                    trailPositions.RemoveAt(trailPositions.Count - 1);
                }
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        public override void OnKill(int timeLeft) {
            //鱼体已在Fading阶段化雾, 此处只留轻收尾
            for (int i = 0; i < 5; i++) {
                Dust frost = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch
                    , Main.rand.NextVector2Circular(3f, 3f), 0, new Color(200, 230, 255), Main.rand.NextFloat(1.2f, 2f));
                frost.noGravity = true;
            }
            for (int i = 0; i < 2; i++) {
                FrostMinnowVFX.Mist(Projectile.Center, Main.rand.NextVector2Circular(1.2f, 1.2f), 0.26f, 26, 0.16f);
            }

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.4f,
                Pitch = -0.5f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.FrostMinnow].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            bool dir = Projectile.rotation.ToRotationVector2().X > 0;
            SpriteEffects spriteEffects = dir ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (dir ? MathHelper.PiOver4 : -MathHelper.PiOver4);

            float alpha = (255f - Projectile.alpha) / 255f;
            //连喷瞬间轻微缩身, 读作吐息后坐
            float drawScale = Projectile.scale * (1f - 0.1f * burstKick);

            //移动尾波残影
            DrawWake(sb, fishTex, origin, drawRot, spriteEffects, alpha);

            //单层冷雾底光, 只作底层
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && glowIntensity > 0.05f) {
                Color under = FrostMinnowVFX.DeepBlue;
                under.A = 0;
                sb.Draw(glow, drawPos, null, under * (0.3f * glowIntensity * alpha), 0f, glow.Size() / 2f, 1.4f * drawScale, SpriteEffects.None, 0f);
            }

            //蓄力晶核: 画在鱼身之下, 根部被鱼嘴覆盖
            if (State == FishState.Charging && ChargeProgress > 0.1f) {
                float eased = MathF.Pow(ChargeProgress, 0.8f);
                Vector2 mouthDraw = MouthPos - Main.screenPosition;
                FrostMinnowVFX.DrawHexCrystal(sb, mouthDraw, pulsePhase * 0.6f, 4f + 13f * eased
                    , 0.85f * Math.Min(ChargeProgress * 3f, 1f) * alpha, 0.3f);
            }
            //释放后晶核快速收敛, 禁pop-out
            if (State == FishState.Spitting && StateTimer <= 6f) {
                float t = StateTimer / 6f;
                Vector2 mouthDraw = MouthPos - Main.screenPosition;
                FrostMinnowVFX.DrawHexCrystal(sb, mouthDraw, pulsePhase * 0.6f, 17f * (1f - t), 0.6f * (1f - t) * alpha, 0f);
            }

            //顶缘受光: 先画上移淡拷贝再被本体覆盖, 只留背脊月牙
            sb.Draw(fishTex, drawPos + new Vector2(0f, -2f), null, FrostMinnowVFX.PaleCyan * (0.4f * glowIntensity * alpha), drawRot, origin, drawScale, spriteEffects, 0f);

            //本体冷蓝化
            Color mainColor = Color.Lerp(lightColor, FrostMinnowVFX.PaleCyan, glowIntensity * 0.5f);
            sb.Draw(fishTex, drawPos, null, mainColor * alpha, drawRot, origin, drawScale, spriteEffects, 0f);

            return false;
        }

        private void DrawWake(SpriteBatch sb, Texture2D texture, Vector2 origin, float drawRot, SpriteEffects spriteEffects, float alpha) {
            if (trailPositions.Count < 2) return;
            for (int i = 1; i < trailPositions.Count; i++) {
                float progress = 1f - i / (float)trailPositions.Count;
                Color ghost = FrostMinnowVFX.DeepBlue;
                ghost.A = 70;
                Vector2 trailPos = trailPositions[i] - Main.screenPosition;
                sb.Draw(texture, trailPos, null, ghost * (progress * alpha * 0.3f), drawRot, origin
                    , Projectile.scale * MathHelper.Lerp(0.7f, 0.95f, progress), spriteEffects, 0f);
            }
        }
    }

    /// <summary>
    /// 寒霜雪花弹幕。
    /// 六角冰晶碎片核心：暗底描边/淡青中层/极小冰芯，自旋以旋转拖影表达，
    /// 镜面闪为离散亮事件（固定受光角）；沿途剥落低伏霜雾，
    /// 命中在目标表面生长冰凌花纹 decal 并碎裂成有棱角的冰屑
    /// </summary>
    internal class FrostSnowflakeProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.ai[0];
        private float rotationSpeed = 0f;
        private float flakeScale = 1f;
        private float glintTimer = 0f;
        private int glintCooldown = 0;
        private const float GlintFrames = 4f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 140;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            rotationSpeed = Main.rand.NextFloat(0.07f, 0.25f) * (Main.rand.NextBool() ? 1f : -1f);
            flakeScale = Main.rand.NextFloat(0.85f, 1.12f);
            glintCooldown = Main.rand.Next(8, 32);
        }

        public override void AI() {
            Timer++;

            //减速
            Projectile.velocity *= 0.985f;

            //轻微下坠
            Projectile.velocity.Y += 0.08f;

            //追踪最近的敌人
            if (Timer % 18 == 0 && Timer < 70) {
                NPC target = Projectile.Center.FindClosestNPC(450f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.9f;

                    if (Projectile.velocity.Length() > 18f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 18f;
                    }
                }
            }

            //旋转
            Projectile.rotation += rotationSpeed;

            //镜面闪调度: 离散亮事件而非常亮
            if (glintTimer > 0f) {
                glintTimer--;
            }
            else if (--glintCooldown <= 0) {
                glintTimer = GlintFrames;
                glintCooldown = Main.rand.Next(26, 54);
            }

            //寒霜光照压低明度
            Lighting.AddLight(Projectile.Center, 0.16f * flakeScale, 0.3f * flakeScale, 0.5f * flakeScale);

            //低伏霜雾沿弹道剥落, 活得比弹体久
            if (Timer % 4 == 0 && Main.rand.NextBool(2)) {
                FrostMinnowVFX.Mist(Projectile.Center, -Projectile.velocity * 0.08f + new Vector2(0f, 0.12f)
                    , Main.rand.NextFloat(0.18f, 0.24f), Main.rand.Next(22, 30), 0.15f);
            }
            //少量冰尘底噪
            if (Main.rand.NextBool(6)) {
                Dust frost = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), DustID.IceTorch
                    , -Projectile.velocity * 0.2f, 0, new Color(200, 230, 255), Main.rand.NextFloat(0.9f, 1.4f));
                frost.noGravity = true;
            }
            if (Main.rand.NextBool(9)) {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                    , -Projectile.velocity * 0.15f, 0, default, Main.rand.NextFloat(1f, 1.6f));
                snow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冰冻敌人
            target.AddBuff(BuffID.Frostburn, 180);

            //冰凌花纹沿目标表面爬升(同目标限频), 冰晶碎裂+沿表面扁冲击环
            FrostMinnowVFX.FernOnNPC(target, Projectile.Center, Projectile.velocity);
            Vector2 fromCenter = Projectile.Center - target.Center;
            float normalRot = fromCenter.SafeNormalize(-Projectile.velocity.SafeNormalize(Vector2.UnitX)).ToRotation();
            FrostMinnowVFX.CrystalShatter(Projectile.Center, -Projectile.velocity, 0.5f, normalRot + MathHelper.PiOver2);
            FrostMinnowVFX.Glint(Projectile.Center, 0.45f, 7);

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.42f,
                Pitch = 0.3f
            }, Projectile.Center);
            FrostMinnowVFX.CrystalTink(Projectile.Center, 0.5f, 0.3f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //触地留霜花印: 被截停的轴指认撞面, 花纹沿表面切向铺开
            float tangent = Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > Math.Abs(Projectile.velocity.X - oldVelocity.X)
                ? 0f : MathHelper.PiOver2;
            FrostMinnowVFX.FernPrint(Projectile.Center, tangent, Main.rand.NextFloat(26f, 40f));
            return true;
        }

        public override void OnKill(int timeLeft) {
            //消散: 冰晶碎裂, 碎屑与霜雾活得比弹体久
            FrostMinnowVFX.CrystalShatter(Projectile.Center, -Projectile.velocity, 0.8f
                , Projectile.velocity.ToRotation() + MathHelper.PiOver2);
            FrostMinnowVFX.Glint(Projectile.Center, 0.5f, 7);
            FrostMinnowVFX.CrystalTink(Projectile.Center, -0.3f, 0.42f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //位移残影: 旧位置一枚暗蓝迷你晶体
            if (Projectile.oldPos.Length > 3 && Projectile.oldPos[3] != Vector2.Zero) {
                Vector2 ghostPos = Projectile.oldPos[3] + Projectile.Size / 2f - Main.screenPosition;
                FrostMinnowVFX.DrawHexBlades(sb, ghostPos, Projectile.oldRot[3], 8.5f * flakeScale, FrostMinnowVFX.DeepBlue, 0.14f);
            }
            //旋转拖影: 滞后角度表达自旋
            FrostMinnowVFX.DrawHexBlades(sb, drawPos, Projectile.rotation - rotationSpeed * 4f, 10.5f * flakeScale, FrostMinnowVFX.DeepBlue, 0.22f);

            //本体六角冰晶
            FrostMinnowVFX.DrawHexCrystal(sb, drawPos, Projectile.rotation, 11f * flakeScale, 1f);

            //离散镜面闪, 固定受光角
            if (glintTimer > 0f) {
                Texture2D star = CWRAsset.StarGlow01?.Value;
                if (star != null) {
                    float it = MathF.Pow(glintTimer / GlintFrames, 1.6f);
                    Vector2 so = star.Size() / 2f;
                    Color gcol = FrostMinnowVFX.PaleCyan;
                    gcol.A = 0;
                    sb.Draw(star, drawPos, null, gcol * it, -0.42f, so, new Vector2(0.85f, 0.26f) * flakeScale, SpriteEffects.None, 0f);
                    //≤2帧纯白过冲
                    if (glintTimer >= GlintFrames - 1f) {
                        Color white = Color.White;
                        white.A = 0;
                        sb.Draw(star, drawPos, null, white * (it * 0.9f), -0.42f, so, new Vector2(0.4f, 0.18f) * flakeScale, SpriteEffects.None, 0f);
                    }
                }
            }
            return false;
        }
    }
}
