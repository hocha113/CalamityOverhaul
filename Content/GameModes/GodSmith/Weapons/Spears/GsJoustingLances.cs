using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【骑枪子族·冲锋势能基准版】骑枪重铸：按住端平、奔驰蓄势。<br/>
    /// 材质：锻钢枪骑长矛。签名行为：①移动速度攒冲势，满冲势一击 2.4 倍并强击退
    /// ②满冲势直道骑得越久，下一击追加伤害越高（骑士的长直道）
    /// ③满冲势命中迸冲击波与震屏
    /// </summary>
    internal class GsJoustingLance : GsSpearScheme
    {
        public override int TargetItemID => ItemID.JoustingLance;

        protected override string GsDescFallback =>
            "Reforged: hold to couch the lance, speed builds momentum, a full-tilt strike deals up to 2.4x damage;" +
            "\nthe longer you ride at full tilt, the more bonus damage the next strike carries";

        protected override int HeldProjType => ModContent.ProjectileType<GsJoustingLanceHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//冲锋乘区（0.75~2.4）才是主要收益，底伤只补零头，综合 DPS 落在原版 100%~120%
    }

    /// <summary>
    /// 【骑枪子族·暗影独行】暗影骑枪重铸：冲锋路径留焰。<br/>
    /// 材质：暗影钢骑枪缠噬影紫焰。签名行为：①冲势过半后冲锋路径驻下暗焰残影
    /// ②触碰残影的敌人受 25% 伤害并点燃暗影焰 ③满冲势命中迸暗紫冲击波
    /// </summary>
    internal class GsShadowJoustingLance : GsSpearScheme
    {
        public override int TargetItemID => ItemID.ShadowJoustingLance;

        protected override string GsDescFallback =>
            "Reforged: charging at speed leaves shadowflame embers along your path;" +
            "\nfoes touching an ember take 25% damage and catch shadowflame";

        protected override int HeldProjType => ModContent.ProjectileType<GsShadowJoustingLanceHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;//签名残影吃掉大半预算，底伤几乎不补，综合 DPS 落在原版 100%~120%
    }

    /// <summary>
    /// 【骑枪子族·圣辉冲阵】神圣骑枪重铸：满冲势天降星芒。<br/>
    /// 材质：圣金骑枪覆棱彩辉光。签名行为：①满冲势期间每约 0.7 秒向最近敌人落一枚彩虹星芒（40% 伤害）
    /// ②星芒坠落加速带彩虹拖尾 ③命中迸圣光棱彩粒子
    /// </summary>
    internal class GsHallowJoustingLance : GsSpearScheme
    {
        public override int TargetItemID => ItemID.HallowJoustingLance;

        protected override string GsDescFallback =>
            "Reforged: at full tilt, a prismatic star falls on the nearest foe every 0.7s for 40% damage;" +
            "\nfull-tilt strikes burst with holy prismatic light";

        protected override int HeldProjType => ModContent.ProjectileType<GsHallowJoustingLanceHeld>();

        //星芒驻场是本把的伤害大头，底伤不加成（包络 1.0），综合 DPS 落在原版 105%~120%
    }

    /// <summary>
    /// 骑枪共享手持骨架（不走 GsThrustHeldBase：无三相刺击，改为按住持续冲锋）。<br/>
    /// 存活 = 按住左键 + 物品匹配 + 玩家活着；松手短收势后由 owner 权威收枪，远端等击杀包
    /// （收势期不续 timeLeft，丢包也能自然超时）。<br/>
    /// 冲势 momentum 0~1 各端从同步的 Owner.velocity 各自累计，天然近似一致；
    /// 伤害乘区 lerp(0.75, 2.4, momentum) 只在 owner 端 ModifyHitNPC 生效
    /// </summary>
    internal abstract class GsJoustingLanceHeldBase : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName =>
            Language.GetText("ItemName." + ItemID.Search.GetName(TargetItemType));

        //==================== 子类必填 ====================

        /// <summary>目标物品 ID，换武器即自杀</summary>
        protected abstract int TargetItemType { get; }
        /// <summary>色板：亮缘色（速度线/残影）</summary>
        protected abstract Color EdgeColor { get; }
        /// <summary>色板：能量核心色（辉光/冲击波）</summary>
        protected abstract Color CoreColor { get; }
        /// <summary>色板：暗底色（阴影垫底/暗层）</summary>
        protected abstract Color DeepColor { get; }

        //==================== 参数 ====================

        /// <summary>手→枪尖长度（判定与贴图共用）</summary>
        protected virtual float BladeLength => 95f;
        /// <summary>枪线判定宽度</summary>
        protected virtual float CollisionWidth => 30f;
        /// <summary>枪尖贪婪圆半径</summary>
        protected virtual float TipGreedRadius => 26f;
        /// <summary>贴图对角线上枪身占比（换算绘制缩放）</summary>
        protected virtual float BladeTexFill => 0.82f;
        /// <summary>aim 每帧向鼠标角度靠拢的弧度（骑枪是重家伙，转向慢）</summary>
        protected virtual float AimTurnRate => 0.05f;
        /// <summary>冲势增长的速度阈值（|velocity.X| 超过才攒）</summary>
        protected const float MomentumThreshold = 5f;
        /// <summary>松手收势帧数</summary>
        protected const int RetractFrames = 8;

        //==================== 运行时状态 ====================

        /// <summary>冲锋势能 0~1，各端从同步的 Owner.velocity 各自累计</summary>
        protected float momentum;
        protected Vector2 aimUnit = Vector2.UnitX;
        protected float aimAngle;
        protected int facingDir = 1;
        /// <summary>当前持距（手→枪根）</summary>
        protected float holdout = 4f;
        /// <summary>生成时的基础伤害快照</summary>
        protected int BaseDamage { get; private set; }

        private int retractTimer;
        private int lastTier;
        private int flashTimer;
        private int shockCooldown;
        private float bodyLean;
        private bool bodyLeanApplied;

        /// <summary>冲势三档：1=0.33 起冲 2=0.66 拉风线 3=满档</summary>
        protected int MomentumTier => momentum >= 0.995f ? 3 : momentum >= 0.66f ? 2 : momentum >= 0.33f ? 1 : 0;
        protected bool Retracting => retractTimer > 0;
        /// <summary>收势期整体淡出系数</summary>
        protected float DrawFade => Retracting ? MathHelper.Clamp(1f - retractTimer / (float)RetractFrames, 0f, 1f) : 1f;
        protected Vector2 Hand => Owner.GetPlayerStabilityCenter();
        /// <summary>枪尖世界坐标</summary>
        protected Vector2 TipPos => Hand + aimUnit * (holdout + BladeLength);
        protected float FlashT => flashTimer / 8f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;//骑冲反复撞，同目标 18 帧一跳
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            aimUnit = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            aimAngle = aimUnit.ToRotation();
            facingDir = MathF.Abs(aimUnit.X) < 0.05f ? Owner.direction : Math.Sign(aimUnit.X);
            BaseDamage = Projectile.damage;
        }

        public override void AI() {
            if (Item.type != TargetItemType || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            //松手进收势且不回头；进攻期每帧续命，收势期不续（远端丢击杀包也能 90 帧内自然超时）
            if (retractTimer > 0 || !DownLeft) {
                retractTimer++;
                if (retractTimer >= RetractFrames && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                Projectile.timeLeft = 90;
            }

            if (flashTimer > 0) {
                flashTimer--;
            }
            if (shockCooldown > 0) {
                shockCooldown--;
            }

            UpdateAim();
            UpdateMomentum();
            UpdateHoldout();
            UpdatePose();
            HandleParticles();
            OnTickExtra(MomentumTier);

            Lighting.AddLight(TipPos, CoreColor.ToVector3() * ((0.18f + momentum * 0.42f) * DrawFade));
        }

        /// <summary>枪压平贴冲锋向，aim 缓慢跟鼠标（重家伙转向慢）</summary>
        private void UpdateAim() {
            float wanted = ToMouse.ToRotation();
            aimAngle = aimAngle.AngleTowards(wanted, AimTurnRate);
            aimUnit = aimAngle.ToRotationVector2();
            facingDir = MathF.Abs(aimUnit.X) < 0.05f ? Owner.direction : Math.Sign(aimUnit.X);
        }

        /// <summary>冲势累计：超阈值增长（增速随超出量），低速衰减；升档给可见反馈</summary>
        private void UpdateMomentum() {
            float speed = MathF.Abs(Owner.velocity.X);
            if (speed > MomentumThreshold) {
                momentum += 0.006f + (speed - MomentumThreshold) * 0.0035f;
            }
            else {
                momentum -= 0.011f;
            }
            momentum = MathHelper.Clamp(momentum, 0f, 1f);

            int tier = MomentumTier;
            if (tier > lastTier && tier >= 2) {
                flashTimer = 8;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with {
                        Volume = tier >= 3 ? 0.55f : 0.4f,
                        Pitch = tier >= 3 ? 0.35f : 0.05f
                    }, Owner.Center);
                    if (tier >= 3) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.15f }, Owner.Center);
                    }
                }
            }
            lastTier = tier;
        }

        /// <summary>持距：冲势越足枪压得越前；收势期抽回</summary>
        private void UpdateHoldout() {
            if (Retracting) {
                float rt = retractTimer / (float)RetractFrames;
                holdout = MathHelper.Lerp(holdout, -16f, rt * rt);
                return;
            }
            holdout = MathHelper.Lerp(holdout, 4f + momentum * 10f, 0.3f);
        }

        /// <summary>持枪姿态：双手臂姿 + 体态前倾（冲势越足倾得越深；坐骑/冲刺让位）</summary>
        private void UpdatePose() {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (aimUnit * Owner.direction).ToRotation();

            float armRot = aimAngle - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot - facingDir * 0.35f);

            Projectile.Center = Hand + aimUnit * (holdout + BladeLength * 0.5f);
            Projectile.rotation = aimAngle;

            float target = Retracting ? 0f : facingDir * (0.02f + momentum * 0.055f);
            bodyLean = MathHelper.Lerp(bodyLean, target, 0.2f);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜钉脚底，坐骑/冲刺旋转让位（坐骑时不加倾斜但冲势机制照常）</summary>
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

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        /// <summary>冲锋粒子：一档起枪身零星风痕，满档枪尖吐火花</summary>
        private void HandleParticles() {
            if (VaultUtils.isServer || Retracting) {
                return;
            }
            int tier = MomentumTier;
            if (tier >= 1 && Main.rand.NextFloat() < 0.10f + momentum * 0.22f) {
                Vector2 at = Hand + aimUnit * Main.rand.NextFloat(holdout + 12f, holdout + BladeLength * 0.9f);
                PRTLoader.NewParticle<PRT_Light>(at, -Owner.velocity * 0.10f,
                    Main.rand.NextBool(3) ? CoreColor : EdgeColor,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(7, 12), 0.5f, 1.5f);
            }
            if (tier >= 3 && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(TipPos + Main.rand.NextVector2Circular(5f, 5f),
                    aimUnit.RotatedByRandom(0.4) * Main.rand.NextFloat(1.5f, 3.5f),
                    CoreColor, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        /// <summary>每帧尾钩（tier=当前冲势档；残影/星芒驻场逻辑写在这，弹幕生成自守 owner）</summary>
        protected virtual void OnTickExtra(int tier) { }

        //==================== 判定 ====================

        public override bool? CanDamage() => Retracting ? false : null;

        /// <summary>贪婪判定：枪线 hand→tip 宽 30 + 尖端贪婪圆</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Retracting) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(6, 6);
            Vector2 tip = TipPos;
            if (greedyBox.Distance(tip) <= TipGreedRadius) {
                return true;
            }
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(),
                Hand, tip, CollisionWidth, ref collisionPoint);
        }

        public override void CutTiles() {
            if (Retracting) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Hand, TipPos, 28f, DelegateMethods.CutTiles);
        }

        //==================== 命中 ====================

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //冲锋乘区（owner 端 hook 生效）；命中击退方向 = 冲锋向
            modifiers.SourceDamage *= MathHelper.Lerp(0.75f, 2.4f, momentum);
            modifiers.HitDirectionOverride = facingDir;
            if (MomentumTier >= 3) {
                modifiers.Knockback *= 1.5f;//满档撞飞
            }
            ModifyHitExtra(target, ref modifiers);
        }

        /// <summary>命中伤害修饰尾钩（owner 端）</summary>
        protected virtual void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) { }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发外部命中钩子（模拟物品直击链，喂饰品与神赋；18 帧跳一跳，每跳都是真实命中）
            ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
            NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
            PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);

            if (MomentumTier >= 3) {
                FullTierImpact(target);
            }
            OnHitExtra(target, hit, damageDone);

            if (!VaultUtils.isServer) {
                SpawnHitEffects(target, hit);
            }
        }

        /// <summary>命中尾钩（挂 buff/资源结算；owner 端执行）</summary>
        protected virtual void OnHitExtra(NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>满档命中升级反馈：冲击波环 + 震屏 + 重音（12 帧一次防人堆刷屏）</summary>
        private void FullTierImpact(NPC target) {
            if (shockCooldown > 0) {
                return;
            }
            shockCooldown = 12;
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, CoreColor, 1f)
                ?.Configure(0.25f, 1.35f, 13);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = -0.4f }, target.Center);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, aimUnit, 5f, 6f, 8, 700f, FullName));
            }
        }

        /// <summary>命中反馈：钢质弹钢屑、血肉火花+血尘，规模随冲势（子类可换识别度）</summary>
        protected virtual void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero,
                steel ? CoreColor : EdgeColor, 0.15f + momentum * 0.12f)?.Configure(9, 0.75f);
            int sparks = 4 + (int)(momentum * 6f);
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = aimUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3.5f, 7f + momentum * 4f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Main.rand.NextBool() ? CoreColor : EdgeColor, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
            if (!steel) {
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                        aimUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, Main.rand.NextFloat(0.9f, 1.2f));
                    d.noGravity = Main.rand.NextBool();
                }
            }
        }

        //==================== 绘制（原版贴图垫底 + 自绘速度线/辉光/尖端光点，禁 Main.rand） ====================

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            DrawSpeedLines(sb);
            DrawLanceSet(sb, lightColor);
            DrawTipGlow(sb);
            DrawExtra(sb);
            return false;
        }

        /// <summary>最上层的武器自有层（残影核/棱彩闪点等；whoAmI 种子，无随机）</summary>
        protected virtual void DrawExtra(SpriteBatch sb) { }

        /// <summary>二档起风线：SpeedLines01 定带截条随身后拉 + Airflow 沿枪身拉丝（加色 A=0，whoAmI 种子）</summary>
        private void DrawSpeedLines(SpriteBatch sb) {
            float lineT = MathHelper.Clamp((momentum - 0.66f) / 0.34f, 0f, 1f);
            float ownerVelX = Owner.velocity.X;
            if (lineT <= 0.02f || MathF.Abs(ownerVelX) < 3f || DrawFade <= 0.05f) {
                return;
            }
            int chargeDir = ownerVelX >= 0f ? 1 : -1;
            float alpha = lineT * DrawFade * 0.5f;

            Texture2D lines = CWRAsset.SpeedLines01?.Value;
            if (lines != null) {
                int bandH = Math.Max(1, lines.Height / 5);
                for (int i = 0; i < 3; i++) {
                    int bandY = (Projectile.whoAmI * 89 + i * 197) % Math.Max(1, lines.Height - bandH);
                    Rectangle src = new(0, bandY, lines.Width, bandH);
                    float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * (6f + i * 1.7f) + Projectile.whoAmI * 1.3f) * 3f;
                    Vector2 pos = Owner.MountedCenter
                        + new Vector2(-chargeDir * (26f + i * 22f), -20f + i * 18f + bob) - Main.screenPosition;
                    Color c = EdgeColor with { A = 0 } * (alpha * (1f - i * 0.22f));
                    SpriteEffects fx = chargeDir < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    sb.Draw(lines, pos, src, c, 0f, new Vector2(src.Width * 0.5f, bandH * 0.5f),
                        new Vector2(0.30f + momentum * 0.10f, 0.55f), fx, 0f);
                }
            }

            Texture2D flow = CWRAsset.Airflow?.Value;
            if (flow != null) {
                Vector2 mid = Hand + aimUnit * (holdout + BladeLength * 0.45f) - Main.screenPosition;
                float breath = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.whoAmI * 0.7f);
                Color c = CoreColor with { A = 0 } * (alpha * 0.7f * breath);
                sb.Draw(flow, mid, null, c, aimAngle, flow.Size() / 2f,
                    new Vector2((BladeLength + 46f) / flow.Width, 0.18f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>持距残影 + 暗影垫底 + 原版本体 + 冲势辉光</summary>
        private void DrawLanceSet(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(TargetItemType);
            Texture2D tex = TextureAssets.Item[TargetItemType].Value;
            Vector2 origin = tex.Size() / 2f;
            float scale = BladeLength / MathF.Max(tex.Size().Length() * BladeTexFill, 1f);

            //贴图枪尖指向右上：沿冲锋向旋转；朝左翻转再补角
            float rot = aimAngle + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (facingDir < 0) {
                rot += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }

            Vector2 hand = Hand;
            float fade = DrawFade;

            //高速位移残影：沿身后方向两枚，冲势越足越亮
            float ghostT = MathHelper.Clamp((momentum - 0.4f) / 0.6f, 0f, 1f);
            if (ghostT > 0.02f && MathF.Abs(Owner.velocity.X) > 3f && fade > 0.05f) {
                int chargeDir = Owner.velocity.X >= 0f ? 1 : -1;
                for (int g = 1; g <= 2; g++) {
                    Color ghost = EdgeColor with { A = 0 } * ((g == 1 ? 0.26f : 0.12f) * ghostT * fade);
                    Vector2 gPos = hand + aimUnit * (holdout + BladeLength * 0.5f)
                        + new Vector2(-chargeDir * 9f * g, 0f) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, rot, origin, scale, effect, 0f);
                }
            }

            Vector2 drawPos = hand + aimUnit * (holdout + BladeLength * 0.5f) - Main.screenPosition;

            //暗影垫底
            Color shadow = DeepColor with { A = 190 } * (0.45f * fade);
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, rot, origin, scale * 1.02f, effect, 0f);

            //本体（原版贴图只当本体，识别度在自绘层）
            sb.Draw(tex, drawPos, null, lightColor * fade, rot, origin, scale, effect, 0f);

            //冲势辉光：势能升温 + 升档闪
            float glowStrength = (momentum * 0.30f + FlashT * 0.45f) * fade;
            if (glowStrength > 0.02f) {
                Color glow = CoreColor with { A = 0 } * glowStrength;
                sb.Draw(tex, drawPos, null, glow, rot, origin, scale * 1.045f, effect, 0f);
            }
        }

        /// <summary>三档枪尖辉光：软晕 + 四芒星光点（脉动吃 whoAmI 种子）</summary>
        private void DrawTipGlow(SpriteBatch sb) {
            if (MomentumTier < 3 || DrawFade <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return;
            }
            Vector2 tip = TipPos - Main.screenPosition;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.whoAmI * 1.37f);
            sb.Draw(glow, tip, null, CoreColor with { A = 0 } * (0.5f * DrawFade), 0f,
                glow.Size() / 2f, 0.5f * pulse, SpriteEffects.None, 0f);
            sb.Draw(star, tip, null, EdgeColor with { A = 0 } * (0.75f * DrawFade),
                Main.GlobalTimeWrappedHourly * 2f + Projectile.whoAmI, star.Size() / 2f, 0.34f * pulse, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 骑枪基准版手持：满档在场时间攒「直道储势」，下一击追加小额伤害后清账。<br/>
    /// 储势只在 owner 端记账与结算（ModifyHit 系 owner 端 hook），无需过线
    /// </summary>
    internal class GsJoustingLanceHeld : GsJoustingLanceHeldBase
    {
        protected override int TargetItemType => ItemID.JoustingLance;

        //锻钢骑枪色板
        internal static readonly Color SteelEdge = new(232, 232, 240);
        internal static readonly Color KnightGold = new(255, 214, 120);
        internal static readonly Color SteelDeep = new(70, 74, 92);

        protected override Color EdgeColor => SteelEdge;
        protected override Color CoreColor => KnightGold;
        protected override Color DeepColor => SteelDeep;

        /// <summary>满档在场帧计数（骑士的长直道），上限 300（5 秒攒满）</summary>
        private int fullFrames;

        protected override void OnTickExtra(int tier) {
            if (tier >= 3) {
                fullFrames = Math.Min(fullFrames + 1, 300);
            }
            else {
                fullFrames = Math.Max(0, fullFrames - 4);
            }
        }

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.FlatBonusDamage += MathF.Min(fullFrames * 0.15f, BaseDamage * 0.75f);

        /// <summary>储势一击后清账，重新骑直道再攒</summary>
        protected override void OnHitExtra(NPC target, NPC.HitInfo hit, int damageDone)
            => fullFrames = 0;

        /// <summary>储势可视化：枪身前段金辉随储势增亮（定值，无随机）</summary>
        protected override void DrawExtra(SpriteBatch sb) {
            float bank = MathHelper.Clamp(fullFrames / 300f, 0f, 1f);
            if (bank <= 0.03f || DrawFade <= 0.05f) {
                return;
            }
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return;
            }
            Vector2 at = Hand + aimUnit * (holdout + BladeLength * 0.8f) - Main.screenPosition;
            Color c = KnightGold with { A = 0 } * ((0.3f + 0.4f * bank) * DrawFade);
            sb.Draw(star, at, null, c, Main.GlobalTimeWrappedHourly * 1.4f + Projectile.whoAmI * 0.9f,
                star.Size() / 2f, 0.16f + 0.14f * bank, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 暗影骑枪手持：二档起冲锋路径每 5 帧驻一枚短命暗焰判定点（owner 端生成）
    /// </summary>
    internal class GsShadowJoustingLanceHeld : GsJoustingLanceHeldBase
    {
        protected override int TargetItemType => ItemID.ShadowJoustingLance;

        //暗影钢紫焰色板
        internal static readonly Color ShadowPale = new(216, 196, 240);
        internal static readonly Color ShadowViolet = new(140, 72, 210);
        internal static readonly Color ShadowVoid = new(36, 22, 52);

        protected override Color EdgeColor => ShadowPale;
        protected override Color CoreColor => ShadowViolet;
        protected override Color DeepColor => ShadowVoid;

        private int trailTick;

        protected override void OnTickExtra(int tier) {
            if (tier < 2 || Retracting || !Projectile.IsOwnedByLocalPlayer()
                || MathF.Abs(Owner.velocity.X) <= MomentumThreshold) {
                return;
            }
            if (++trailTick < 5) {
                return;
            }
            trailTick = 0;
            //冲锋路径驻暗焰点：25% 伤害 + 暗影焰
            Vector2 at = Hand + aimUnit * (holdout + BladeLength * 0.45f);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), at, Vector2.Zero,
                ModContent.ProjectileType<GsShadowJoustingLanceTrailProj>(),
                Math.Max(1, (int)(BaseDamage * 0.25f)), 0f, Owner.whoAmI);
        }

        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            base.SpawnHitEffects(target, hit);
            //命中补一口暗紫烟
            PRTLoader.NewParticle<PRT_Light>(Vector2.Lerp(TipPos, target.Center, 0.5f),
                -aimUnit * 1.5f, ShadowVoid, 0.5f)?.Configure(12, 0.5f, 1.3f);
        }
    }

    /// <summary>
    /// 神圣骑枪手持：满档期间每 42 帧向最近敌人落一枚彩虹星芒（owner 端生成，40% 伤害）
    /// </summary>
    internal class GsHallowJoustingLanceHeld : GsJoustingLanceHeldBase
    {
        protected override int TargetItemType => ItemID.HallowJoustingLance;

        //圣金棱彩色板
        internal static readonly Color HolyWhite = new(255, 244, 214);
        internal static readonly Color HolyGold = new(255, 208, 96);
        internal static readonly Color HolyViolet = new(112, 88, 152);

        protected override Color EdgeColor => HolyWhite;
        protected override Color CoreColor => HolyGold;
        protected override Color DeepColor => HolyViolet;

        private int starTick;

        protected override void OnTickExtra(int tier) {
            if (tier < 3 || Retracting || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (++starTick < 42) {
                return;
            }
            starTick = 0;
            NPC target = Owner.Center.FindClosestNPC(900f);
            if (target == null) {
                return;
            }
            //星芒从目标上空坠落，带一点提前量
            Vector2 lead = target.Center + target.velocity * 18f;
            Vector2 from = lead + new Vector2(Main.rand.NextFloat(-90f, 90f), -Main.rand.NextFloat(380f, 460f));
            Vector2 vel = (lead - from).SafeNormalize(Vector2.UnitY) * 13.5f;
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                ModContent.ProjectileType<GsHallowJoustingLanceStarProj>(),
                Math.Max(1, (int)(BaseDamage * 0.40f)), 2f, Owner.whoAmI);
        }

        /// <summary>命中迸圣光棱彩：色相散布的火花（AI 路径可用 Main.rand）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, HolyWhite, 0.16f + momentum * 0.12f)
                ?.Configure(9, 0.8f);
            int sparks = 5 + (int)(momentum * 6f);
            for (int i = 0; i < sparks; i++) {
                Color c = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.62f);
                Vector2 vel = aimUnit.RotatedByRandom(0.6) * Main.rand.NextFloat(3.5f, 7.5f);
                PRTLoader.NewParticle<PRT_Sparkle>(pos, vel, c, Main.rand.NextFloat(0.4f, 0.7f));
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.35f, Pitch = 0.3f }, target.Center);
        }

        /// <summary>二档起枪身两点棱彩闪烁（色相走时间，whoAmI 种子，无随机）</summary>
        protected override void DrawExtra(SpriteBatch sb) {
            if (MomentumTier < 2 || DrawFade <= 0.05f) {
                return;
            }
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                float along = holdout + BladeLength * (0.35f + i * 0.3f);
                float hue = (Main.GlobalTimeWrappedHourly * 0.3f + i * 0.5f + Projectile.whoAmI * 0.13f) % 1f;
                float tw = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 2.4f + Projectile.whoAmI);
                Color c = Main.hslToRgb(hue, 1f, 0.65f) with { A = 0 } * (0.55f * tw * DrawFade);
                sb.Draw(star, Hand + aimUnit * along - Main.screenPosition, null, c,
                    Main.GlobalTimeWrappedHourly * 3f + i, star.Size() / 2f, 0.16f * tw, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 暗影骑枪的路径暗焰点：驻场短命判定，触碰敌人 25% 伤害 + 暗影焰。<br/>
    /// 自绘三层：真 alpha 暗核（Extra_98）+ 紫加色晕 + 苍白芯；脉动吃 whoAmI 种子（绘制无随机）
    /// </summary>
    internal class GsShadowJoustingLanceTrailProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ShadowJoustingLance");

        private const int LifeFrames = 50;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//每枚焰点对同一目标只烧一口
            Projectile.timeLeft = LifeFrames;
        }

        /// <summary>淡入淡出包络（头 8 帧升、尾 12 帧落）</summary>
        private float Envelope {
            get {
                float lived = LifeFrames - Projectile.timeLeft;
                float fadeIn = MathHelper.Clamp(lived / 8f, 0f, 1f);
                float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
                return fadeIn * fadeOut;
            }
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, GsShadowJoustingLanceHeld.ShadowViolet.ToVector3() * (0.28f * Envelope));
            if (VaultUtils.isServer) {
                return;
            }
            //暗焰缓升的余絮
            if (Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)),
                    Main.rand.NextBool(3) ? GsShadowJoustingLanceHeld.ShadowPale : GsShadowJoustingLanceHeld.ShadowViolet,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(8, 13), 0.5f, 1.3f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.ShadowFlame, 180);

        /// <summary>三层自绘：真 alpha 暗核压底、紫加色晕、苍白芯（whoAmI 种子脉动）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D dark = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (dark == null || glow == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.whoAmI * 1.37f;
            float env = Envelope;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + seed);

            //暗核（真 alpha 才能压暗）
            Main.spriteBatch.Draw(dark, drawPos, null, GsShadowJoustingLanceHeld.ShadowVoid * (0.65f * env),
                seed + Main.GlobalTimeWrappedHourly * 0.8f, dark.Size() / 2f, 0.20f * pulse, SpriteEffects.None, 0f);
            //紫加色晕
            Main.spriteBatch.Draw(glow, drawPos, null,
                GsShadowJoustingLanceHeld.ShadowViolet with { A = 0 } * (0.7f * env), 0f,
                glow.Size() / 2f, 0.6f * pulse, SpriteEffects.None, 0f);
            //苍白芯
            Main.spriteBatch.Draw(glow, drawPos, null,
                GsShadowJoustingLanceHeld.ShadowPale with { A = 0 } * (0.45f * env), 0f,
                glow.Size() / 2f, 0.24f * pulse, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 神圣骑枪的彩虹星芒：目标上空坠落，加速下坠 + 微追踪，40% 伤害。<br/>
    /// 自绘：StarFlare01 星体 + StarGlow01 芯，彩虹渐变走时间与 whoAmI 种子，oldPos 拖尾（绘制无随机）
    /// </summary>
    internal class GsHallowJoustingLanceStarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.HallowJoustingLance");

        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>本星的彩虹主色相（时间流转 + whoAmI 种子，各端一致）</summary>
        private float Hue => (Main.GlobalTimeWrappedHourly * 0.35f + Projectile.whoAmI * 0.161f) % 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;
            if (Timer == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.3f, Pitch = 0.2f }, Projectile.Center);
            }
            //穿过头顶地形一小段后恢复碰撞，别隔着天板刷怪也别卡在山洞顶
            if (Timer > 25f) {
                Projectile.tileCollide = true;
            }
            //加速下坠 + 微追踪（掉头不掉速）
            if (Timer > 12f) {
                NPC target = Projectile.Center.FindClosestNPC(600f);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1f, 0.05f);
                }
            }
            if (Projectile.velocity.Length() < 19f) {
                Projectile.velocity *= 1.02f;
            }
            Projectile.rotation += 0.24f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            Lighting.AddLight(Projectile.Center, Main.hslToRgb(Hue, 1f, 0.6f).ToVector3() * 0.42f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.08f, Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.65f),
                    Main.rand.NextFloat(0.3f, 0.5f));
            }
        }

        /// <summary>落点圣光棱彩迸发</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.25f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsHallowJoustingLanceHeld.HolyWhite, 0.22f)?.Configure(10, 0.8f);
            for (int i = 0; i < 8; i++) {
                Color c = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.62f);
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>彩虹拖尾 + 星体自绘（色相沿尾巴推移，加色 A=0，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (flare == null || star == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 half = Projectile.Size / 2f;

            //拖尾：越旧越小越淡，色相逐节推移
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color c = Main.hslToRgb((Hue + i * 0.045f) % 1f, 1f, 0.62f) with { A = 0 } * (0.34f * t);
                Vector2 at = Projectile.oldPos[i] + half - Main.screenPosition;
                sb.Draw(star, at, null, c, Projectile.oldRot[i], star.Size() / 2f, 0.16f * t + 0.05f, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.whoAmI * 1.1f);
            Color body = Main.hslToRgb(Hue, 1f, 0.62f) with { A = 0 };

            //星体光斑 + 白芯
            sb.Draw(flare, drawPos, null, body * 0.85f, Projectile.rotation, flare.Size() / 2f, 0.30f * pulse, SpriteEffects.None, 0f);
            sb.Draw(star, drawPos, null, GsHallowJoustingLanceHeld.HolyWhite with { A = 0 } * 0.7f,
                -Projectile.rotation * 0.6f, star.Size() / 2f, 0.20f * pulse, SpriteEffects.None, 0f);
            return false;
        }
    }
}
