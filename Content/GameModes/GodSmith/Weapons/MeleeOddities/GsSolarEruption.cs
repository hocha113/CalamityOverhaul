using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【日曜熔金锁链刃·A档】材质：太阳碎片锻成的多节链刃，节间喷日冕火舌。
    /// 签名：①保留原版环形甩击身份（穿墙、命中引爆日光、Daybroken）但补收-爆-停加速度曲线
    /// ②连续命中攒日冕，满 8 次下一鞭整条鞭路点燃连环爆 ③链体逐节自绘+鞭头日冕光斑随充能点亮
    /// </summary>
    internal class GsSolarEruption : GodSmithScheme
    {
        public override int TargetItemID => ItemID.SolarEruption;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: a looping chain-blade lash that pierces walls and detonates sunlight; " +
            "8 hits charge a corona lash that ignites its whole path";

        //日曜色板：焦暗→深红→橙金→日白 的火冷却斜坡
        internal static readonly Color SunWhite = new(255, 244, 214); //日白
        internal static readonly Color SunGold = new(255, 196, 88);   //熔金
        internal static readonly Color SunRed = new(226, 82, 40);     //日冕深红
        internal static readonly Color SunChar = new(64, 28, 20);     //焦暗链影

        internal const int CrownChargeMax = 8;

        /// <summary>日冕充能；方案单例跨玩家共享，只在 myPlayer 守门路径消费</summary>
        private int crownCharge;
        /// <summary>无命中衰减计时，只在 myPlayer 路径消费</summary>
        private int crownDecay;

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 鞭击总帧)，两者都吃攻速）
            if (HeldAlive<GsSolarEruptionHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                bool crown = crownCharge >= CrownChargeMax;
                int chargeNow = crownCharge;
                if (crown) {
                    crownCharge = 0;
                    crownDecay = 0;
                }
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsSolarEruptionHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI, chargeNow, crown ? 1f : 0f);
            }
            //全端返回 false 压掉原版鞭击；远端靠弹幕同步看到动作
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //5 秒不命中衰减一层，不清零，鼓励持续压刀
            if (crownCharge > 0 && ++crownDecay > 300) {
                crownDecay = 0;
                crownCharge--;
            }
        }

        /// <summary>held 命中回报充能（只在 owner 端被调）</summary>
        internal void AddCrownCharge() {
            crownDecay = 0;
            if (crownCharge < CrownChargeMax) {
                crownCharge++;
            }
        }

        //底伤不加成：原版全额爆炸保留 + 日冕环增益已计入 DPS 包络（约 110%~118%）
    }

    /// <summary>
    /// 日耀链鞭手持：单次完整环形甩击。收链（慢）-爆发（3f 甩满前半环）-余摆（减速回手）。
    /// 链体沿贝塞尔曲线逐节绘制（原版弹幕贴图分段采样），命中引爆日光（4f 冷却）+挂 Daybroken。<br/>
    /// ai[0]=出手时日冕充能数（鞭头光斑亮度），ai[1]=1 为日冕鞭（完成时沿鞭路生成驻焰连环爆）
    /// </summary>
    internal class GsSolarEruptionHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.SolarEruption");

        /// <summary>鞭尖最大触及（px）</summary>
        private const float MaxReach = 330f;
        /// <summary>侧弓幅度（px），出程与回程各弓一侧</summary>
        private const float BowAmp = 96f;
        /// <summary>基准鞭击总帧（除以攻速）</summary>
        private const int BaseDur = 24;

        private int lashDur = BaseDur;
        private int timer;
        private float baseAngle;
        private float bowSign = 1f;
        private int facingDir = 1;
        private float pEff;
        private float lastPEff;
        private float lateral;
        private Vector2 perp;
        private Vector2 tipPos;
        private Vector2 lastTip;
        private float bodyLean;
        private bool bodyLeanApplied;
        private bool lashSoundPlayed;
        private bool crownSpawned;
        private int blastCooldown;
        private bool sweepDamageActive;
        private readonly HashSet<int> hitNPCs = [];
        /// <summary>鞭尖轨迹环形缓存（拖尾光带用）</summary>
        private readonly Vector2[] tipTrail = new Vector2[10];
        private int tipTrailLen;

        private int CrownCharge => Math.Clamp((int)Projectile.ai[0], 0, GsSolarEruption.CrownChargeMax);
        private bool IsCrown => Projectile.ai[1] >= 1f;

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;   //穿墙是原版身份
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6; //原版 611 复击节奏
            Projectile.ownerHitCheck = false;   //允许隔墙命中，同原版
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private void InitLash() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            //出程弓侧按弹幕 identity 交替，各端一致
            bowSign = Projectile.identity % 2 == 0 ? 1f : -1f;

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            lashDur = Math.Max(14, (int)MathF.Round(BaseDur / speed));
            tipPos = lastTip = Hand;
        }

        /// <summary>收-爆-停行程曲线：前 22% 时长只走 10% 行程，26% 时长甩过 58% 行程，余下减速回手</summary>
        private static float LashCurve(float p) {
            const float gatherEnd = 0.22f, burstEnd = 0.48f;
            const float gatherP = 0.10f, burstP = 0.68f;
            if (p < gatherEnd) {
                return gatherP * SmoothStep01(p / gatherEnd);
            }
            if (p < burstEnd) {
                return MathHelper.Lerp(gatherP, burstP, SmoothStep01((p - gatherEnd) / (burstEnd - gatherEnd)));
            }
            float q = (p - burstEnd) / (1f - burstEnd);
            return MathHelper.Lerp(burstP, 1f, 1f - ((1f - q) * (1f - q)));
        }

        public override void AI() {
            if (Item.type != ItemID.SolarEruption || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            if (timer == 0) {
                InitLash();
            }
            timer++;
            if (blastCooldown > 0) {
                blastCooldown--;
            }

            float p = MathHelper.Clamp(timer / (float)lashDur, 0f, 1f);
            lastPEff = pEff;
            pEff = LashCurve(p);
            lastTip = tipPos;
            UpdateWhipGeometry();
            PushTipTrail(tipPos);

            //伤害窗：行程中段且鞭尖在动
            sweepDamageActive = pEff > 0.06f && pEff < 0.97f
                && (tipPos - lastTip).LengthSquared() > 4f;

            UpdatePose();
            HandleLashEvents(p);
            if (!VaultUtils.isServer) {
                HandleParticles();
            }

            Lighting.AddLight(tipPos, GsSolarEruption.SunGold.ToVector3() * (0.35f + 0.05f * CrownCharge));

            if (timer >= lashDur) {
                Projectile.Kill();
            }
        }

        /// <summary>鞭尖参数轨迹：出程沿瞄准向拉满、回程收回手，侧向 S 形弓出环形观感</summary>
        private void UpdateWhipGeometry() {
            Vector2 dir = baseAngle.ToRotationVector2();
            perp = dir.RotatedBy(MathHelper.PiOver2);
            float reach = MaxReach * MathF.Pow(MathF.Sin(MathHelper.Pi * pEff), 0.85f);
            lateral = bowSign * BowAmp * MathF.Sin(MathHelper.TwoPi * pEff);
            tipPos = Hand + (dir * reach) + (perp * lateral);
        }

        /// <summary>链体曲线：手→鞭尖的二次贝塞尔，控制点向弓侧顶出</summary>
        private Vector2 ChainPoint(float s) {
            Vector2 hand = Hand;
            Vector2 ctrl = hand + ((tipPos - hand) * 0.5f) + (perp * lateral * 0.85f);
            return Vector2.Lerp(Vector2.Lerp(hand, ctrl, s), Vector2.Lerp(ctrl, tipPos, s), s);
        }

        private void PushTipTrail(Vector2 tip) {
            tipTrailLen = Math.Min(tipTrailLen + 1, tipTrail.Length);
            for (int i = tipTrail.Length - 1; i > 0; i--) {
                tipTrail[i] = tipTrail[i - 1];
            }
            tipTrail[0] = tip;
        }

        /// <summary>持械姿态：手臂追鞭尖，收链后仰爆发前甩</summary>
        private void UpdatePose() {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            float armAngle = tipPos == Hand ? baseAngle : (tipPos - Hand).ToRotation();
            Owner.itemRotation = (armAngle.ToRotationVector2() * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle - MathHelper.PiOver2);

            Projectile.Center = ChainPoint(0.6f);
            Projectile.rotation = armAngle;

            (float target, float rate) = pEff switch {
                < 0.10f => (-facingDir * 0.05f, 0.25f),
                < 0.70f => (facingDir * 0.09f, 0.55f),
                _ => (0f, 0.16f),
            };
            bodyLean = MathHelper.Lerp(bodyLean, target, rate);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜钉脚底，坐骑/冲刺旋转让位</summary>
        private void ApplyBodyLean() {
            CWRPlayer modPlayer = Owner.CWR();
            if (Owner.mount.Active || (modPlayer != null && modPlayer.IsRotatingDuringDash)) {
                bodyLeanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            bodyLeanApplied = true;
        }

        private void HandleLashEvents(float p) {
            //爆发起手音：日曜鞭响，日冕鞭补一记厚响
            if (!lashSoundPlayed && p >= 0.22f) {
                lashSoundPlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item116 with { Volume = 0.75f, Pitch = IsCrown ? -0.15f : 0f }, Owner.Center);
                    if (IsCrown) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.3f }, Owner.Center);
                    }
                }
            }

            //日冕鞭：行程近满时沿鞭路放驻焰连环爆（owner 端一次）
            if (IsCrown && !crownSpawned && pEff >= 0.9f) {
                crownSpawned = true;
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), Hand,
                        baseAngle.ToRotationVector2(), ModContent.ProjectileType<GsSolarEruptionCrownProj>(),
                        Projectile.damage, Projectile.knockBack * 0.5f, Owner.whoAmI, bowSign, MaxReach);
                }
            }
        }

        /// <summary>粒子演出（已守非服务器端）：爆发期沿链身喷日冕火舌与金火星</summary>
        private void HandleParticles() {
            if (pEff <= 0.10f || pEff >= 0.92f) {
                return;
            }
            int count = IsCrown ? 3 : 1;
            for (int i = 0; i < count; i++) {
                Vector2 at = ChainPoint(Main.rand.NextFloat(0.35f, 1f));
                //原版日曜火（Torch/SolarFlare 双尘）打底
                Dust d = Dust.NewDustPerfect(at, DustID.SolarFlare, (tipPos - lastTip) * 0.15f, 100, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Vector2 sparkVel = (tipPos - lastTip) * Main.rand.NextFloat(0.2f, 0.45f);
                Color c = Main.rand.NextBool(3) ? GsSolarEruption.SunRed : GsSolarEruption.SunGold;
                PRTLoader.NewParticle<PRT_Spark>(tipPos, sparkVel, c, Main.rand.NextFloat(0.4f, 0.62f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override bool? CanDamage() => sweepDamageActive ? null : false;

        /// <summary>贪婪判定：链身逐段线判 + 贴身兜底</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!sweepDamageActive) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(8, 8);
            Vector2 hand = Hand;
            if (greedyBox.Distance(hand) <= 40f) {
                return true;
            }
            const int steps = 12;
            float collisionPoint = 0f;
            Vector2 prev = hand;
            for (int i = 1; i <= steps; i++) {
                Vector2 next = ChainPoint(i / (float)steps);
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(), prev, next, 30f, ref collisionPoint)) {
                    return true;
                }
                prev = next;
            }
            return false;
        }

        public override void CutTiles() {
            if (!sweepDamageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 prev = Hand;
            const int samples = 6;
            for (int i = 1; i <= samples; i++) {
                Vector2 next = ChainPoint(i / (float)samples);
                Utils.PlotTileLine(prev, next, 26f, DelegateMethods.CutTiles);
                prev = next;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = facingDir;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次鞭击对同一目标只转发一次外部命中钩子（喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //原版身份：命中挂 Daybroken（常量名 BuffID.Daybreak=189；AddBuff 自带跨端同步）
            target.AddBuff(BuffID.Daybreak, 300);

            if (Projectile.owner == Main.myPlayer) {
                //原版身份：命中引爆日光（4 帧冷却，全额伤害，随机尺度）
                if (blastCooldown <= 0) {
                    blastCooldown = 4;
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsSolarEruptionBlastProj>(),
                        Projectile.damage, 8f, Owner.whoAmI, 0.85f + Main.rand.NextFloat() * 1.15f);
                }
                //日冕充能回报
                if (GodSmithScheme.TryGetScheme(ItemID.SolarEruption, out GodSmithScheme scheme)
                    && scheme is GsSolarEruption solar) {
                    solar.AddCrownCharge();
                }
            }

            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsSolarEruption.SunGold, 0.2f)
                    ?.Configure(9, 0.8f);
                for (int i = 0; i < 5; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                    Color c = Main.rand.NextBool(3) ? GsSolarEruption.SunRed : GsSolarEruption.SunGold;
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(true, Main.rand.Next(12, 22));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - (2f * x));
        }

        //==================== 绘制：鞭尖拖尾光带 + 链体逐节 + 日冕光斑 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 1) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            DrawTipTrail(sb);
            DrawChain(sb, lightColor);
            DrawCoronaGlow(sb);
            return false;
        }

        /// <summary>鞭尖拖尾：轨迹缓存上铺渐灭日金光点（加色 A=0），越旧越红越小</summary>
        private void DrawTipTrail(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || pEff <= 0.10f) {
                return;
            }
            Vector2 origin = glow.Size() / 2f;
            for (int i = 0; i < tipTrailLen; i++) {
                float fade = 1f - (i / (float)tipTrail.Length);
                Color c = Color.Lerp(GsSolarEruption.SunRed, GsSolarEruption.SunGold, fade) * (0.34f * fade);
                c.A = 0;
                sb.Draw(glow, tipTrail[i] - Main.screenPosition, null, c, 0f, origin,
                    (0.42f + (0.3f * fade)) * (IsCrown ? 1.5f : 1f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>链体：原版弹幕贴图分段采样（头 0,2,40 / 刺 0,46,18 / 链 0,68,18），沿贝塞尔切向逐节铺</summary>
        private void DrawChain(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SolarWhipSword);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SolarWhipSword].Value;
            Rectangle chainRect = new(0, 68, tex.Width, 18);
            Rectangle spikeRect = new(0, 46, tex.Width, 18);
            Rectangle headRect = new(0, 2, tex.Width, 40);

            float fadeIn = MathHelper.Clamp(timer / 3f, 0f, 1f);
            float chainLen = 0f;
            const int segs = 16;
            Vector2 prev = Hand;
            for (int i = 1; i <= segs; i++) {
                Vector2 next = ChainPoint(i / (float)segs);
                chainLen += Vector2.Distance(prev, next);
                prev = next;
            }
            if (chainLen < 8f) {
                return;
            }

            //逐节：链节打底，每第 3 节叠日棘
            prev = Hand;
            for (int i = 1; i <= segs; i++) {
                Vector2 next = ChainPoint(i / (float)segs);
                Vector2 mid = (prev + next) / 2f;
                float rot = (next - prev).ToRotation() + MathHelper.PiOver2;
                Color lit = Color.Lerp(lightColor, GsSolarEruption.SunGold, 0.18f) * fadeIn;
                sb.Draw(tex, mid - Main.screenPosition, chainRect, lit, rot,
                    new Vector2(chainRect.Width / 2f, chainRect.Height / 2f), 1f, SpriteEffects.None, 0f);
                if (i % 3 == 0) {
                    sb.Draw(tex, mid - Main.screenPosition, spikeRect, lit, rot,
                        new Vector2(spikeRect.Width / 2f, spikeRect.Height / 2f), 1f, SpriteEffects.None, 0f);
                }
                //爆发期链节加色辉边（identity 播种错相闪变）
                if (pEff is > 0.10f and < 0.92f) {
                    float flick = 0.7f + (0.3f * MathF.Sin((Main.GlobalTimeWrappedHourly * 14f) + (DrawRand01(i) * 6.28f)));
                    Color hot = (IsCrown ? GsSolarEruption.SunWhite : GsSolarEruption.SunGold) * (0.22f * flick * fadeIn);
                    hot.A = 0;
                    sb.Draw(tex, mid - Main.screenPosition, chainRect, hot, rot,
                        new Vector2(chainRect.Width / 2f, chainRect.Height / 2f), 1.06f, SpriteEffects.None, 0f);
                }
                prev = next;
            }

            //鞭头：链端日刃，沿末段切向
            Vector2 tipTangent = tipPos - ChainPoint((segs - 1) / (float)segs);
            if (tipTangent == Vector2.Zero) {
                tipTangent = baseAngle.ToRotationVector2();
            }
            float headRot = tipTangent.ToRotation() + MathHelper.PiOver2;
            Color headLit = Color.Lerp(lightColor, GsSolarEruption.SunGold, 0.25f) * fadeIn;
            sb.Draw(tex, tipPos - Main.screenPosition, headRect, headLit, headRot,
                new Vector2(headRect.Width / 2f, headRect.Height * 0.8f), 1f, SpriteEffects.None, 0f);
        }

        /// <summary>鞭头日冕光斑：亮度随充能层数，日冕鞭再套一圈日白</summary>
        private void DrawCoronaGlow(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null) {
                return;
            }
            float charge = CrownCharge / (float)GsSolarEruption.CrownChargeMax;
            float pulse = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 9f) + (DrawRand01(99) * 6.28f)));
            Vector2 at = tipPos - Main.screenPosition;

            Color halo = GsSolarEruption.SunGold * ((0.22f + (0.30f * charge)) * pulse);
            halo.A = 0;
            sb.Draw(glow, at, null, halo, 0f, glow.Size() / 2f, 0.6f + (0.5f * charge), SpriteEffects.None, 0f);

            if (IsCrown && star != null) {
                Color cross = GsSolarEruption.SunWhite * (0.5f * pulse);
                cross.A = 0;
                sb.Draw(star, at, null, cross, Projectile.rotation * 0.5f, star.Size() / 2f, 0.16f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 日光爆：命中点原地引爆的日冕闪（原版 612 的重铸复刻，全额伤害、随机尺度、挂 Daybroken）。
    /// ai[0]=尺度（生成端随包过线）。自绘：软光核+四芒星十字+扩散残圈，加色批全 A=0
    /// </summary>
    internal class GsSolarEruptionBlastProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.SolarEruption");

        private const int Life = 24;
        private float Scale => MathHelper.Clamp(Projectile.ai[0], 0.5f, 2.2f);
        private float LifeT => 1f - (Projectile.timeLeft / (float)Life);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一次爆炸对同一目标只命中一次
            Projectile.timeLeft = Life;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //按尺度撑判定箱
                int size = (int)(90 * Scale);
                Projectile.Resize(size, size);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f, Pitch = -0.1f }, Projectile.Center);
                    for (int i = 0; i < 10; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f) * Scale;
                        Color c = Main.rand.NextBool(3) ? GsSolarEruption.SunRed : GsSolarEruption.SunGold;
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.4f, 0.7f))
                            ?.Configure(true, Main.rand.Next(14, 24));
                    }
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 4f) * Scale, 100, default, Main.rand.NextFloat(1f, 1.6f));
                        d.noGravity = true;
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GsSolarEruption.SunGold.ToVector3() * (0.8f * (1f - LifeT) * Scale));
        }

        /// <summary>伤害窗只开前 10 帧，余下是纯演出</summary>
        public override bool? CanDamage() => Projectile.timeLeft > Life - 10 ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Daybreak, 300);

        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 at = Projectile.Center - Main.screenPosition;
            float t = LifeT;
            float burst = MathF.Pow(1f - t, 1.6f); //出生最亮，快速冷却
            float expand = 0.35f + (t * 0.85f);

            //日白核（只活前 1/3）
            if (t < 0.35f) {
                Color core = GsSolarEruption.SunWhite * ((1f - (t / 0.35f)) * 0.85f);
                core.A = 0;
                Main.EntitySpriteDraw(glow, at, null, core, 0f, glow.Size() / 2f, 0.7f * Scale, SpriteEffects.None, 0);
            }
            //熔金体
            Color body = GsSolarEruption.SunGold * (0.6f * burst);
            body.A = 0;
            Main.EntitySpriteDraw(glow, at, null, body, 0f, glow.Size() / 2f, expand * 1.5f * Scale, SpriteEffects.None, 0);
            //深红外晕（冷却端）
            Color rim = GsSolarEruption.SunRed * (0.4f * burst);
            rim.A = 0;
            Main.EntitySpriteDraw(glow, at, null, rim, 0f, glow.Size() / 2f, expand * 2.2f * Scale, SpriteEffects.None, 0);
            //四芒十字（identity 播种初始角，缓旋收缩）
            Color cross = Color.Lerp(GsSolarEruption.SunWhite, GsSolarEruption.SunGold, t) * (0.7f * burst);
            cross.A = 0;
            float crossRot = (DrawRand01(3) * MathHelper.TwoPi) + (t * 0.6f);
            Main.EntitySpriteDraw(star, at, null, cross, crossRot, star.Size() / 2f,
                (0.24f - (t * 0.1f)) * Scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 日冕环：日冕鞭完成时沿鞭路驻焰 0.5 秒，每 6 帧沿路序贯引爆一记 0.55 倍日光爆。
    /// ai[0]=弓侧符号 ai[1]=触及（随生成包过线）；锚定生成瞬间的手位，路径与鞭击同参
    /// </summary>
    internal class GsSolarEruptionCrownProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.SolarEruption");

        private const int Life = 46;
        private const int BlastCount = 5;

        private Vector2 origin;
        private float baseAngle;
        private int blastsFired;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false; //本体不判伤，伤害全走序贯日光爆
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>鞭路参数点（与 held 的出程几何同参，取行程前 60% 的外扬段）</summary>
        private Vector2 PathPoint(float s) {
            float pAt = 0.10f + (s * 0.42f); //只取出程外扬段，驻焰贴着甩出的那半环
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float reach = Projectile.ai[1] * MathF.Pow(MathF.Sin(MathHelper.Pi * pAt), 0.85f);
            float lateral = Projectile.ai[0] * 96f * MathF.Sin(MathHelper.TwoPi * pAt);
            return origin + (dir * reach) + (perp * lateral);
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                origin = Projectile.Center;
                baseAngle = Projectile.velocity.ToRotation();
            }

            //序贯引爆：每 6 帧一记，从近到远
            int elapsed = Life - Projectile.timeLeft;
            if (Projectile.owner == Main.myPlayer && blastsFired < BlastCount && elapsed >= (blastsFired + 1) * 6) {
                Vector2 at = PathPoint(blastsFired / (float)(BlastCount - 1));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), at, Vector2.Zero,
                    ModContent.ProjectileType<GsSolarEruptionBlastProj>(),
                    (int)(Projectile.damage * 0.55f), 6f, Projectile.owner, 1f + (blastsFired * 0.12f));
                blastsFired++;
            }

            //驻焰粒子沿路零星喷舌
            if (!VaultUtils.isServer && Projectile.timeLeft > 8 && Main.rand.NextBool(2)) {
                Vector2 at = PathPoint(Main.rand.NextFloat());
                Dust d = Dust.NewDustPerfect(at, DustID.SolarFlare,
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.6f), 110, default, Main.rand.NextFloat(0.9f, 1.5f));
                d.noGravity = true;
            }
            Lighting.AddLight(PathPoint(0.5f), GsSolarEruption.SunRed.ToVector3() * 0.5f);
        }

        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>驻焰带：沿鞭路铺渐灭日金光珠，加色 A=0，identity 播种错相闪变</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)(Life * 0.6f), 0f, 1f);
            Vector2 texOrigin = glow.Size() / 2f;
            const int beads = 22;
            for (int i = 0; i <= beads; i++) {
                float s = i / (float)beads;
                float flick = 0.7f + (0.3f * MathF.Sin((Main.GlobalTimeWrappedHourly * 12f) + (DrawRand01(i) * 6.28f)));
                Color c = Color.Lerp(GsSolarEruption.SunGold, GsSolarEruption.SunRed, s) * (0.3f * fade * flick);
                c.A = 0;
                Main.EntitySpriteDraw(glow, PathPoint(s) - Main.screenPosition, null, c, 0f, texOrigin,
                    0.38f + (0.14f * flick), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
