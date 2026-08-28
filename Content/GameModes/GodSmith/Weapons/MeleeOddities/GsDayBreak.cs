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
    /// 【破晓·A档】材质：凝固的日出光矛，矛体是光不是铁。
    /// 签名：①矛钉入持续灼烧（Daybreak DoT + 定期再判伤），同目标至多 8 根
    /// ②日出审判：第 4 根钉上时四矛齐爆十字日曜（1.8×武器伤 AoE）③矛身流光从尾涌向尖、掷出踏步
    /// </summary>
    internal class GsDayBreak : GodSmithScheme
    {
        public override int TargetItemID => ItemID.DayBreak;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: spears stick and burn, up to 8 per target; " +
            "pinning 4 spears into one target detonates them into a cross of daylight";

        //日曜色板（与日耀链刃同名同值，本类自持避免跨文件依赖）
        internal static readonly Color SunWhite = new(255, 244, 214); //日白
        internal static readonly Color SunGold = new(255, 196, 88);   //熔金
        internal static readonly Color SunRed = new(226, 82, 40);     //日冕深红

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 弹幕总帧)，两者都吃攻速）
            if (HeldAlive<GsDayBreakHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsDayBreakHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版投掷；远端靠弹幕同步看到动作
            return false;
        }

        //底伤 ×1.0：引爆（1.8×）消耗 4 根钉矛的剩余 DoT，净增益约一成，综合落在原版 105%~115%
    }

    /// <summary>
    /// 破晓手持投掷。三相 举矛聚光-掷-收；聚光期矛尖光点从散到聚，
    /// 掷出帧沿出手向踏半步（守坐骑）+ 低音掷响
    /// </summary>
    internal class GsDayBreakHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.DayBreak");

        private const int PhaseCharge = 0;
        private const int PhaseThrow = 1;
        private const int PhaseRecover = 2;

        //阶段时长，InitStage 写入（已含攻速缩放）
        private int chargeDur = 6;
        private int throwDur = 3;
        private int recoverDur = 7;
        private int totalDur;

        private float baseAngle;
        private int facingDir = 1;
        private float armAngle;
        private float bodyLean;
        private bool bodyLeanApplied;
        private bool spearThrown;
        private int timer;

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 HandPos => Hand + (armAngle.ToRotationVector2() * 20f);
        /// <summary>手中矛尖（聚光位）</summary>
        private Vector2 SpearTip => HandPos + (armAngle.ToRotationVector2() * 30f);

        private int CurrentPhase {
            get {
                if (timer <= chargeDur) {
                    return PhaseCharge;
                }
                if (timer <= chargeDur + throwDur) {
                    return PhaseThrow;
                }
                return PhaseRecover;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false; //纯演出手持，伤害全在矛弹
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>各相时长除以攻速，攻速词条真实生效</summary>
        private void InitStage() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));
            chargeDur = D(6);
            throwDur = D(3);
            recoverDur = D(7);
            totalDur = chargeDur + throwDur + recoverDur;
        }

        public override void AI() {
            if (Item.type != ItemID.DayBreak || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            if (timer == 0) {
                InitStage();
            }
            timer++;

            int phase = CurrentPhase;
            UpdateArm(phase);
            UpdatePose(phase);
            HandlePhaseEvents(phase);
            if (!VaultUtils.isServer) {
                HandleParticles(phase);
            }

            Lighting.AddLight(SpearTip, GsDayBreak.SunGold.ToVector3() * 0.45f);

            if (timer >= totalDur) {
                Projectile.Kill();
            }
        }

        /// <summary>臂角时间线：举矛过肩聚光，掷出过冲下压，收势渐直</summary>
        private void UpdateArm(int phase) {
            float lift;
            switch (phase) {
                case PhaseCharge: {
                    float p = timer / (float)chargeDur;
                    lift = MathHelper.Lerp(0.6f, 0.4f, EaseOutQuad(p));
                    break;
                }
                case PhaseThrow: {
                    float p = (timer - chargeDur) / (float)throwDur;
                    lift = MathHelper.Lerp(0.4f, -0.16f, EaseOutQuad(Math.Min(1f, p * 1.4f)));
                    break;
                }
                default: {
                    float p = (timer - chargeDur - throwDur) / (float)recoverDur;
                    lift = MathHelper.Lerp(-0.16f, 0.04f, SmoothStep01(p));
                    break;
                }
            }
            armAngle = baseAngle - (facingDir * lift);
        }

        /// <summary>持械姿态，聚光微仰掷出前倾</summary>
        private void UpdatePose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (armAngle.ToRotationVector2() * Owner.direction).ToRotation();

            Player.CompositeArmStretchAmount stretch = phase == PhaseRecover
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, armAngle - MathHelper.PiOver2);

            Projectile.Center = HandPos;
            Projectile.rotation = armAngle;

            (float target, float rate) = phase switch {
                PhaseCharge => (-facingDir * 0.045f, 0.3f),
                PhaseThrow => (facingDir * 0.09f, 0.65f),
                _ => (0f, 0.16f),
            };
            bodyLean = MathHelper.Lerp(bodyLean, target, rate);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜上身，坐骑/冲刺旋转让位，origin 钉脚底</summary>
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

        private void HandlePhaseEvents(int phase) {
            if (spearThrown || phase != PhaseThrow) {
                return;
            }
            spearThrown = true;
            if (Projectile.owner == Main.myPlayer) {
                float speed = Item.shootSpeed;
                if (speed <= 0f) {
                    speed = 10f; //原版 shootSpeed 10 兜底
                }
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), HandPos,
                    baseAngle.ToRotationVector2() * speed,
                    ModContent.ProjectileType<GsDayBreakSpearProj>(),
                    Projectile.damage, Projectile.knockBack, Owner.whoAmI);
                //掷出踏步（守坐骑）
                if (!Owner.mount.Active) {
                    Owner.velocity.X += facingDir * 2.5f;
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.35f }, Owner.Center);
            }
        }

        /// <summary>粒子演出（已守非服务器端）：聚光期日耀尘向矛尖收拢</summary>
        private void HandleParticles(int phase) {
            if (phase != PhaseCharge) {
                return;
            }
            if (Main.rand.NextBool(2)) {
                Vector2 tip = SpearTip;
                Vector2 at = tip + Main.rand.NextVector2Circular(24f, 24f);
                Dust d = Dust.NewDustPerfect(at, DustID.SolarFlare, (tip - at) * 0.22f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        private static float EaseOutQuad(float t) => 1f - ((1f - t) * (1f - t));
        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - (2f * x));
        }

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        //==================== 绘制：手中光矛 + 聚光收拢 + 掷出金芒 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            DrawSpearInHand(sb, lightColor);
            DrawGatherGlow(sb);
            DrawThrowSmear(sb);
            return false;
        }

        /// <summary>聚光期手中光矛：原版物品贴图 + 熔金辉（矛体是光，暗处也亮）</summary>
        private void DrawSpearInHand(SpriteBatch sb, Color lightColor) {
            if (CurrentPhase != PhaseCharge) {
                return;
            }
            Main.instance.LoadItem(ItemID.DayBreak);
            Texture2D tex = TextureAssets.Item[ItemID.DayBreak].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 at = HandPos - Main.screenPosition;
            float rot = armAngle + MathHelper.PiOver4;
            float fadeIn = MathHelper.Clamp(timer / 2f, 0f, 1f);

            //光矛自发光：环境光与日白混半
            Color lit = Color.Lerp(lightColor, GsDayBreak.SunWhite, 0.45f) * fadeIn;
            sb.Draw(tex, at, null, lit, rot, origin, 1f, SpriteEffects.None, 0f);
            Color glow = GsDayBreak.SunGold * (0.4f * fadeIn);
            glow.A = 0;
            sb.Draw(tex, at, null, glow, rot, origin, 1.07f, SpriteEffects.None, 0f);
        }

        /// <summary>聚光收拢：3 粒光点从散圈向矛尖收拢（起相 identity 播种），尖端聚核渐亮</summary>
        private void DrawGatherGlow(SpriteBatch sb) {
            if (CurrentPhase != PhaseCharge) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float p = timer / (float)chargeDur;
            Vector2 tip = SpearTip - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;
            for (int k = 0; k < 3; k++) {
                float a0 = (DrawRand01(20 + k) * MathHelper.TwoPi) + (p * 2.2f);
                float r = 28f * (1f - p);
                Vector2 at = tip + (a0.ToRotationVector2() * r);
                Color c = GsDayBreak.SunGold * (0.22f + (0.4f * p));
                c.A = 0;
                sb.Draw(glow, at, null, c, 0f, origin, 0.12f + (0.08f * p), SpriteEffects.None, 0f);
            }
            //尖端聚核
            Color core = GsDayBreak.SunWhite * (0.55f * p);
            core.A = 0;
            sb.Draw(glow, tip, null, core, 0f, origin, 0.3f + (0.2f * p), SpriteEffects.None, 0f);
        }

        /// <summary>掷出帧沿出手向拉一道金芒（加色 A=0）</summary>
        private void DrawThrowSmear(SpriteBatch sb) {
            if (CurrentPhase != PhaseThrow) {
                return;
            }
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star == null) {
                return;
            }
            float p = (timer - chargeDur) / (float)throwDur;
            float a = (1f - p) * 0.55f;
            Vector2 at = Hand + (baseAngle.ToRotationVector2() * 36f) - Main.screenPosition;
            float rot = baseAngle + MathHelper.PiOver2;
            Color c = GsDayBreak.SunGold * a;
            c.A = 0;
            sb.Draw(star, at, null, c, rot, star.Size() / 2f, new Vector2(0.05f, 0.38f), SpriteEffects.None, 0f);
            Color c2 = GsDayBreak.SunWhite * (a * 0.7f);
            c2.A = 0;
            sb.Draw(star, at, null, c2, rot, star.Size() / 2f, new Vector2(0.028f, 0.24f), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 日出光矛：镜像原版 636 黏矛（MaxUpdates=2；飞行 45 更新后 X×0.995/更新、Y+=0.15/更新；
    /// 命中钉入 velocity=(target.Center−Center)×0.75 随目标移动；同目标至多 8 根杀最旧；
    /// 钉入挂 Daybreak 300、定期对宿主再判伤、钉入 5 秒自灭）。<br/>
    /// 日出审判：第 4 根钉上时四矛设 ai[2]=1 齐爆十字日曜（owner 守门）。<br/>
    /// ai[0]=0 飞行/1 钉入，ai[1]=飞行计数/宿主 whoAmI，ai[2]=引爆标记。<br/>
    /// 自绘：原版物品贴图垫底 + 流光层（错相加色副本，光从尾流向尖）+ 双层拖尾（金白芯窄+深红缘宽）
    /// </summary>
    internal class GsDayBreakSpearProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.DayBreak");

        /// <summary>钉入寿命：600 更新 = 300 游戏帧（镜像原版 60×5×MaxUpdates）</summary>
        private const int StuckLife = 600;

        private bool Stuck => Projectile.ai[0] == 1f;
        private int HostIdx => (int)Projectile.ai[1];
        private bool DetonateMark => Projectile.ai[2] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;   //原版：黏矛不耗穿透
            Projectile.MaxUpdates = 2;   //原版双更新
            Projectile.timeLeft = 3600;
            Projectile.usesLocalNPCImmunity = true;
            //设计意图每 10 游戏帧再判一次伤；localNPCImmunity 逐更新递减、MaxUpdates=2，故写 20
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            if (!Stuck) {
                //镜像原版 636 飞行：45 更新后 X×0.995、Y+=0.15（每更新）
                Projectile.ai[1]++;
                if (Projectile.ai[1] >= 45f) {
                    Projectile.ai[1] = 45f;
                    Projectile.velocity.X *= 0.995f;
                    Projectile.velocity.Y += 0.15f;
                }
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4; //物品贴图斜 45°

                if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                        -Projectile.velocity * 0.08f, 100, default, Main.rand.NextFloat(0.7f, 1.1f));
                    d.noGravity = true;
                }
            }
            else {
                Projectile.tileCollide = false;
                Projectile.ignoreWater = true;
                if (HostIdx < 0 || HostIdx >= Main.maxNPCs) {
                    Projectile.Kill();
                    return;
                }
                NPC host = Main.npc[HostIdx];
                if (!host.active || host.dontTakeDamage) {
                    Projectile.Kill();
                    return;
                }
                //钉点跟随（镜像原版：Center = 宿主中心 − velocity×2）
                Projectile.Center = host.Center - (Projectile.velocity * 2f);
                //钉入寿命计数（各端本地跑，owner 端权威击杀）
                Projectile.localAI[0]++;
                if (Projectile.owner == Main.myPlayer && Projectile.localAI[0] >= StuckLife) {
                    Projectile.Kill();
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, GsDayBreak.SunGold.ToVector3() * 0.5f);
        }

        /// <summary>钉入后只再判宿主</summary>
        public override bool? CanHitNPC(NPC target)
            => Stuck && target.whoAmI != HostIdx ? false : null;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //原版身份：钉入目标挂 Daybreak 300
            target.AddBuff(BuffID.Daybreak, 300);

            if (Stuck) {
                //钉入期再判伤：迸 1~2 粒日耀尘
                if (!VaultUtils.isServer) {
                    int n = Main.rand.Next(1, 3);
                    for (int i = 0; i < n; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                        d.noGravity = true;
                    }
                }
                return;
            }

            //钉入（镜像原版 636：记钉点、随包过线）
            Projectile.ai[0] = 1f;
            Projectile.ai[1] = target.whoAmI;
            Projectile.velocity = (target.Center - Projectile.Center) * 0.75f;
            Projectile.netUpdate = true;

            if (Projectile.owner == Main.myPlayer) {
                HandlePinBookkeeping(target);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = true;
                }
            }
        }

        /// <summary>钉矛记账（owner 守门）：第 4 根触发日出审判齐爆；防御性保留原版 8 根上限杀最旧</summary>
        private void HandlePinBookkeeping(NPC target) {
            List<Projectile> pinned = [Projectile];
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (i != Projectile.whoAmI && p.active && p.owner == Projectile.owner
                    && p.type == Projectile.type && p.ai[0] == 1f && (int)p.ai[1] == target.whoAmI) {
                    pinned.Add(p);
                }
            }

            //日出审判：本根是第 4 根，四矛设引爆标记齐灭，目标中心起十字日曜
            if (pinned.Count >= 4) {
                foreach (Projectile p in pinned) {
                    p.ai[2] = 1f;
                    p.netUpdate = true;
                    p.Kill();
                }
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsDayBreakBurstProj>(),
                    (int)(Projectile.damage * 1.8f), Projectile.knockBack, Projectile.owner);
                return;
            }

            //原版身份：同目标至多 8 根，第 9 根钉上杀最旧（正常流第 4 根即引爆，此路兜底）
            if (pinned.Count > 8) {
                Projectile oldest = null;
                foreach (Projectile p in pinned) {
                    if (p == Projectile) {
                        continue;
                    }
                    if (oldest == null || p.localAI[0] > oldest.localAI[0]) {
                        oldest = p;
                    }
                }
                oldest?.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            if (DetonateMark) {
                //引爆标记：死亡闪白
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsDayBreak.SunWhite, 0.22f)
                    ?.Configure(8, 0.9f);
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, GsDayBreak.SunWhite,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(10, 16));
                }
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.DayBreak);
            Texture2D tex = TextureAssets.Item[ItemID.DayBreak].Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //飞行双层拖尾：深红缘宽垫底 + 金白芯窄
            if (!Stuck && star != null) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 at = Projectile.oldPos[i] + (Projectile.Size / 2f) - Main.screenPosition;
                    float k = 1f - (i / (float)Projectile.oldPos.Length);
                    float rot = Projectile.oldRot[i] + MathHelper.PiOver4; //矛轴向（贴图斜 45°，长轴对齐飞行向）
                    Color rim = GsDayBreak.SunRed * (0.22f * k);
                    rim.A = 0;
                    Main.EntitySpriteDraw(star, at, null, rim, rot, star.Size() / 2f,
                        new Vector2(0.05f, 0.15f), SpriteEffects.None, 0);
                    Color core = Color.Lerp(GsDayBreak.SunWhite, GsDayBreak.SunGold, 0.4f) * (0.34f * k);
                    core.A = 0;
                    Main.EntitySpriteDraw(star, at, null, core, rot, star.Size() / 2f,
                        new Vector2(0.024f, 0.10f), SpriteEffects.None, 0);
                }
            }

            //钉入态呼吸辉光
            if (Stuck && glow != null) {
                float breath = 0.8f + (0.2f * MathF.Sin((Main.GlobalTimeWrappedHourly * 6f) + (DrawRand01(9) * 6.28f)));
                float life = 1f - MathHelper.Clamp(Projectile.localAI[0] / StuckLife, 0f, 1f);
                Color halo = GsDayBreak.SunGold * (0.4f * breath * (0.4f + (0.6f * life)));
                halo.A = 0;
                Main.EntitySpriteDraw(glow, drawPos, null, halo, 0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            }

            //本体：光矛自发光（环境光与日白混半）
            Color lit = Color.Lerp(lightColor, GsDayBreak.SunWhite, 0.45f);
            Main.EntitySpriteDraw(tex, drawPos, null, lit, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);

            //流光层：同贴图加色副本沿矛轴 3 份错相，亮度依次延迟=光从尾流向尖
            Vector2 axis = (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2();
            for (int k = 0; k < 3; k++) {
                float phase = (Main.GlobalTimeWrappedHourly * 9f) - (k * 1.9f) + (DrawRand01(k + 3) * MathHelper.TwoPi);
                float bright = 0.5f + (0.5f * MathF.Sin(phase));
                Color c = GsDayBreak.SunGold * (0.22f * bright);
                c.A = 0;
                Vector2 off = axis * (6f * (k - 1));
                Main.EntitySpriteDraw(tex, drawPos + off, null, c, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 十字日曜：日出审判的引爆体（1.8×武器伤经生成参数过线），AoE 半径约 140，
    /// 伤害窗只开前 8 帧一击，命中挂 Daybreak 300。<br/>
    /// 自绘：StarTexture 纵横两笔拉长（纵长横短）+ SoftGlow 核 + 外圈深红晕，出生最亮快速冷却
    /// </summary>
    internal class GsDayBreakBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.DayBreak");

        private const int Life = 26;
        private float LifeT => 1f - (Projectile.timeLeft / (float)Life);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 280; //半径约 140
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一次引爆对同一目标只命中一次
            Projectile.timeLeft = Life;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = -0.15f }, Projectile.Center);
                    //金火星 12 粒放射
                    for (int i = 0; i < 12; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f);
                        Color c = Main.rand.NextBool(3) ? GsDayBreak.SunRed : GsDayBreak.SunGold;
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.4f, 0.7f))
                            ?.Configure(true, Main.rand.Next(14, 24));
                    }
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 100, default, Main.rand.NextFloat(1f, 1.6f));
                        d.noGravity = true;
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GsDayBreak.SunGold.ToVector3() * (1.1f * (1f - LifeT)));
        }

        /// <summary>伤害窗只开前 8 帧，余下是纯演出</summary>
        public override bool? CanDamage() => Projectile.timeLeft > Life - 8 ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Daybreak, 300);

        /// <summary>确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
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
            float expand = 0.4f + (t * 0.8f);

            //日白核（只活前 1/3）
            if (t < 0.35f) {
                Color core = GsDayBreak.SunWhite * ((1f - (t / 0.35f)) * 0.9f);
                core.A = 0;
                Main.EntitySpriteDraw(glow, at, null, core, 0f, glow.Size() / 2f, 1.0f, SpriteEffects.None, 0);
            }
            //熔金体
            Color body = GsDayBreak.SunGold * (0.6f * burst);
            body.A = 0;
            Main.EntitySpriteDraw(glow, at, null, body, 0f, glow.Size() / 2f, expand * 2.2f, SpriteEffects.None, 0);
            //深红外晕
            Color rim = GsDayBreak.SunRed * (0.4f * burst);
            rim.A = 0;
            Main.EntitySpriteDraw(glow, at, null, rim, 0f, glow.Size() / 2f, expand * 3.2f, SpriteEffects.None, 0);

            //十字日曜：纵横两笔拉长，纵长横短，缓旋收缩
            float crossRot = (DrawRand01(3) * 0.4f) + (t * 0.25f);
            Color cross = Color.Lerp(GsDayBreak.SunWhite, GsDayBreak.SunGold, t) * (0.8f * burst);
            cross.A = 0;
            Main.EntitySpriteDraw(star, at, null, cross, crossRot, star.Size() / 2f,
                new Vector2(0.16f, 1.5f - (t * 0.5f)), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, at, null, cross * 0.85f, crossRot + MathHelper.PiOver2, star.Size() / 2f,
                new Vector2(0.13f, 0.95f - (t * 0.3f)), SpriteEffects.None, 0);
            return false;
        }
    }
}
