using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【吸血鬼刀·A档】材质：猩红古银圣物刀，血是暗红的（禁白热）。
    /// 签名：①命中吸血凝成环绕血珠，40 血一颗、至多 3 颗，每颗让下一掷多一把刀
    /// ②满 3 珠为处决掷：11 把定数齐射、本轮吸血翻倍、出手手位血雾爆
    /// ③命中血线回流：暗红火星弧线从伤口流回持刀人
    /// </summary>
    internal class GsVampireKnives : GodSmithScheme
    {
        public override int TargetItemID => ItemID.VampireKnives;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: hurls 4-8 knives that heal 7.5% of damage dealt; " +
            "stolen blood condenses into up to 3 orbiting blood pearls, each adding a knife to the next throw; " +
            "at 3 pearls the next throw is a crimson volley with doubled lifesteal";

        //猩红古银圣物刀色板：血是暗红的
        internal static readonly Color BloodDeep = new(42, 4, 7);       //凝血暗底
        internal static readonly Color BloodMain = new(107, 11, 18);    //血珠主体
        internal static readonly Color BloodBright = new(168, 18, 28);  //鲜血亮缘
        internal static readonly Color SilverCold = new(214, 216, 224); //古银冷光

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 弹幕总帧)，两者都吃攻速）
            if (HeldAlive<GsVampireKnivesHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                GsVampireKnivesPlayer mp = player.GetModPlayer<GsVampireKnivesPlayer>();
                bool execution = mp.pearls >= GsVampireKnivesPlayer.PearlMax;
                int count;
                if (execution) {
                    count = 8 + 3; //处决掷：原版上限 8 + 3 珠，定数
                }
                else {
                    //镜像原版掷数：4 把基数，再各以 1/2、1/4、1/8、1/16 概率 +1
                    count = 4;
                    if (Main.rand.Next(2) == 0) {
                        count++;
                    }
                    if (Main.rand.Next(4) == 0) {
                        count++;
                    }
                    if (Main.rand.Next(8) == 0) {
                        count++;
                    }
                    if (Main.rand.Next(16) == 0) {
                        count++;
                    }
                    count += mp.pearls; //血珠奉献：每颗珠多一把刀
                }
                mp.pearls = 0; //出手清珠（环绕珠下一帧自杀）
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsVampireKnivesHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI, count, execution ? 1f : 0f);
            }
            //全端返回 false 压掉原版投掷；远端靠弹幕同步看到动作
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer || player.dead) {
                return;
            }
            //补珠：换武器致环绕珠自杀后重新持刀，按珠数补生（每帧至多一颗，序号=现存数）
            GsVampireKnivesPlayer mp = player.GetModPlayer<GsVampireKnivesPlayer>();
            int orbType = ModContent.ProjectileType<GsVampireBloodOrbProj>();
            int alive = player.ownedProjectileCounts[orbType];
            if (alive < mp.pearls) {
                Projectile.NewProjectile(player.GetSource_Misc("GsVampirePearl"), player.Center, Vector2.Zero,
                    orbType, 0, 0f, player.whoAmI, alive);
            }
        }

        //底伤 ×1.0：吸血凝珠加刀与处决翻倍吸血的机制收益已占满 DPS 预算（综合约原版 105%~118%）
    }

    /// <summary>
    /// 吸血鬼刀每玩家持久状态：吸血池与血珠计数。
    /// 只在 owner 端路径写入（AddBlood 来自刀弹 OnHitNPC，清珠来自 GsCanUseItem 的 myPlayer 块）
    /// </summary>
    internal class GsVampireKnivesPlayer : ModPlayer
    {
        internal const int PearlMax = 3;
        internal const int BloodPerPearl = 40;

        /// <summary>吸血累计池，满 40 凝 1 珠</summary>
        public int bloodBank;
        /// <summary>已凝血珠 0~3</summary>
        public int pearls;

        public override void UpdateDead() {
            bloodBank = 0;
            pearls = 0;
        }

        /// <summary>命中吸血入账（owner 端调用）：满 40 凝珠并生成环绕珠，满 3 珠后溢血弃置</summary>
        public void AddBlood(int amount) {
            if (amount <= 0 || pearls >= PearlMax) {
                return;
            }
            bloodBank += amount;
            while (bloodBank >= BloodPerPearl && pearls < PearlMax) {
                bloodBank -= BloodPerPearl;
                if (Player.whoAmI == Main.myPlayer) {
                    Projectile.NewProjectile(Player.GetSource_Misc("GsVampirePearl"), Player.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsVampireBloodOrbProj>(), 0, 0f, Player.whoAmI, pearls);
                }
                pearls++;
            }
            if (pearls >= PearlMax) {
                bloodBank = 0;
            }
        }
    }

    /// <summary>
    /// 吸血鬼刀手持投掷。三相 展扇-甩掷-收势；展扇期指间渐显小刀扇（数量预览本次掷数），
    /// 甩掷帧爆发生成全部刀弹并前倾。<br/>
    /// ai[0]=本次掷刀数（凝珠已加成），ai[1]=1 为处决掷（吸血×2、手位血雾爆）
    /// </summary>
    internal class GsVampireKnivesHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.VampireKnives");

        private const int PhaseFan = 0;
        private const int PhaseThrow = 1;
        private const int PhaseRecover = 2;

        //阶段时长，InitStage 写入（已含攻速缩放）
        private int fanDur = 4;
        private int throwDur = 3;
        private int recoverDur = 7;
        private int totalDur;

        private float baseAngle;
        private int facingDir = 1;
        private float armAngle;
        private float bodyLean;
        private bool bodyLeanApplied;
        private bool knivesThrown;
        private int timer;

        private int KnifeCount => Math.Clamp((int)Projectile.ai[0], 1, 16);
        private bool IsExecution => Projectile.ai[1] >= 1f;
        /// <summary>展扇预览刀数：随将掷刀数走，3~5 把封顶</summary>
        private int FanCount => Math.Clamp((KnifeCount / 2) + 1, 3, 5);

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 HandPos => Hand + (armAngle.ToRotationVector2() * 22f);

        private int CurrentPhase {
            get {
                if (timer <= fanDur) {
                    return PhaseFan;
                }
                if (timer <= fanDur + throwDur) {
                    return PhaseThrow;
                }
                return PhaseRecover;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false; //纯演出手持，伤害全在刀弹
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
            fanDur = D(4);
            throwDur = D(3);
            recoverDur = D(7);
            totalDur = fanDur + throwDur + recoverDur;
        }

        public override void AI() {
            if (Item.type != ItemID.VampireKnives || Owner.dead || !Owner.active) {
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

            Lighting.AddLight(HandPos, GsVampireKnives.BloodMain.ToVector3() * 0.3f);

            if (timer >= totalDur) {
                Projectile.Kill();
            }
        }

        /// <summary>臂角时间线：展扇举于面侧，甩掷过冲下压，收势渐直</summary>
        private void UpdateArm(int phase) {
            float lift;
            switch (phase) {
                case PhaseFan: {
                    float p = timer / (float)fanDur;
                    lift = MathHelper.Lerp(0.85f, 0.5f, EaseOutQuad(p));
                    break;
                }
                case PhaseThrow: {
                    float p = (timer - fanDur) / (float)throwDur;
                    lift = MathHelper.Lerp(0.5f, -0.15f, EaseOutQuad(Math.Min(1f, p * 1.4f)));
                    break;
                }
                default: {
                    float p = (timer - fanDur - throwDur) / (float)recoverDur;
                    lift = MathHelper.Lerp(-0.15f, 0.05f, SmoothStep01(p));
                    break;
                }
            }
            armAngle = baseAngle - (facingDir * lift);
        }

        /// <summary>持械姿态，展扇微仰甩掷前倾</summary>
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
                PhaseFan => (-facingDir * 0.035f, 0.3f),
                PhaseThrow => (facingDir * 0.08f, 0.65f),
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
            if (knivesThrown || phase != PhaseThrow) {
                return;
            }
            knivesThrown = true;
            ThrowKnives();
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item39 with { Volume = 0.9f, Pitch = IsExecution ? -0.25f : 0f }, Owner.Center);
                if (IsExecution) {
                    ExecutionMistFX();
                }
            }
        }

        /// <summary>甩掷帧爆发生成全部刀弹（owner 守门）；散布镜像原版：每把 ±35×0.05×序号后归一回满速</summary>
        private void ThrowKnives() {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            float speed = Item.shootSpeed;
            if (speed <= 0f) {
                speed = 15f; //原版 shootSpeed 15 兜底
            }
            Vector2 aim = baseAngle.ToRotationVector2();
            Vector2 baseVel = aim * speed;
            for (int i = 0; i < KnifeCount; i++) {
                float spread = 0.05f * i;
                Vector2 v = baseVel + new Vector2(Main.rand.Next(-35, 36) * spread, Main.rand.Next(-35, 36) * spread);
                v = v.SafeNormalize(aim) * speed;
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), HandPos, v,
                    ModContent.ProjectileType<GsVampireKnifeProj>(), Projectile.damage, Projectile.knockBack,
                    Owner.whoAmI, 0f, IsExecution ? 1f : 0f);
            }
        }

        /// <summary>处决掷的手位血雾爆（已守非服务器端）</summary>
        private void ExecutionMistFX() {
            Vector2 at = HandPos;
            PRTLoader.NewParticle<PRT_Light>(at, Vector2.Zero, GsVampireKnives.BloodMain, 0.2f)?.Configure(10, 0.85f);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                Color c = Main.rand.NextBool(3) ? GsVampireKnives.BloodBright : GsVampireKnives.BloodMain;
                PRTLoader.NewParticle<PRT_Spark>(at, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(at, DustID.Blood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f), 80, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>粒子演出（已守非服务器端）：处决展扇期手位血珠雾升腾</summary>
        private void HandleParticles(int phase) {
            if (phase == PhaseFan && IsExecution && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(HandPos + Main.rand.NextVector2Circular(10f, 10f), DustID.Blood,
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)), 120, default, Main.rand.NextFloat(0.8f, 1.2f));
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

        //==================== 绘制：指间刀扇 + 甩掷银芒 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            DrawFingerFan(sb, lightColor);
            DrawThrowSmear(sb);
            return false;
        }

        /// <summary>展扇期指间渐显小刀：原版物品贴图 0.62 缩放扇形排开 + 暗银加色辉</summary>
        private void DrawFingerFan(SpriteBatch sb, Color lightColor) {
            if (CurrentPhase != PhaseFan) {
                return;
            }
            Main.instance.LoadItem(ItemID.VampireKnives);
            Texture2D tex = TextureAssets.Item[ItemID.VampireKnives].Value;
            Vector2 origin = tex.Size() / 2f;
            float p = EaseOutQuad(timer / (float)fanDur);
            int n = FanCount;
            for (int k = 0; k < n; k++) {
                float ang = armAngle - 0.35f + (0.7f * k / (n - 1));
                Vector2 at = HandPos + (ang.ToRotationVector2() * 16f) - Main.screenPosition;
                float rot = ang + MathHelper.PiOver4;
                //本体渐显
                sb.Draw(tex, at, null, lightColor * p, rot, origin, 0.62f, SpriteEffects.None, 0f);
                //暗银加色辉
                Color glow = GsVampireKnives.SilverCold * (0.30f * p);
                glow.A = 0;
                sb.Draw(tex, at, null, glow, rot, origin, 0.66f, SpriteEffects.None, 0f);
                //处决掷刃口渗血
                if (IsExecution) {
                    Color blood = GsVampireKnives.BloodBright * (0.28f * p);
                    blood.A = 0;
                    sb.Draw(tex, at, null, blood, rot, origin, 0.70f, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>甩掷帧沿出手向拉一道银芒（处决为血芒），加色 A=0</summary>
        private void DrawThrowSmear(SpriteBatch sb) {
            if (CurrentPhase != PhaseThrow) {
                return;
            }
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star == null) {
                return;
            }
            float p = (timer - fanDur) / (float)throwDur;
            float a = (1f - p) * 0.55f;
            Vector2 at = Hand + (baseAngle.ToRotationVector2() * 32f) - Main.screenPosition;
            float rot = baseAngle + MathHelper.PiOver2;
            Color c = (IsExecution ? GsVampireKnives.BloodBright : GsVampireKnives.SilverCold) * a;
            c.A = 0;
            sb.Draw(star, at, null, c, rot, star.Size() / 2f, new Vector2(0.05f, 0.34f), SpriteEffects.None, 0f);
            Color c2 = GsVampireKnives.BloodMain * (a * 0.8f);
            c2.A = 0;
            sb.Draw(star, at, null, c2, rot, star.Size() / 2f, new Vector2(0.03f, 0.22f), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 猩红飞刀：前 30 帧刃口顺飞行向，20 帧后渐重下坠；30 帧起镜像原版衰减
    /// （翻滚自旋、alpha+10/帧、伤害与击退 ×0.9/帧直至消失）。
    /// 命中吸血（处决 ×2 经 ai[1] 过线）并向 ModPlayer 凝珠记账。<br/>
    /// 自绘：原版物品贴图垫底 + 暗银辉光层 + 短血丝拖尾（oldPos 渐淡暗红加色）
    /// </summary>
    internal class GsVampireKnifeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.VampireKnives");

        private Player Owner => Main.player[Projectile.owner];
        private bool IsExecution => Projectile.ai[1] >= 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1; //原版单穿
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.ai[0]++;
            int dir = Projectile.velocity.X >= 0f ? 1 : -1;

            if (Projectile.ai[0] < 30f) {
                //刃口顺飞行向（物品贴图斜 45°）
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }
            else {
                //镜像原版：30 帧起翻滚 + alpha+10/帧 + 伤害击退 ×0.9/帧
                Projectile.rotation += (Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y)) * 0.03f * dir;
                Projectile.alpha += 10;
                Projectile.damage = (int)(Projectile.damage * 0.9);
                Projectile.knockBack *= 0.9f;
                if (Projectile.alpha >= 255) {
                    Projectile.Kill();
                    return;
                }
            }

            //20 帧后渐重下坠
            if (Projectile.ai[0] > 20f) {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.1f, 16f);
            }

            Lighting.AddLight(Projectile.Center, GsVampireKnives.BloodMain.ToVector3() * 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                //镜像原版 vampireHeal：7.5% 吸血（处决 ×2）、月噬封锁、lifeSteal 池扣减、305 治疗珠
                float heal = damageDone * 0.075f * (IsExecution ? 2f : 1f);
                if ((int)heal > 0 && !Owner.moonLeech && Main.player[Main.myPlayer].lifeSteal > 0f) {
                    Main.player[Main.myPlayer].lifeSteal -= heal;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                        ProjectileID.VampireHeal, 0, 0f, Projectile.owner, Projectile.owner, (int)heal);
                    //凝珠记账
                    Owner.GetModPlayer<GsVampireKnivesPlayer>().AddBlood((int)heal);
                }
            }
            if (!VaultUtils.isServer) {
                BloodFlowFX(target.Center);
            }
        }

        /// <summary>血线回流：命中点→玩家的暗红火星弧线序列 + 血滴迸溅（已守非服务器端）</summary>
        private void BloodFlowFX(Vector2 from) {
            Vector2 to = Owner.Center;
            Vector2 chord = to - from;
            Vector2 mid = from + (chord * 0.5f)
                + (chord.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-46f, 46f));
            int n = Main.rand.Next(6, 9);
            for (int i = 0; i < n; i++) {
                float t = i / (float)(n - 1);
                Vector2 p = Bezier(from, mid, to, t);
                Vector2 tangent = (Bezier(from, mid, to, Math.Min(1f, t + 0.08f)) - p).SafeNormalize(Vector2.UnitX);
                Color c = Color.Lerp(GsVampireKnives.BloodBright, GsVampireKnives.BloodMain, t);
                PRTLoader.NewParticle<PRT_Spark>(p, tangent * (5.5f - (3f * t)), c, 0.42f)
                    ?.Configure(false, 14);
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(from, DustID.Blood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 60, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
            => Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, c, t), t);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 80, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.1f,
                GsVampireKnives.SilverCold, 0.3f)?.Configure(true, 10);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.VampireKnives);
            Texture2D tex = TextureAssets.Item[ItemID.VampireKnives].Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = (255 - Projectile.alpha) / 255f;

            //短血丝拖尾：oldPos 渐淡暗红窄条（禁白热）
            if (star != null) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 at = Projectile.oldPos[i] + (Projectile.Size / 2f) - Main.screenPosition;
                    float k = 1f - (i / (float)Projectile.oldPos.Length);
                    Color c = Color.Lerp(GsVampireKnives.BloodDeep, GsVampireKnives.BloodMain, k) * (0.34f * k * fade);
                    c.A = 0;
                    Main.EntitySpriteDraw(star, at, null, c, Projectile.oldRot[i] + MathHelper.PiOver4,
                        star.Size() / 2f, new Vector2(0.028f, 0.085f), SpriteEffects.None, 0);
                }
            }

            //暗银辉光层
            Color silver = GsVampireKnives.SilverCold * (0.22f * fade);
            silver.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, silver, Projectile.rotation, origin, 1.06f, SpriteEffects.None, 0);
            //处决刀渗血辉
            if (IsExecution) {
                Color blood = GsVampireKnives.BloodBright * (0.26f * fade);
                blood.A = 0;
                Main.EntitySpriteDraw(tex, drawPos, null, blood, Projectile.rotation, origin, 1.10f, SpriteEffects.None, 0);
            }
            //本体
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor * fade, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 环绕血珠：驻场纯演出（friendly=false），绕玩家公转。
    /// owner 端发现珠数小于自己序号或玩家未持吸血鬼刀即自杀（远端等击杀包）。<br/>
    /// ai[0]=珠序 0~2。自绘：SoftGlow 暗红核双层 + StarTexture 窄高光条（禁圆形大高光）
    /// </summary>
    internal class GsVampireBloodOrbProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.VampireKnives");

        private Player Owner => Main.player[Projectile.owner];
        private int PearlIndex => (int)Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = false; //纯演出
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            //owner 端权威判死：珠数掉到序号以下（出手清珠）或未持本物品
            if (Projectile.owner == Main.myPlayer) {
                GsVampireKnivesPlayer mp = Owner.GetModPlayer<GsVampireKnivesPlayer>();
                if (mp.pearls <= PearlIndex || Owner.HeldItem.type != ItemID.VampireKnives) {
                    Projectile.Kill();
                    return;
                }
            }
            Projectile.timeLeft = 120; //常驻，由状态检查决定生死

            //环绕：公转 + 呼吸半径（各端本地演算，纯演出无需过线）
            float t = Main.GlobalTimeWrappedHourly;
            float ang = (t * 2.2f) + (PearlIndex * MathHelper.TwoPi / 3f);
            float radius = 42f + (4f * MathF.Sin((t * 3.1f) + PearlIndex));
            Projectile.Center = Owner.MountedCenter + (ang.ToRotationVector2() * radius) - new Vector2(0f, 6f);

            Lighting.AddLight(Projectile.Center, GsVampireKnives.BloodMain.ToVector3() * 0.25f);

            //偶发滴露
            if (!VaultUtils.isServer && Main.rand.NextBool(30)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    DustID.Blood, new Vector2(0f, 0.6f), 120, default, 0.8f);
                d.noGravity = false;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsVampireKnives.BloodMain, 0.12f)
                ?.Configure(8, 0.7f);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 80, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
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
            float pulse = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 7f) + (DrawRand01(1) * 6.28f)));

            //暗红核双层
            Color outer = GsVampireKnives.BloodDeep * (0.6f * pulse);
            outer.A = 0;
            Main.EntitySpriteDraw(glow, at, null, outer, 0f, glow.Size() / 2f, 0.46f, SpriteEffects.None, 0);
            Color core = GsVampireKnives.BloodMain * (0.9f * pulse);
            core.A = 0;
            Main.EntitySpriteDraw(glow, at, null, core, 0f, glow.Size() / 2f, 0.26f, SpriteEffects.None, 0);

            //古银窄高光条（缓旋，禁圆形大高光）
            float rot = (DrawRand01(2) * MathHelper.TwoPi) + (Main.GlobalTimeWrappedHourly * 0.8f);
            Color spec = GsVampireKnives.SilverCold * (0.5f * pulse);
            spec.A = 0;
            Main.EntitySpriteDraw(star, at, null, spec, rot, star.Size() / 2f, new Vector2(0.016f, 0.055f), SpriteEffects.None, 0);
            //血亮细十字点
            Color pin = GsVampireKnives.BloodBright * (0.55f * pulse);
            pin.A = 0;
            Main.EntitySpriteDraw(star, at, null, pin, rot + MathHelper.PiOver2, star.Size() / 2f, new Vector2(0.012f, 0.032f), SpriteEffects.None, 0);
            return false;
        }
    }
}
