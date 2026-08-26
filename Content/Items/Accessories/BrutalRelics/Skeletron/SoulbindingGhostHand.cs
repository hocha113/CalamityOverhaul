using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Skeletron
{
    /// <summary>
    /// 掌攫处刑：魂环集满后自玩家背后探出的幽灵巨手，扑向最强敌人抓握碾轧<br/>
    /// ai[0]=目标NPC下标 ai[1]=目标类型（校验槽位复用） ai[2]=相位 0飞行/1抓握/2空振<br/>
    /// 目标身份随生成参数进生成包；抓握沿 ai[2] 同步，冲击演出各端由闩锁补演；
    /// 命中与灭杀只由拥有者端裁决（命中钩子本就只在 owner 端跑）
    /// </summary>
    internal class SoulbindingGhostHand : ModProjectile, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        #region 常量
        /// <summary>掌攫伤害，与 Tooltip 的 500 同源</summary>
        public const int GrabDamage = 500;
        /// <summary>凝聚帧数（含末段回抽蓄势）</summary>
        private const int CondenseFrames = 16;
        /// <summary>扑击初速 / 复利加速 / 速度上限</summary>
        private const float LungeSpeed = 26f;
        private const float LungeAccel = 1.045f;
        private const float LungeSpeedCap = 46f;
        /// <summary>扑击每帧最大转向（弧度）</summary>
        private const float SteerRate = 0.085f;
        /// <summary>抓握碾轧帧数与随后的消散帧数</summary>
        private const int HoldFrames = 34;
        private const int DissolveFrames = 16;
        /// <summary>禁锢时长：普通敌人 / Boss</summary>
        private const int LockFrames = 75;
        private const int BossLockFrames = 40;
        /// <summary>扑空判定：起扑后超时即空振</summary>
        private const int MissTimeout = 110;
        #endregion

        #region 状态
        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float TargetType => ref Projectile.ai[1];
        /// <summary>相位：0 飞行 / 1 抓握 / 2 空振（随同步包走）</summary>
        private ref float Phase => ref Projectile.ai[2];
        /// <summary>本端帧龄（不跨端，远端起点略滞后无碍演出）</summary>
        private ref float Age => ref Projectile.localAI[0];
        /// <summary>抓握/空振相位内的本端计时</summary>
        private ref float PhaseAge => ref Projectile.localAI[1];

        /// <summary>冲击演出闩：各端首次观测到抓握相位补演一次</summary>
        private bool impactPlayed;
        private bool launchSoundPlayed;
        /// <summary>肩根朝向平滑量（纯绘制）</summary>
        private Vector2 shoulderDir = Vector2.UnitX;
        #endregion

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Default;
        }

        private Player Owner => Main.player[Projectile.owner];
        private bool IsOwnerEndpoint => Projectile.owner == Main.myPlayer;

        private float Grow => MathHelper.Clamp(Age / CondenseFrames, 0f, 1f);

        /// <summary>当前消散进度（抓握尾段 / 空振全程），三端各自由本地相位计时得出</summary>
        private float Dissolve => (int)Phase switch {
            1 => MathHelper.Clamp((PhaseAge - HoldFrames) / DissolveFrames, 0f, 1f),
            2 => MathHelper.Clamp((PhaseAge - 4f) / 14f, 0f, 1f),
            _ => 0f,
        };

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }
            Age++;

            switch ((int)Phase) {
                case 1:
                    GrabBehavior();
                    break;
                case 2:
                    FizzleBehavior();
                    break;
                default:
                    if (Age <= CondenseFrames) {
                        CondenseBehavior(owner);
                    }
                    else {
                        LungeBehavior(owner);
                    }
                    break;
            }

            //起势音效：由速度阶跃在各端本地判定
            if (!launchSoundPlayed && Projectile.velocity.Length() > 9f && !VaultUtils.isServer) {
                launchSoundPlayed = true;
                SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 0.9f, Pitch = -0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.7f }, Projectile.Center);
            }
            //冲击演出闩：远端首次观测到抓握相位补演
            if ((int)Phase == 1 && !impactPlayed) {
                PlayImpactFx();
            }

            //凝聚期灵质吸入 / 高速期灵焰剥落（余韵活得比弹幕久）
            if (!VaultUtils.isServer) {
                if (Grow < 1f && Main.rand.NextBool(2)) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2CircularEdge(64f, 64f);
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos, (Projectile.Center - pos) * 0.13f,
                        SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(0.9f, 1.4f))?.Configure(14, 0f);
                }
                if ((int)Phase == 0 && Projectile.velocity.Length() > 9f && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        -Projectile.velocity * 0.14f,
                        SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.7f))
                        ?.Configure(Main.rand.Next(18, 30));
                }
            }

            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostCyan.ToVector3() * 0.55f * Grow * (1f - Dissolve));
        }

        #region 相位行为
        /// <summary>凝聚：锚在玩家背后成形，末 5 帧反向回抽蓄势</summary>
        private void CondenseBehavior(Player owner) {
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 26f, -10f);
            Vector2 aimDir = AimDirection(anchor);
            float reel = MathHelper.Clamp((Age - (CondenseFrames - 5f)) / 5f, 0f, 1f);
            Projectile.Center = anchor - aimDir * MathF.Pow(reel, 3f) * 16f;
            Projectile.velocity = Vector2.Zero;
            shoulderDir = Vector2.Lerp(shoulderDir, -aimDir, 0.3f).SafeNormalize(-aimDir);
        }

        /// <summary>扑击：起扑一帧定速，随后复利加速 + 受限转向追踪</summary>
        private void LungeBehavior(Player owner) {
            bool targetOk = TryGetTarget(out NPC target);

            if ((int)Age == CondenseFrames + 1) {
                //各端同帧同几何起扑；拥有者补一发同步纠偏
                Vector2 aim = targetOk
                    ? target.Center + target.velocity * 8f
                    : Projectile.Center + shoulderDir * -300f;
                Projectile.velocity = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX) * LungeSpeed;
                if (IsOwnerEndpoint) {
                    Projectile.netUpdate = true;
                }
            }
            else if (targetOk) {
                //受限转向 + 复利续力
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 cur = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                float turn = MathHelper.Clamp(MathF.Sign(cur.X * want.Y - cur.Y * want.X)
                    * MathF.Acos(MathHelper.Clamp(Vector2.Dot(cur, want), -1f, 1f)), -SteerRate, SteerRate);
                float speed = MathHelper.Clamp(Projectile.velocity.Length() * LungeAccel, LungeSpeed, LungeSpeedCap);
                Projectile.velocity = cur.RotatedBy(turn) * speed;
            }

            //目标失效：拥有者就地换最强目标，找不到就宣告空振
            if (!targetOk && IsOwnerEndpoint) {
                NPC retarget = FindStrongestNear(Projectile.Center, 700f);
                if (retarget != null) {
                    TargetIndex = retarget.whoAmI;
                    TargetType = retarget.type;
                }
                else {
                    Phase = 2f;
                    PhaseAge = 0f;
                }
                Projectile.netUpdate = true;
            }
            //扑空超时
            if (IsOwnerEndpoint && Age > CondenseFrames + MissTimeout) {
                Phase = 2f;
                PhaseAge = 0f;
                Projectile.netUpdate = true;
            }

            shoulderDir = Vector2.Lerp(shoulderDir, -Projectile.velocity.SafeNormalize(shoulderDir), 0.25f)
                .SafeNormalize(Vector2.UnitX);
        }

        /// <summary>抓握：锁在目标身上碾轧两拍，随后自肩向腕消散</summary>
        private void GrabBehavior() {
            PhaseAge++;
            Projectile.velocity = Vector2.Zero;

            if (TryGetTarget(out NPC target)) {
                Projectile.Center = target.Center;
                //碾轧拍：骨屑 + 闷响（各端本地，目标死活由同步决定所以同拍）
                if (((int)PhaseAge == 8 || (int)PhaseAge == 20) && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.65f, Pitch = -0.1f }, Projectile.Center);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_SkeleBoneChip>(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                            Main.rand.NextVector2Circular(4f, 4f) - new Vector2(0f, 2f),
                            SkeletronRenderHelper.BonePale, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(30, 55));
                    }
                    if (Main.LocalPlayer.WithinRange(Projectile.Center, 900f)) {
                        Main.LocalPlayer.CWR().GetScreenShake(2.4f);
                    }
                }
            }
            else if (PhaseAge < HoldFrames) {
                //猎物已死于掌中：提前进入消散段
                PhaseAge = HoldFrames;
            }

            if (IsOwnerEndpoint && PhaseAge > HoldFrames + DissolveFrames + 2f) {
                Projectile.Kill();
            }
        }

        /// <summary>空振：减速消散</summary>
        private void FizzleBehavior() {
            PhaseAge++;
            Projectile.velocity *= 0.86f;
            if (IsOwnerEndpoint && PhaseAge > 20f) {
                Projectile.Kill();
            }
        }
        #endregion

        #region 目标解析
        private bool TryGetTarget(out NPC target) {
            target = null;
            int idx = (int)TargetIndex;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[idx];
            //下标会被复用：类型 + 可追猎双重校验
            if (!npc.active || npc.type != (int)TargetType || !npc.CanBeChasedBy()) {
                return false;
            }
            target = npc;
            return true;
        }

        private static NPC FindStrongestNear(Vector2 center, float range) {
            NPC best = null;
            float bestScore = 0f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || !npc.CanBeChasedBy()
                    || !npc.WithinRange(center, range)) {
                    continue;
                }
                float score = npc.lifeMax * (npc.boss ? 4f : 1f);
                if (score > bestScore) {
                    bestScore = score;
                    best = npc;
                }
            }
            return best;
        }

        private Vector2 AimDirection(Vector2 from) {
            if (TryGetTarget(out NPC target)) {
                return (target.Center - from).SafeNormalize(Vector2.UnitX * Owner.direction);
            }
            return Vector2.UnitX * Owner.direction;
        }
        #endregion

        #region 判伤与命中
        /// <summary>成形且处于高速扑击段才伤人；抓握/空振后不再判伤</summary>
        public override bool? CanDamage()
            => (int)Phase == 0 && Age > CondenseFrames && Projectile.velocity.Length() > 8f ? null : false;

        /// <summary>掌攫无视防御（处刑语义）</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.ScalingArmorPenetration += 1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if ((int)Phase != 0) {
                return;
            }
            //改锁实际命中者并宣告抓握（owner 端裁决，随同步包广播）
            TargetIndex = target.whoAmI;
            TargetType = target.type;
            Phase = 1f;
            PhaseAge = 0f;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;

            //禁锢与追咒走 AddBuff，骑原版 buff 同步
            target.AddBuff(ModContent.BuffType<SoulGripLockDebuff>(), target.boss ? BossLockFrames : LockFrames);
            target.AddBuff(ModContent.BuffType<SoulbindCurseDebuff>(), 300);

            PlayImpactFx();
        }

        /// <summary>掌攫冲击帧：骨屑迸发 + 冲击环 + 距离衰减震屏（闩锁保证每端一次）</summary>
        private void PlayImpactFx() {
            impactPlayed = true;
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 pos = Projectile.Center;
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 1f, Pitch = -0.4f }, pos);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.2f }, pos);

            for (int i = 0; i < 16; i++) {
                float ang = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(0.2f);
                PRTLoader.NewParticle<PRT_SkeleBoneChip>(pos + Main.rand.NextVector2Circular(14f, 14f),
                    ang.ToRotationVector2() * Main.rand.NextFloat(3f, 8f) - new Vector2(0f, 2.5f),
                    SkeletronRenderHelper.BonePale, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(40, 80));
            }
            for (int i = 0; i < 14; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos + Main.rand.NextVector2Circular(20f, 20f),
                    Main.rand.NextVector2Circular(4.5f, 4.5f),
                    Main.rand.NextBool() ? SkeletronRenderHelper.GhostCyan : SkeletronRenderHelper.GhostDeep,
                    Main.rand.NextFloat(1.1f, 1.9f))?.Configure(Main.rand.Next(20, 34));
            }
            SoulbindingArmRender.AddPop(pos, 1.8f);

            float dist = Main.LocalPlayer.Distance(pos);
            if (dist < 900f) {
                Main.LocalPlayer.CWR().GetScreenShake(MathHelper.Lerp(7f, 0f, dist / 900f));
            }
        }

        public override void OnKill(int timeLeft) {
            //余韵：比弹幕活得久的烬屑与残焰
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Main.rand.NextVector2Circular(2.6f, 2.6f) - new Vector2(0f, 0.6f),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.7f))?.Configure(Main.rand.Next(24, 40));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_SkeleBoneChip>(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    SkeletronRenderHelper.BoneShadow, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(36, 60));
            }
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>臂身条带：玩家背后肩根 → 掌腕，拉远时加宽保持体量</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            float grow = Grow;
            float dissolve = Dissolve;
            if (grow <= 0.03f || dissolve >= 0.97f) {
                return;
            }
            Player owner = Owner;
            if (owner == null || !owner.active) {
                return;
            }

            Vector2 shoulder = owner.Center + new Vector2(-owner.direction * 24f, -12f);
            Vector2 hand = Projectile.Center + shoulderDir * 6f;
            float len = Vector2.Distance(shoulder, hand);
            float width = MathHelper.Clamp(40f + len * 0.012f, 40f, 58f);
            float curvature = MathF.Sin(Projectile.identity * 2.39996f) * 60f
                + MathF.Sin(Age * 0.05f + Projectile.identity) * 16f;

            SkeletronRenderHelper.DrawGhostArmStrip(shoulder, hand, curvature, width,
                grow, dissolve, 1f, Projectile.identity * 0.137f % 1f);
        }

        /// <summary>手掌实体与掌焰：抓握期两拍攥紧的缩放脉冲</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            float fade = (1f - Dissolve) * Grow;
            if (fade <= 0.03f) {
                return;
            }

            Vector2 fingersDir = (int)Phase == 0 && Projectile.velocity.Length() > 2f
                ? Projectile.velocity.SafeNormalize(-shoulderDir)
                : -shoulderDir;
            float rotation = fingersDir.ToRotation() + MathHelper.PiOver2;

            float scale = 1.15f * (0.7f + 0.3f * Grow);
            if ((int)Phase == 1) {
                //攥紧脉冲：8/20 两拍各一次收缩回弹
                float squeeze = MathF.Max(SqueezePulse(PhaseAge, 8f), SqueezePulse(PhaseAge, 20f));
                scale *= 1f + 0.14f * squeeze - 0.06f * MathF.Max(SqueezePulse(PhaseAge, 10f), SqueezePulse(PhaseAge, 22f));
            }

            //掌底灵鞘冷焰（顶点批，帧滞后与本体幽灵臂同款可接受）
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity * 2.3f);
            SkeletronFlameRender.Push(Projectile.Center + shoulderDir * 24f * scale, fingersDir.ToRotation(),
                new Vector2(58f, 76f) * scale * pulse, 0.4f, Projectile.identity * 0.137f, 0.25f, 0.55f * fade);
            if ((int)Phase == 1) {
                //抓握期指缝溢出的诅咒火
                SkeletronFlameRender.Push(Projectile.Center + new Vector2(0f, -14f * scale), -MathHelper.PiOver2,
                    new Vector2(30f, 44f) * scale, 0.6f, Projectile.identity * 0.311f, 0.7f, 0.6f * fade);
            }

            SkeletronRenderHelper.DrawGhostHandSprite(spriteBatch, Projectile.Center, rotation, scale, fade,
                fingersDir.X >= 0f ? 1 : -1);
        }

        /// <summary>拍点脉冲：命中拍前陡升后缓落</summary>
        private static float SqueezePulse(float age, float beat) {
            float t = age - beat;
            if (t < -3f || t > 6f) {
                return 0f;
            }
            return t < 0f ? 1f + t / 3f : 1f - t / 6f;
        }
        #endregion
    }
}
