using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// <summary>公主鱼技能，召唤公主鱼释放魔法攻击</summary>
    internal class FishPrincess : FishSkill
    {
        public override int UnlockFishID => ItemID.PrincessFish;
        public override int DefaultCooldown => 50 - HalibutData.GetDomainLayer() * 2;
        public override int ResearchDuration => 60 * 22;

        //活跃的公主鱼追踪
        private static readonly List<int> ActivePrincessFish = new();
        private static int MaxPrincessFish => 3 + HalibutData.GetDomainLayer() / 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFish();

                if (ActivePrincessFish.Count < MaxPrincessFish) {
                    //在玩家周围随机位置生成公主鱼
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(200f, 300f);
                    Vector2 spawnPos = player.Center + angle.ToRotationVector2() * distance;

                    //将鱼的索引通过ai2传递
                    int fishProj = Projectile.NewProjectile(
                        source,
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<PrincessFishMinion>(),
                        (int)(damage * (0.2f + HalibutData.GetDomainLayer() * 0.05f)),
                        knockback * 1.5f,
                        player.whoAmI,
                        ai2: ActivePrincessFish.Count //通过ai2传递索引
                    );

                    if (fishProj >= 0 && fishProj < Main.maxProjectiles) {
                        ActivePrincessFish.Add(fishProj);
                        SpawnSummonEffect(spawnPos);

                        //召唤音效
                        SoundEngine.PlaySound(SoundID.Item29 with {
                            Volume = 0.6f,
                            Pitch = 0.4f
                        }, spawnPos);

                        SoundEngine.PlaySound(SoundID.Item82 with {
                            Volume = 0.5f,
                            Pitch = 0.3f
                        }, spawnPos);
                    }
                }
            }

            return null;
        }

        private static void CleanupInactiveFish() {
            ActivePrincessFish.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<PrincessFishMinion>();
            });
        }

        /// <summary>召唤落点演出：圆点环 + 星尘 + 三色闪光 + 薰衣草扩散环，粉彩绘本开场</summary>
        private static void SpawnSummonEffect(Vector2 position) {
            if (Main.dedServ) {
                return;
            }
            FishPrincessVFX.DotBurst(position, 10, 5f);
            FishPrincessVFX.Stardust(position, Vector2.Zero, 6, 1.6f);
            for (int i = 0; i < 3; i++) {
                FishPrincessVFX.Glint(position + Main.rand.NextVector2Circular(18f, 18f)
                    , Main.rand.NextVector2Circular(2f, 2f), FishPrincessVFX.Pastel(i), Main.rand.NextFloat(0.8f, 1.2f));
            }
            PRTLoader.NewParticle<PRT_DWave>(position, Vector2.Zero, FishPrincessVFX.Lavender, 0.10f)
                ?.Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.42f, 14);
        }
    }

    /// <summary>
    /// 公主鱼召唤物弹幕。<br/>
    /// 演出：粉彩身份色（按索引取三色之一）+ 双缎带随游动 DNA 式旋绕（一条画在鱼身下，三明治）
    /// + 速度拉伸残影链 + 星尘低频掉落；施法有预告拍（嘴前符印展开 + 星尘向心 + 轻音提示）
    /// </summary>
    internal class PrincessFishMinion : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.PrincessFish;

        //状态
        private enum FishState
        {
            Spawning,    //生成阶段
            Following,   //跟随玩家
            Targeting,   //锁定目标
            Attacking    //攻击阶段
        }

        private FishState State {
            get => (FishState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float AttackCooldown => ref Projectile.ai[1];
        private ref float FishIndex => ref Projectile.ai[2];
        private ref float StateTimer => ref Projectile.localAI[0];

        private int targetNPCID = -1;
        private Vector2 idleOffset = Vector2.Zero;
        private float orbitAngle = 0f;
        private float floatPhase = 0f;

        //视觉状态
        private float glowIntensity = 0f;
        private float ribbonRelax = 1f;
        private int volleyParity = 0;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 18;

        //攻击参数
        private const float SearchRange = 1400f;
        private const int AttackInterval = 90;
        private const int SpawningDuration = 20;
        /// <summary>施法仪式帧窗：0..RitualFire 预告，RitualFire 帧释放</summary>
        private const int RitualFire = 14;

        private Color PastelIdentity => FishPrincessVFX.Pastel((int)FishIndex);

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            floatPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishPrincess>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            StateTimer++;

            //状态机
            switch (State) {
                case FishState.Spawning:
                    SpawningAI();
                    break;
                case FishState.Following:
                    FollowingAI(owner);
                    break;
                case FishState.Targeting:
                    TargetingAI(owner);
                    break;
                case FishState.Attacking:
                    AttackingAI(owner);
                    break;
            }

            //更新拖尾
            UpdateTrail();

            //缎带松紧：施法预告时收紧
            float relaxTarget = State == FishState.Attacking && StateTimer < RitualFire + 4 ? 0.45f : 1f;
            ribbonRelax = MathHelper.Lerp(ribbonRelax, relaxTarget, 0.18f);

            //粉彩身份色照明，亮度克制
            Lighting.AddLight(Projectile.Center, PastelIdentity.ToVector3() * 0.35f);

            //星尘低频掉落
            if (Main.rand.NextBool(10)) {
                FishPrincessVFX.Stardust(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , Projectile.velocity * 0.15f, 1, 0.5f);
            }

            //攻击冷却
            if (AttackCooldown > 0) AttackCooldown--;
        }

        private void SpawningAI() {
            float progress = StateTimer / SpawningDuration;

            //淡入 + 过冲弹入
            Projectile.alpha = (int)((1f - progress) * 255f);
            Projectile.scale = FishPrincessVFX.EaseOutBack(progress);

            //向上浮现
            Projectile.velocity.Y = -2f * (1f - progress);
            Projectile.velocity.X *= 0.9f;

            glowIntensity = progress;

            //入场两拍：起手星尘、落定闪光
            if ((int)StateTimer == 2) {
                FishPrincessVFX.Stardust(Projectile.Center, Vector2.Zero, 4, 1.4f);
            }
            if ((int)StateTimer == SpawningDuration - 2) {
                FishPrincessVFX.Glint(Projectile.Center, Vector2.Zero, PastelIdentity, 1f);
            }

            if (StateTimer >= SpawningDuration) {
                State = FishState.Following;
                StateTimer = 0;
                Projectile.alpha = 0;
                Projectile.scale = 1f;
            }
        }

        private void FollowingAI(Player owner) {
            UpdateIdleOffset();

            //环绕玩家
            orbitAngle += 0.02f;
            Vector2 orbitPos = owner.Center +
                new Vector2(
                    (float)Math.Cos(orbitAngle + FishIndex) * 150f,
                    (float)Math.Sin(orbitAngle + FishIndex * 0.7f) * 100f
                ) + idleOffset;

            //平滑移动
            Vector2 toTarget = orbitPos - Projectile.Center;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 0.08f, 0.2f);

            //旋转朝向移动方向
            if (Projectile.velocity.LengthSquared() > 0.5f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation(),
                    0.15f
                );
            }

            //待机呼吸
            Projectile.scale = 1f + (float)Math.Sin(floatPhase * 1.4f) * 0.035f;
            glowIntensity = 0.6f + (float)Math.Sin(StateTimer * 0.1f) * 0.2f;

            //搜索敌人
            if (AttackCooldown <= 0) {
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = FishState.Targeting;
                    StateTimer = 0;
                }
            }
        }

        private void TargetingAI(Player owner) {
            if (!IsTargetValid()) {
                State = FishState.Following;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //移动到目标上方
            Vector2 attackPos = target.Center + new Vector2(0, -200f);
            Vector2 toAttackPos = attackPos - Projectile.Center;

            Projectile.velocity = Vector2.Lerp(
                Projectile.velocity,
                toAttackPos.SafeNormalize(Vector2.Zero) * 14f,
                0.15f
            );

            //旋转朝向目标
            Projectile.rotation = MathHelper.Lerp(
                Projectile.rotation,
                (target.Center - Projectile.Center).ToRotation(),
                0.2f
            );

            glowIntensity = 0.8f + (float)Math.Sin(StateTimer * 0.3f) * 0.2f;

            //到达位置后开始攻击
            if (Vector2.Distance(Projectile.Center, attackPos) < 100f && StateTimer > 25) {
                State = FishState.Attacking;
                StateTimer = 0;
            }
        }

        private void AttackingAI(Player owner) {
            if (!IsTargetValid()) {
                State = FishState.Following;
                StateTimer = 0;
                AttackCooldown = AttackInterval;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //保持位置
            Projectile.velocity *= 0.9f;
            glowIntensity = 1f;

            //施法仪式预告拍：轻音提示 + 上浮摇摆 + 星尘向心
            if ((int)StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.25f, Pitch = 0.75f }, Projectile.Center);
            }
            if (StateTimer < RitualFire) {
                if (StateTimer < 8) {
                    Projectile.velocity.Y -= 0.05f;
                }
                Projectile.rotation += (float)Math.Sin(StateTimer * 0.55f) * 0.02f;

                int st = (int)StateTimer;
                if (!Main.dedServ && (st == 3 || st == 7 || st == 11)) {
                    Vector2 mouth = SigilPos();
                    Vector2 spawn = mouth + Main.rand.NextVector2CircularEdge(24f, 24f);
                    PRTLoader.NewParticle<PRT_FishPrincessMote>(spawn
                        , (mouth - spawn).SafeNormalize(Vector2.Zero) * 2.4f
                        , PastelIdentity, 0.9f)?.Configure(22);
                }
            }

            //释放拍：发射 + 后坐 + 枪口圆点
            if ((int)StateTimer == RitualFire) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    LaunchMagicAttack(target);
                }
                Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity -= aim * 2.4f;
                FishPrincessVFX.DotBurst(SigilPos(), 6, 3.5f, (int)FishIndex);
                FishPrincessVFX.Glint(SigilPos(), aim * 2f, FishPrincessVFX.Cream, 0.9f);
            }

            //攻击持续时间
            if (StateTimer >= 45) {
                State = FishState.Following;
                StateTimer = 0;
                AttackCooldown = AttackInterval - HalibutData.GetDomainLayer() * 8;
                volleyParity ^= 1;
            }
        }

        private void LaunchMagicAttack(NPC target) {
            //发射多个符号魔法弹
            int projectileCount = 3 + HalibutData.GetDomainLayer() / 4;

            for (int i = 0; i < projectileCount; i++) {
                //计算预判位置
                Vector2 targetPos = target.Center + target.velocity * 20f;
                Vector2 toTarget = targetPos - Projectile.Center;

                //添加扇形散射
                float spreadAngle = MathHelper.Lerp(-0.3f, 0.3f, i / (float)(projectileCount - 1));
                Vector2 velocity = toTarget.SafeNormalize(Vector2.Zero).RotatedBy(spreadAngle) * 18f;

                //生成魔法弹，ai0 传符号索引（心/星交替 + 三色循环）
                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<PrincessMagicOrb>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    ai0: i + volleyParity
                );

                if (proj >= 0) {
                    Main.projectile[proj].netUpdate = true;
                }
            }

            //攻击音效
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.Item43 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        private void UpdateIdleOffset() {
            idleOffset.X = (float)Math.Sin(floatPhase * 0.8f) * 30f;
            idleOffset.Y = (float)Math.Cos(floatPhase * 0.6f) * 20f;
            floatPhase += 0.05f;
        }

        private void UpdateTrail() {
            //慢速时不堆点，保证缎带有跨度
            if (trailPositions.Count == 0
                || Vector2.DistanceSquared(trailPositions[0], Projectile.Center) > 9f) {
                trailPositions.Insert(0, Projectile.Center);
            }
            else {
                trailPositions[0] = Projectile.Center;
            }
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        private Vector2 SigilPos() => Projectile.Center + Projectile.rotation.ToRotationVector2() * 26f;

        /// <summary>沿拖尾点链生成缎带路径：正弦横摆，根部收拢锚定鱼体</summary>
        private int BuildRibbonPts(Span<Vector2> dst, float phase, float amp) {
            int n = Math.Min(trailPositions.Count, dst.Length);
            if (n < 3) {
                return 0;
            }
            for (int i = 0; i < n; i++) {
                Vector2 tangent = i == 0
                    ? trailPositions[0] - trailPositions[1]
                    : trailPositions[i - 1] - trailPositions[i];
                Vector2 normal = tangent.SafeNormalize(Vector2.UnitX);
                normal = new Vector2(-normal.Y, normal.X);
                float u = i / (float)(n - 1);
                float wave = (float)Math.Sin(u * 5.2f + phase + floatPhase * 1.1f);
                dst[i] = trailPositions[i] + normal * wave * amp * (0.15f + 0.85f * u) * ribbonRelax;
            }
            return n;
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                //星尘化消散 + 圆点 + 闪光 + 扩散环
                FishPrincessVFX.Stardust(Projectile.Center, Vector2.Zero, 8, 1.5f);
                FishPrincessVFX.DotBurst(Projectile.Center, 8, 3f, (int)FishIndex);
                FishPrincessVFX.Glint(Projectile.Center, new Vector2(0, -1.5f), PastelIdentity, 1.1f);
                FishPrincessVFX.Glint(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , new Vector2(0, -1f), FishPrincessVFX.Cream, 0.8f);
                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, FishPrincessVFX.Blush, 0.08f)
                    ?.Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.36f, 12);

                //缎带脱落：双带化作残迹飘散，活得比鱼久
                Span<Vector2> pts = stackalloc Vector2[MaxTrailLength];
                int n = BuildRibbonPts(pts, 0f, 7f);
                if (n >= 3) {
                    PRTLoader.NewParticle<PRT_FishPrincessRibbonFade>(Projectile.Center, Vector2.Zero, Color.White, 1f)
                        ?.Configure(pts, n, FishPrincessVFX.Lavender, FishPrincessVFX.DeepLilac, 20);
                }
                n = BuildRibbonPts(pts, MathHelper.Pi, 7f);
                if (n >= 3) {
                    PRTLoader.NewParticle<PRT_FishPrincessRibbonFade>(Projectile.Center, Vector2.Zero, Color.White, 1f)
                        ?.Configure(pts, n, FishPrincessVFX.Blush, FishPrincessVFX.Lavender, 20);
                }
            }

            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.PrincessFish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;
            Color pastel = PastelIdentity;
            float sheenPhase = Main.GlobalTimeWrappedHourly * 0.6f + Projectile.whoAmI * 0.37f;
            float ribbonAlpha = (0.4f + glowIntensity * 0.2f) * alpha;

            //缎带 A：画在鱼身下（三明治底层）
            Span<Vector2> ribbonPts = stackalloc Vector2[MaxTrailLength];
            int n = BuildRibbonPts(ribbonPts, 0f, 7f);
            FishPrincessVFX.DrawRibbonSegments(sb, ribbonPts, n, 4.5f
                , FishPrincessVFX.Lavender, FishPrincessVFX.DeepLilac, ribbonAlpha, sheenPhase);

            //入场底光：仅生成阶段，薰衣草软光，克制
            if (State == FishState.Spawning) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                sb.Draw(glow, drawPos, null, (FishPrincessVFX.Lavender with { A = 0 }) * (0.28f * glowIntensity * alpha)
                    , 0f, glow.Size() / 2f, 0.6f * Projectile.scale, SpriteEffects.None, 0);
            }

            //速度拉伸残影链：近影清晰远影淡
            float spd = Projectile.velocity.Length();
            float stretch = MathF.Min(spd * 0.014f, 0.16f);
            Vector2 bodyScale = new Vector2(1f + stretch, 1f - stretch * 0.6f) * Projectile.scale;
            Color ghostTint = Color.Lerp(pastel, FishPrincessVFX.Lavender, 0.5f);
            for (int g = 0; g < 2; g++) {
                int idx = 3 + g * 4;
                if (idx >= trailPositions.Count) {
                    break;
                }
                float ghostAlpha = (g == 0 ? 0.20f : 0.10f) * alpha;
                sb.Draw(fishTex, trailPositions[idx] - Main.screenPosition, null, ghostTint * ghostAlpha
                    , Projectile.rotation + MathHelper.PiOver4, origin
                    , bodyScale * (0.96f - g * 0.06f), SpriteEffects.None, 0);
            }

            //主体：哑光，受光照，粉彩身份色轻染
            Color mainColor = Color.Lerp(lightColor, pastel, 0.30f);
            sb.Draw(fishTex, drawPos, null, mainColor * alpha
                , Projectile.rotation + MathHelper.PiOver4, origin, bodyScale, SpriteEffects.None, 0);

            //缎带 B：画在鱼身上（三明治顶层）
            n = BuildRibbonPts(ribbonPts, MathHelper.Pi, 6.5f);
            FishPrincessVFX.DrawRibbonSegments(sb, ribbonPts, n, 4f
                , FishPrincessVFX.Blush, FishPrincessVFX.Lavender, ribbonAlpha * 0.9f, sheenPhase + 0.4f);

            return false;
        }

        /// <summary>施法符印：预告拍在嘴前展开描边心/星，释放拍过冲后消失</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || State != FishState.Attacking || StateTimer > RitualFire + 2) {
                return;
            }
            float t = StateTimer;
            float half = 13f * FishPrincessVFX.EaseOutBack(MathHelper.Clamp(t / 10f, 0f, 1f)) * Projectile.scale;
            float fade = 0.85f;
            if (t > RitualFire) {
                //释放过冲：2 帧放大提亮后熄灭
                half *= 1.35f;
                fade = 1.2f - (t - RitualFire) * 0.45f;
            }
            float pulse = 0.5f + 0.5f * (float)Math.Sin(t * 0.6f);
            FishPrincessVFX.DrawSymbolQuad(SigilPos(), 0f, half, volleyParity & 1
                , PastelIdentity, pulse, fade, 1f);
        }
    }

    /// <summary>
    /// 公主鱼的符号魔法弹。<br/>
    /// 弹体即符号：心/星 SDF shader 四边形（平涂+描边+高光点），非贴纸非光球；
    /// 心直立微摆带位置残影，星自旋带旋转拖影；窄缎带拖尾 + 星尘掉落；
    /// 死亡时缎带交给独立残迹粒子尾部先蚀。ai0=符号索引（心/星交替、三色循环）
    /// </summary>
    internal class PrincessMagicOrb : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float SymbolIndex => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private float spinAngle = 0f;
        private float pulsePhase = 0f;

        private int Shape => (int)SymbolIndex & 1;
        private Color PastelFill => FishPrincessVFX.Pastel((int)SymbolIndex);

        public override void SetStaticDefaults() {
            //extraUpdates=1 → 20 点 ≈ 10 渲染帧缎带
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;
            pulsePhase += 0.09f;

            //轻微追踪
            if (Timer > 15) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.25f;

                    if (Projectile.velocity.Length() > 20f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    }
                }
            }

            //符号姿态：心直立微摆随速度倾身，星自旋随速度加转
            if (Shape == 0) {
                Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.02f, -0.35f, 0.35f)
                    + (float)Math.Sin(pulsePhase * 0.5f) * 0.10f;
            }
            else {
                spinAngle += 0.11f + Projectile.velocity.Length() * 0.003f;
                Projectile.rotation = spinAngle;
            }

            //粉彩照明，亮度克制
            Lighting.AddLight(Projectile.Center, PastelFill.ToVector3() * 0.5f);

            //星尘掉落：轻微向后脱离
            if (Main.rand.NextBool(9)) {
                FishPrincessVFX.Stardust(Projectile.Center, -Projectile.velocity * 0.06f, 1, 0.5f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //绘本印章式命中：定向椭圆环 + 圆点 + 闪光
            if (!Main.dedServ) {
                FishPrincessVFX.DotBurst(Projectile.Center, 6, 4f, (int)SymbolIndex);
                FishPrincessVFX.Glint(Projectile.Center, -Projectile.velocity * 0.1f, FishPrincessVFX.Cream, 1f);
                FishPrincessVFX.Glint(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(2f, 2f), PastelFill, 0.8f);
                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, PastelFill, 0.08f)
                    ?.Configure(new Vector2(1f, 0.55f), Projectile.velocity.ToRotation(), 0.32f, 12);
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.5f,
                Pitch = 0.4f
            }, Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            }

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
            else {
                FishPrincessVFX.DotBurst(Projectile.Center, 3, 2.2f, (int)SymbolIndex);
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);

            return false;
        }

        /// <summary>轨迹点链：当前中心打头，oldPos 依次向尾（去掉未写入的零槽与过近点）</summary>
        private int BuildTrailPts(Span<Vector2> pts) {
            Vector2 half = Projectile.Size / 2f;
            int count = 0;
            pts[count++] = Projectile.Center;
            for (int k = 0; k < Projectile.oldPos.Length && count < pts.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    break;
                }
                Vector2 p = Projectile.oldPos[k] + half;
                if (Vector2.DistanceSquared(p, pts[count - 1]) < 16f) {
                    continue;
                }
                pts[count++] = p;
            }
            return count;
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                //缎带交棒给残迹粒子：活得比弹体久，尾部先蚀 + 上飘
                Span<Vector2> pts = stackalloc Vector2[PRT_FishPrincessRibbonFade.MaxPts];
                int count = BuildTrailPts(pts);
                if (count >= 3) {
                    PRTLoader.NewParticle<PRT_FishPrincessRibbonFade>(Projectile.Center, Vector2.Zero, Color.White, 1f)
                        ?.Configure(pts, count, PastelFill, FishPrincessVFX.Lavender, 16);
                }

                FishPrincessVFX.Stardust(Projectile.Center, Vector2.Zero, 3, 1.2f);
                FishPrincessVFX.DotBurst(Projectile.Center, 4, 2.5f, (int)SymbolIndex);
                FishPrincessVFX.Glint(Projectile.Center, new Vector2(0, -1f), PastelFill, 0.9f);
            }

            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //shader 缺失兜底：粉彩软块 + 十字星，保证弹体可见
            if (FishPrincessAssets.FishPrincessSymbol == null) {
                Texture2D blob = CWRAsset.Extra_98?.Value;
                Texture2D star = PRT_FishPrincessMote.StarTex?.Value;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                if (blob != null) {
                    Main.EntitySpriteDraw(blob, drawPos, null, PastelFill * 0.9f, Projectile.rotation
                        , blob.Size() / 2f, 26f / blob.Width, SpriteEffects.None, 0);
                }
                if (star != null) {
                    Main.EntitySpriteDraw(star, drawPos, null, (FishPrincessVFX.Cream with { A = 0 }) * 0.7f
                        , Projectile.rotation, star.Size() / 2f, 0.14f, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ) {
                return;
            }

            //出生弹入：过冲缩放 + 淡入
            float spawnPop = FishPrincessVFX.EaseOutBack(MathHelper.Clamp(Timer / 12f, 0f, 1f));
            float fade = MathHelper.Clamp(Timer / 8f, 0f, 1f);

            //缎带拖尾：宽度随速度，头淡入尾收梢
            Span<Vector2> pts = stackalloc Vector2[21];
            int count = BuildTrailPts(pts);
            if (count >= 3) {
                float headWidth = 6.5f * (0.75f + Projectile.velocity.Length() * 0.02f);
                FishPrincessVFX.DrawRibbonStrip(pts[..count], count, headWidth
                    , Projectile.whoAmI * 0.61f % 1f, fade * 0.9f);
            }

            //符号体
            float half = 15f * spawnPop * (1f + (float)Math.Sin(pulsePhase) * 0.06f);
            float pulse = 0.5f + 0.5f * (float)Math.Sin(pulsePhase * 2f);
            Color fill = PastelFill;

            if (Shape == 1) {
                //星形自旋：旋转拖影双 ghost（同位滞后角）
                FishPrincessVFX.DrawSymbolQuad(Projectile.Center, Projectile.rotation - 0.48f, half * 1.02f
                    , 1, fill, 0f, fade * 0.12f);
                FishPrincessVFX.DrawSymbolQuad(Projectile.Center, Projectile.rotation - 0.24f, half * 1.01f
                    , 1, fill, 0f, fade * 0.28f);
            }
            else if (Projectile.oldPos.Length > 3 && Projectile.oldPos[3] != Vector2.Zero) {
                //心形位置残影：速度回声
                FishPrincessVFX.DrawSymbolQuad(Projectile.oldPos[3] + Projectile.Size / 2f
                    , Projectile.rotation, half * 0.92f, 0, fill, 0f, fade * 0.22f);
            }

            FishPrincessVFX.DrawSymbolQuad(Projectile.Center, Projectile.rotation, half
                , Shape, fill, pulse, fade);
        }
    }
}
