using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using InnoVault.GameContent.BaseEntity;
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
    /// ②满 3 珠为处决掷：11 把定数齐射、本轮吸血翻倍
    /// </summary>
    internal class GsVampireKnives : GodSmithScheme
    {
        public override int TargetItemID => ItemID.VampireKnives;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: hurls 4-8 knives that heal 7.5% of damage dealt; stolen blood condenses into up to 3 orbiting blood pearls, each adding a knife to the next throw; at 3 pearls the next throw is a crimson volley with doubled lifesteal";
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
    /// 吸血鬼刀手持投掷。三相 展扇-甩掷-收势；展扇期指间小刀扇（数量预览本次掷数），
    /// 甩掷帧爆发生成全部刀弹并前倾。<br/>
    /// ai[0]=本次掷刀数（凝珠已加成），ai[1]=1 为处决掷（吸血×2）
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

        /// <summary>展扇期指间小刀本体：原版物品贴图 0.62 缩放扇形排开，每把一笔 lightColor 着色（掷出后手空不画）</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0 || CurrentPhase != PhaseFan) {
                return false;
            }
            Main.instance.LoadItem(ItemID.VampireKnives);
            Texture2D tex = TextureAssets.Item[ItemID.VampireKnives].Value;
            Vector2 origin = tex.Size() / 2f;
            int n = FanCount;
            for (int k = 0; k < n; k++) {
                float ang = armAngle - 0.35f + (0.7f * k / (n - 1));
                Vector2 at = HandPos + (ang.ToRotationVector2() * 16f) - Main.screenPosition;
                Main.spriteBatch.Draw(tex, at, null, lightColor, ang + MathHelper.PiOver4, origin, 0.62f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 猩红飞刀：前 30 帧刃口顺飞行向，20 帧后渐重下坠；30 帧起镜像原版衰减
    /// （翻滚自旋、alpha+10/帧、伤害与击退 ×0.9/帧直至消失）。
    /// 命中吸血（处决 ×2 经 ai[1] 过线）并向 ModPlayer 凝珠记账。贴图借原版吸血鬼飞刀（304）默认绘制
    /// </summary>
    internal class GsVampireKnifeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireKnife;
        public override LocalizedText DisplayName => Language.GetText("ItemName.VampireKnives");

        private Player Owner => Main.player[Projectile.owner];
        private bool IsExecution => Projectile.ai[1] >= 1f;

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
                //刃口顺飞行向（原版贴图斜 45°）
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
        }
    }

    /// <summary>
    /// 环绕血珠：驻场计数标记（friendly=false），绕玩家公转。
    /// owner 端发现珠数小于自己序号或玩家未持吸血鬼刀即自杀（远端等击杀包）。<br/>
    /// ai[0]=珠序 0~2。贴图借原版吸血治疗珠（305）默认绘制
    /// </summary>
    internal class GsVampireBloodOrbProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireHeal;
        public override LocalizedText DisplayName => Language.GetText("ItemName.VampireKnives");

        private Player Owner => Main.player[Projectile.owner];
        private int PearlIndex => (int)Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = false; //纯标记不判伤
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

            //环绕：公转 + 呼吸半径（各端本地演算，无需过线）
            float t = Main.GlobalTimeWrappedHourly;
            float ang = (t * 2.2f) + (PearlIndex * MathHelper.TwoPi / 3f);
            float radius = 42f + (4f * MathF.Sin((t * 3.1f) + PearlIndex));
            Projectile.Center = Owner.MountedCenter + (ang.ToRotationVector2() * radius) - new Vector2(0f, 6f);
        }
    }
}
