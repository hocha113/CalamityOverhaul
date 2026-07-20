using CalamityOverhaul.Common;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>双鳍鳕鱼技能，周期发射游动鳕鱼：成对出膛，绕共同轴 DNA 式交织追踪</summary>
    internal class FishDoubleCod : FishSkill
    {
        public override int UnlockFishID => ItemID.DoubleCod;
        public override int DefaultCooldown => 18;
        public override int ResearchDuration => 60 * 15;

        private int shootCounter = 0;
        private static int ShootInterval => 6 - HalibutData.GetDomainLayer() / 4; //每6-4次开火触发一次

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            shootCounter++;

            if (shootCounter >= ShootInterval && Cooldown <= 0) {
                shootCounter = 0;
                SetCooldown();

                //发射游动的鳕鱼
                SpawnDoubleCodFish(player, source, position, velocity, damage, knockback);
            }

            return null;
        }

        private void SpawnDoubleCodFish(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            //发射数量随领域层数增加
            int fishCount = 2 + HalibutData.GetDomainLayer() / 4;
            int pairCount = (fishCount + 1) / 2;
            int fishDamage = (int)(damage * (0.6f + HalibutData.GetDomainLayer() * 0.15f));

            for (int p = 0; p < pairCount; p++) {
                //扇形按对散布：同对共享轴线初速，两条鱼才能绕同一轴交织
                float spreadAngle = pairCount > 1 ? MathHelper.Lerp(-0.25f, 0.25f, p / (float)(pairCount - 1)) : 0f;
                Vector2 pairVelocity = velocity.RotatedBy(spreadAngle) * Main.rand.NextFloat(0.85f, 1.1f);

                int membersInPair = Math.Min(2, fishCount - p * 2);
                int firstProj = -1;
                for (int m = 0; m < membersInPair; m++) {
                    int codProj = Projectile.NewProjectile(
                        source,
                        position,
                        pairVelocity,
                        ModContent.ProjectileType<DoubleCodProjectile>(),
                        fishDamage,
                        knockback * 0.8f,
                        player.whoAmI,
                        ai0: m * 0.5f //螺旋相位：对内两鱼相差半圈
                    );

                    if (codProj < 0) {
                        continue;
                    }
                    Main.projectile[codProj].ai[2] = -1f; //伙伴 identity，默认孤鱼
                    Main.projectile[codProj].netUpdate = true;

                    if (m == 0) {
                        firstProj = codProj;
                    }
                    else if (firstProj >= 0) {
                        //互写伙伴 identity（跨端一致的编号），受扰后彼此吸拢重新成对
                        Main.projectile[firstProj].ai[2] = Main.projectile[codProj].identity;
                        Main.projectile[codProj].ai[2] = Main.projectile[firstProj].identity;
                        Main.projectile[firstProj].netUpdate = true;
                    }
                }
            }

            //发射音效
            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.5f,
                Pitch = 0.2f
            }, position);

            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, position);

            //发射水花效果
            SpawnLaunchEffect(position, velocity);
        }

        private void SpawnLaunchEffect(Vector2 position, Vector2 direction) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = direction.SafeNormalize(Vector2.UnitX);

            //水口撑开：沿射向压扁的小水环
            FishDoubleCodVFX.SplashRing(position, dir.ToRotation(), 0.08f, 0.3f, 10);
            //出膛水花锥：受重力水珠
            FishDoubleCodVFX.DropletFan(position, dir, 7, 2.5f, 6.5f, 0.55f);
            //银鳞出水一闪
            FishDoubleCodVFX.Glints(position + dir * 12f, 2, 1.5f);

            //少量水尘垫底噪
            for (int i = 0; i < 4; i++) {
                Dust splash = Dust.NewDustPerfect(
                    position,
                    DustID.Water,
                    dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 3f),
                    100,
                    new Color(100, 180, 255),
                    Main.rand.NextFloat(1f, 1.6f)
                );
                splash.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 双鳍鳕鱼弹幕：velocity 恒为轴线速度，实际位置在轴线两侧正弦横摆，
    /// 同对两鱼相位差半圈即交织成 DNA。<br/>
    /// ai[0]=螺旋相位 0/0.5；ai[1]=Timer（每 update 递增）；ai[2]=伙伴 identity（-1 孤鱼）
    /// </summary>
    internal class DoubleCodProjectile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.DoubleCod;

        private ref float Timer => ref Projectile.ai[1];
        private ref float Phase01 => ref Projectile.ai[0];
        private ref float PartnerIdentity => ref Projectile.ai[2];

        //==== 游动参数 ====
        private const float MaxSpeed = 16f;       //每 update 上限（extraUpdates=1，保留原值）
        private const float HelixRate = 0.085f;   //螺旋角速度 rad/update，交织一圈约 37 帧
        private const float HelixAmp = 13f;       //螺旋半幅 px：小而精的双鱼间距
        private const float TurnSpring = 0.0045f; //航向弹簧刚度，欠阻尼参数对标旧 20 tick 一次的 0.24 lerp 收敛速度
        private const float TurnDamping = 0.065f; //航向阻尼：ζ≈0.49，过冲约 17% 即摆尾回正
        private const float MaxTurnRate = 0.05f;  //角速度上限 rad/update

        //==== 运动状态（由同步量确定性推演，误差交给位置同步纠正）====
        private bool motionInit;
        private float heading;           //轴线航向
        private float speed;             //轴线速率
        private float omega;             //航向角速度：摆尾/甩水/挤压的读数来源
        private Vector2 lastHelixOffset; //上帧螺旋偏移，位置差分用
        private int targetIndex = -1;
        private int partnerCache = -1;   //identity → 槽位解析缓存
        private int dropletCd;

        //==== 绘制状态（仅 PreDraw 消费）====
        private float prevDepth = 1f;
        private int glintAge;
        private float glintU;
        private int glintCountdown = 24;
        private Trail wakeTrail;

        private float SwimPhase => Timer * HelixRate + Phase01 * MathHelper.TwoPi;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 22;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private Projectile Partner {
            get {
                int id = (int)PartnerIdentity;
                if (id < 0) {
                    return null;
                }
                if (partnerCache >= 0 && partnerCache < Main.maxProjectiles) {
                    Projectile cached = Main.projectile[partnerCache];
                    if (cached.active && cached.identity == id && cached.type == Type && cached.owner == Projectile.owner) {
                        return cached;
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile cand = Main.projectile[i];
                    if (cand.active && cand.identity == id && cand.type == Type && cand.owner == Projectile.owner) {
                        partnerCache = i;
                        return cand;
                    }
                }
                //伙伴已死：identity 会被后续弹幕复用，置 -1 防误配，此后永为孤鱼
                PartnerIdentity = -1f;
                partnerCache = -1;
                return null;
            }
        }

        private static float HelixEaseOf(float timer) => MathHelper.SmoothStep(0f, 1f, Math.Min(timer / 30f, 1f));

        /// <summary>由同步量（timer/相位/轴速）推演的螺旋偏移，对伙伴同样成立</summary>
        private static Vector2 HelixOffsetOf(float timer, float phase01, Vector2 axisVel, float amp) {
            float ph = timer * HelixRate + phase01 * MathHelper.TwoPi;
            Vector2 perp = new Vector2(-axisVel.Y, axisVel.X).SafeNormalize(Vector2.Zero);
            return perp * MathF.Sin(ph) * amp;
        }

        public override void AI() {
            Timer++;

            if (!motionInit) {
                motionInit = true;
                heading = Projectile.velocity.ToRotation();
                speed = Math.Min(Projectile.velocity.Length(), MaxSpeed);
                Projectile.rotation = heading;
                //远端中途收到弹幕时网络位置已含螺旋偏移，从同步量重建防一帧跳变
                lastHelixOffset = HelixOffsetOf(Timer, Phase01, Projectile.velocity, HelixAmp * HelixEaseOf(Timer));
            }

            //==== 追踪：保留 20 update 一次的索敌节奏与 500px 范围，从轴心索敌保持对内一致 ====
            Vector2 guideCenter = Projectile.Center - lastHelixOffset;
            if (Timer % 20 == 0) {
                NPC found = guideCenter.FindClosestNPC(500f);
                targetIndex = found?.whoAmI ?? -1;
            }
            NPC target = targetIndex >= 0 && targetIndex < Main.maxNPCs && Main.npc[targetIndex].active
                ? Main.npc[targetIndex] : null;

            if (target != null) {
                //==== 摆尾式转向：欠阻尼航向弹簧，压尾-过冲-回正 ====
                float desired = (target.Center - guideCenter).ToRotation();
                float diff = MathHelper.WrapAngle(desired - heading);
                omega += diff * TurnSpring - omega * TurnDamping;
                omega = MathHelper.Clamp(omega, -MaxTurnRate, MaxTurnRate);

                //急转压速、出弯回冲：速率随角速度演化，飞行期始终有量在变
                float turnFactor = Math.Min(Math.Abs(omega) / MaxTurnRate, 1f);
                float speedTarget = MaxSpeed * (1f - 0.25f * turnFactor);
                speed = MathHelper.Lerp(speed, speedTarget, 0.02f);
            }
            else {
                omega *= 0.96f;
            }
            heading = MathHelper.WrapAngle(heading + omega);

            //速度限制
            speed = Math.Min(speed, MaxSpeed);
            Vector2 axisVel = heading.ToRotationVector2() * speed;
            Projectile.velocity = axisVel;

            //==== DNA 交织：实际位置=轴心+正弦横摆，对内相位差半圈 ====
            float amp = HelixAmp * HelixEaseOf(Timer);
            Vector2 offset = HelixOffsetOf(Timer, Phase01, axisVel, amp);
            Vector2 helixDelta = offset - lastHelixOffset;
            Projectile.position += helixDelta;
            lastHelixOffset = offset;

            //==== 对内凝聚：轴线彼此吸拢，命中减速的扰动后重新交汇成对 ====
            Projectile partner = Partner;
            if (partner != null) {
                Vector2 partnerGuide = partner.Center
                    - HelixOffsetOf(partner.ai[1], partner.ai[0], partner.velocity, HelixAmp * HelixEaseOf(partner.ai[1]));
                Vector2 pull = partnerGuide - (Projectile.Center - offset);
                float dist = pull.Length();
                if (dist > 2f) {
                    Projectile.position += pull / dist * Math.Min(dist * 0.02f, 0.9f);
                }
            }

            //旋转朝向：表观速度=轴速+横摆差分，鱼头始终顺着真实游线
            Vector2 apparentVel = axisVel + helixDelta;
            if (apparentVel.LengthSquared() > 1f) {
                Projectile.rotation = Projectile.rotation.AngleLerp(apparentVel.ToRotation(), 0.35f);
            }

            //==== 转向甩水：角速度过阈时从鱼尾向弯外甩出受重力水珠 ====
            if (dropletCd > 0) {
                dropletCd--;
            }
            if (!Main.dedServ && Math.Abs(omega) > 0.018f && dropletCd <= 0 && speed > 5f) {
                dropletCd = 10;
                Vector2 tail = Projectile.Center - Projectile.rotation.ToRotationVector2() * 14f;
                Vector2 fling = Projectile.rotation.ToRotationVector2().RotatedBy(-Math.Sign(omega) * MathHelper.PiOver2);
                FishDoubleCodVFX.DropletFan(tail, fling, 2, 1.5f, 3.5f, 0.4f);
            }

            //游动气泡：低频水底噪
            if (!Main.dedServ && Main.rand.NextBool(10)) {
                Dust bubble = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Water,
                    -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    100,
                    new Color(150, 200, 255),
                    Main.rand.NextFloat(0.7f, 1.1f)
                );
                bubble.noGravity = true;
                bubble.fadeIn = 1f;
            }

            //银蓝冷光：深度相位调制明暗（前层亮、后层暗）
            float depth = MathF.Cos(SwimPhase);
            float bright = MathHelper.Lerp(0.75f, 1.05f, depth * 0.5f + 0.5f);
            Lighting.AddLight(Projectile.Center, 0.18f * bright, 0.28f * bright, 0.38f * bright);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //咬合微滞：2 tick 定帧点出咬中的一瞬
            target.CWR().TimeFrozenTick = 2;

            //咬合减速：轴速瞬降，弹簧随后自然回冲；对内凝聚会把掉队的鱼拉回编队
            speed *= 0.86f;

            if (!Main.dedServ) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                //贯穿点水花：顺速度锥形甩珠 + 压扁水环 + 银鳞两闪
                FishDoubleCodVFX.DropletFan(Projectile.Center, dir, 5, 2f, 6f, 0.6f);
                FishDoubleCodVFX.SplashRing(Projectile.Center, dir.ToRotation(), 0.08f, 0.26f, 10);
                FishDoubleCodVFX.Glints(Projectile.Center, 2, 2.5f);
                for (int i = 0; i < 4; i++) {
                    Dust splash = Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Water,
                        dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(2f, 5f),
                        100,
                        new Color(120, 200, 255),
                        Main.rand.NextFloat(1.2f, 1.8f)
                    );
                    splash.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.NPCHit25 with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.25f,
                Pitch = 0.55f,
                MaxInstances = 3
            }, Projectile.Center);

            //减少穿透
            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //反弹（tileCollide=false 常闭，保留骨架）
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.8f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.8f;
            }
            heading = Projectile.velocity.ToRotation();

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);

            //反弹水花
            FishDoubleCodVFX.DropletFan(Projectile.Center, -oldVelocity.SafeNormalize(Vector2.UnitY), 4, 1.5f, 4f, 0.7f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);
            //银鳞碎响：轻脆一声垫在水声上
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.22f,
                Pitch = 0.6f,
                MaxInstances = 3
            }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }

            //鱼化作水：留在原地的水痕比弹体活得久，尾端先蚀缓缓下沉
            PRTLoader.NewParticle<PRT_FishDoubleCodWake>(Projectile.Center, Vector2.Zero, default, 1f)
                ?.Configure(Projectile, 12f, 9f, 15);

            //上抛水珠弧 + 银鳞三闪 + 扁水环
            FishDoubleCodVFX.DropletFan(Projectile.Center, -Vector2.UnitY, 8, 2f, 5.5f, 1.1f);
            FishDoubleCodVFX.Glints(Projectile.Center, 3, 3f);
            FishDoubleCodVFX.SplashRing(Projectile.Center, Projectile.velocity.ToRotation(), 0.1f, 0.34f, 12);

            for (int i = 0; i < 5; i++) {
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    new Color(120, 200, 255),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                splash.noGravity = Main.rand.NextBool();
            }
        }

        //==== 绘制 ====

        /// <summary>水尾流剖面：鱼尾根细、随后鼓起再收尖，慢游时几乎隐去</summary>
        public float GetWakeWidth(float completionRatio) {
            float speedFade = MathHelper.Clamp(Projectile.velocity.Length() / 10f, 0.2f, 1f);
            float rise = MathHelper.Clamp(completionRatio / 0.2f, 0f, 1f);
            return MathF.Pow(rise, 0.6f) * MathF.Pow(1f - completionRatio, 0.9f) * 9f * speedFade;
        }

        public Color GetWakeColor(Vector2 coord) {
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / MaxSpeed, 0f, 1f);
            return Color.White * ((0.5f + 0.5f * speedT) * (1f - coord.X * 0.45f));
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = FishDoubleCodAssets.FishDoubleCodWake;
            if (fx == null || !Projectile.active) {
                return;
            }
            FishDoubleCodVFX.ApplyWake(fx, Projectile.whoAmI * 0.43f + Phase01);
            FishDoubleCodVFX.DrawWakeTrail(Projectile, ref wakeTrail, GetWakeWidth, GetWakeColor, fx, 12f);
        }

        /// <summary>diagonal 贴图的朝向换算：左行水平翻转防倒游</summary>
        private static float DrawRotOf(float head, bool faceLeft)
            => faceLeft ? head + MathHelper.Pi * 0.75f : head + MathHelper.PiOver4;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fishTex = TextureAssets.Item[ItemID.DoubleCod].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;

            //深度相位：cos>0 读作前层、<0 读作后层，两鱼交替遮换出 DNA 立体感
            float depth = MathF.Cos(SwimPhase);
            float depthScale = MathHelper.Lerp(0.94f, 1.05f, depth * 0.5f + 0.5f);
            TickGlint(depth);

            float head = Projectile.rotation;
            bool faceLeft = MathF.Cos(head) < 0f;
            SpriteEffects flip = faceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float drawRot = DrawRotOf(head, faceLeft);

            //挤压拉伸：直线随速拉长、急转压短（摆尾前的蓄力），身体波浪保留
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / MaxSpeed, 0f, 1f);
            float turnT = Math.Min(Math.Abs(omega) / MaxTurnRate, 1f);
            float stretch = speedT * 0.1f - turnT * 0.12f;
            float wave = MathF.Sin(Timer * 0.22f + Phase01 * MathHelper.TwoPi) * 0.06f;
            Vector2 scale = new Vector2(1f + stretch, 1f + wave - stretch * 0.5f) * Projectile.scale * depthScale;

            //底层暗水晕：极淡只托轮廓，不作 body
            Texture2D soft = CWRAsset.SoftGlow?.Value;
            if (soft != null) {
                Main.EntitySpriteDraw(soft, drawPos, null, FishDoubleCodVFX.Deep with { A = 0 } * 0.35f, 0f
                    , soft.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            }

            //速度残影链：位移各向异性（旧位置两枚渐隐鱼影，快时才显）
            Color ghostCol = FishDoubleCodVFX.Scale with { A = 0 };
            for (int i = 2; i <= 4; i += 2) {
                if (i >= Projectile.oldPos.Length) {
                    break;
                }
                Vector2 gp = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float gRot = i < Projectile.oldRot.Length ? Projectile.oldRot[i] : head;
                bool gLeft = MathF.Cos(gRot) < 0f;
                Main.EntitySpriteDraw(fishTex, gp, null, ghostCol * ((0.26f - i * 0.045f) * speedT)
                    , DrawRotOf(gRot, gLeft), origin, scale * (1f - i * 0.02f)
                    , gLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }

            //主体：银鳞冷调轻染，后层压暗（乘法调暗不加亮，防过曝）
            Color mainColor = Color.Lerp(lightColor, FishDoubleCodVFX.Scale, 0.3f)
                * MathHelper.Lerp(0.8f, 1f, depth * 0.5f + 0.5f);
            Main.EntitySpriteDraw(fishTex, drawPos, null, mainColor, drawRot, origin, scale, flip, 0);

            //银鳞单帧小闪：高光瞬现即灭，非常驻
            if (glintAge > 0) {
                Texture2D star = CWRAsset.StarGlow01?.Value;
                if (star != null) {
                    Vector2 gpos = drawPos + head.ToRotationVector2() * (glintU * 20f);
                    float ga = glintAge == 2 ? 1f : 0.4f;
                    Main.EntitySpriteDraw(star, gpos, null, FishDoubleCodVFX.Spec with { A = 0 } * ga
                        , glintU * 9f, star.Size() / 2f, 0.11f + 0.05f * ga, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        /// <summary>闪点调度：随机间隔 + 深度换层瞬间（两鱼交错换位时银鳞接光）</summary>
        private void TickGlint(float depth) {
            if (glintAge > 0) {
                glintAge--;
            }
            bool crossed = prevDepth < 0f != depth < 0f;
            prevDepth = depth;
            if (glintCountdown-- <= 0 || (crossed && glintAge <= 0 && Main.rand.NextBool())) {
                glintCountdown = Main.rand.Next(20, 50);
                glintAge = 2;
                glintU = Main.rand.NextFloat(-0.5f, 0.5f);
            }
        }
    }
}
