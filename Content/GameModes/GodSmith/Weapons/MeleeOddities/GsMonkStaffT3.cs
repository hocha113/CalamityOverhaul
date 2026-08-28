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
    /// 【天龙之怒】材质：衔着雷龙珠的天界法杖。签名：①左键快速旋抡（30 帧 1.5 圈加速度曲线，
    /// 保真原版半程换向/松键提前收）命中攒雷息 ②右键保真原版掷雷龙球（Player.cs 对 3858 的
    /// 硬编码 altFunctionUse，GsAltFunctionUse 返 null 保留；初速 ×1.5 伤害 ×0.5 击退 +4）
    /// ③雷息满 5 层的下一记掷球天龙化——球体 ×1.5 且沿途降下至多 3 道落雷
    /// </summary>
    internal class GsMonkStaffT3 : GodSmithScheme
    {
        public override int TargetItemID => ItemID.MonkStaffT3;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: the twirl builds Dragon Breath on hit; at 5 stacks the next thrown orb " +
            "ascends as a sky dragon, half again as large, calling down up to three lightning bolts";

        //雷龙色板
        internal static readonly Color ThunderWhite = new(240, 250, 255); //雷芯白
        internal static readonly Color ThunderBlue = new(120, 190, 255);  //青雷蓝
        internal static readonly Color DragonJade = new(90, 220, 190);    //龙鳞翠
        internal static readonly Color StormDeep = new(30, 44, 70);       //风暴深空

        internal const int BreathMax = 5;

        /// <summary>雷息层数；方案单例跨玩家共享，只在 myPlayer 守门路径消费
        /// （旋抡 OnHitNPC 只跑攻击方端写入，出手读取在 GsCanUseItem 的 myPlayer 分支）</summary>
        private int dragonBreath;

        /// <summary>held 命中回报雷息（只在 owner 端被调），上限 5</summary>
        internal void AddBreath() {
            if (dragonBreath < BreathMax) {
                dragonBreath++;
            }
        }

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 相位总帧)，两者都吃攻速）
            if (HeldAlive<GsMonkStaffT3Held>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                //右键分流：原版 Player.cs 对 3858 硬编码放行 altFunctionUse，
                //GsAltFunctionUse 保持 null 不动即天然可用，这里只读结果分相
                bool throwOrb = player.altFunctionUse == 2;
                int layers = dragonBreath;
                if (throwOrb && layers >= BreathMax) {
                    dragonBreath = 0; //天龙化即清层
                }
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsMonkStaffT3Held>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI,
                    throwOrb ? 1f : 0f, layers);
            }
            //全端返回 false 压掉原版行为；远端靠弹幕同步看到动作
            return false;
        }

        //底伤不加成（×1.0）：右键 0.5 倍与落雷 0.6 倍均按原版/包络算好，
        //雷息只改掷球形态不改直伤，综合 DPS 落在原版 100%~118%
    }

    /// <summary>
    /// 天龙之怒共用自绘小件：青白电弧短折线（identity+帧播种，禁 shader 禁 Main.rand）。
    /// 非内容类，不进 tML 注册表
    /// </summary>
    internal static class GsMonkStaffT3Arcs
    {
        internal static float Rand01(int identity, int salt) {
            uint h = (uint)((identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>一段电弧笔画：StarTexture 窄笔沿线段拉伸（加色 A=0）</summary>
        internal static void DrawStroke(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thick) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star == null) {
                return;
            }
            Vector2 mid = ((a + b) / 2f) - Main.screenPosition;
            float len = Vector2.Distance(a, b);
            Color c = color;
            c.A = 0;
            sb.Draw(star, mid, null, c, (b - a).ToRotation() + MathHelper.PiOver2,
                star.Size() / 2f, new Vector2(thick, len / star.Height * 1.15f), SpriteEffects.None, 0f);
        }

        /// <summary>绕心电弧簇：count 条 2~3 段随帧换形的短折线，环绕 center 半径 radius</summary>
        internal static void DrawArcCluster(SpriteBatch sb, int identity, Vector2 center,
            float radius, int count, float alpha) {
            if (count <= 0) {
                return;
            }
            int frameSeed = (int)(Main.GlobalTimeWrappedHourly * 16f); //约每 4 帧换形
            for (int i = 0; i < count; i++) {
                int baseSalt = (i * 131) + (frameSeed * 17);
                float a0 = Rand01(identity, baseSalt) * MathHelper.TwoPi;
                Vector2 p = center + (a0.ToRotationVector2()
                    * (radius * (0.55f + (0.45f * Rand01(identity, baseSalt + 1)))));
                int segs = 2 + (Rand01(identity, baseSalt + 2) > 0.6f ? 1 : 0);
                for (int s = 0; s < segs; s++) {
                    float segAng = a0 + MathHelper.PiOver2
                        + ((Rand01(identity, baseSalt + 3 + (s * 5)) - 0.5f) * 2.2f);
                    Vector2 next = p + (segAng.ToRotationVector2()
                        * (6f + (Rand01(identity, baseSalt + 4 + (s * 5)) * 9f)));
                    Color c = Color.Lerp(GsMonkStaffT3.ThunderBlue, GsMonkStaffT3.ThunderWhite,
                        Rand01(identity, baseSalt + s)) * alpha;
                    DrawStroke(sb, p, next, c, 0.022f);
                    p = next;
                }
            }
        }
    }

    /// <summary>
    /// 天龙之怒手持，双相合一。<br/>
    /// ai[0]=0 旋抡相：镜像 T1 自管 spin 骨架，30 帧转 1.5 圈（÷攻速），加速度曲线同款，
    /// 杆线 中心±60px 复击 6 帧，无砸地，保真半程换向与松键提前收（reuseDelay=2）；<br/>
    /// ai[0]=1 掷球臂姿相：12 帧（÷攻速），第 4 帧 owner 掷出雷龙球。<br/>
    /// ai[1]=出手时雷息层数：杖头龙珠辉光亮度与电弧数随层
    /// </summary>
    internal class GsMonkStaffT3Held : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT3");

        /// <summary>旋抡基准总帧（除以攻速）</summary>
        private const int SpinBaseDur = 30;
        /// <summary>旋抡总转角：1.5 圈</summary>
        private const float SpinTotalAngle = MathHelper.TwoPi * 1.5f;
        /// <summary>杆线半长（px），原版 T3 切割半径 60</summary>
        private const float PoleHalf = 60f;
        /// <summary>掷球臂姿基准总帧</summary>
        private const int ThrowBaseDur = 12;
        /// <summary>掷球基准出手帧</summary>
        private const int ThrowBaseFrame = 4;

        private int spinDur = SpinBaseDur;
        private int halfFrame;
        private int throwDur = ThrowBaseDur;
        private int throwFrame = ThrowBaseFrame;

        private int timer;
        private int dir = 1;
        private float rot;
        private float prevRot;
        private float speedFrac = 0.5f;
        private float baseAngle;
        /// <summary>掷球相当前臂角</summary>
        private float armAngle;
        private bool orbThrown;
        private float bodyLean;
        private bool bodyLeanApplied;
        private readonly HashSet<int> hitNPCs = [];

        private bool IsThrow => Projectile.ai[0] >= 1f;
        /// <summary>出手时雷息层数（辉光/电弧随层）</summary>
        private int BreathLayers => Math.Clamp((int)Projectile.ai[1], 0, GsMonkStaffT3.BreathMax);
        /// <summary>雷息满层的掷球即天龙化</summary>
        private bool DragonThrow => IsThrow && BreathLayers >= GsMonkStaffT3.BreathMax;

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 RotVec => rot.ToRotationVector2();
        /// <summary>杖头龙珠端（+rot 方向端）</summary>
        private Vector2 HeadPos => IsThrow
            ? Hand + (armAngle.ToRotationVector2() * 52f)
            : Projectile.Center + (RotVec * PoleHalf);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6; //旋抡复击节奏（原版 T3 更快）
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 120;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private static float MirrorAngle(float angle, int direction)
            => direction == 1 ? angle : MathHelper.Pi - angle;

        private void InitPhase() {
            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            baseAngle = Projectile.velocity.ToRotation();
            if (IsThrow) {
                float cos = MathF.Cos(baseAngle);
                dir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
                throwDur = Math.Max(6, (int)MathF.Round(ThrowBaseDur / speed));
                throwFrame = Math.Clamp((int)MathF.Round(ThrowBaseFrame / speed), 1, throwDur - 2);
                armAngle = baseAngle - (dir * 0.7f);
                return;
            }
            dir = MathF.Abs(Projectile.velocity.X) < 0.001f
                ? Owner.direction : Math.Sign(Projectile.velocity.X);
            //方向寄存在 velocity（±1,0），换向 netUpdate 过线，远端在 AI 里侦测翻杆（同 T1 骨架）
            Projectile.velocity = new Vector2(dir, 0f);
            spinDur = Math.Max(14, (int)MathF.Round(SpinBaseDur / speed));
            halfFrame = spinDur / 2;
            //起角取上后方，1.5 圈后杆头收在身前下方
            rot = MirrorAngle(-2.2f, dir);
            prevRot = rot;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_SkyDragonsFurySwing with { Volume = 0.9f }, Owner.Center);
            }
        }

        public override void AI() {
            if (Item.type != ItemID.MonkStaffT3 || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            if (timer == 0 && Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                InitPhase();
            }

            if (IsThrow) {
                ThrowAI();
            }
            else {
                SpinAI();
            }
        }

        //==================== 旋抡相 ====================

        private void SpinAI() {
            //远端换向侦测（owner 半程翻向后 velocity 随 netUpdate 过线）
            int dirNow = Projectile.velocity.X >= 0f ? 1 : -1;
            if (dirNow != dir && timer > 0) {
                dir = dirNow;
                rot -= MathHelper.Pi;
            }

            timer++;
            prevRot = rot;
            //加速度曲线同 T1：线性权重 0.7→1.4 中点采样，总转角严格 1.5 圈
            float pMid = MathHelper.Clamp((timer - 0.5f) / spinDur, 0f, 1f);
            float w = MathHelper.Lerp(0.7f, 1.4f, pMid);
            speedFrac = w / 1.4f;
            rot += SpinTotalAngle / spinDur * (w / 1.05f) * dir;

            //半程帧：松键提前收（原版 T3 reuseDelay=2）；仍按住可随鼠标换向
            if (timer == halfFrame) {
                if (!Owner.controlUseItem) {
                    EndSpin();
                    return;
                }
                if (Projectile.owner == Main.myPlayer) {
                    int side = Main.MouseWorld.X > Owner.Center.X ? 1 : -1;
                    if (side != dir) {
                        dir = side;
                        Owner.ChangeDir(side);
                        Projectile.velocity = new Vector2(side, 0f);
                        rot -= MathHelper.Pi;
                        Projectile.netUpdate = true;
                    }
                }
            }
            if (timer >= spinDur) {
                EndSpin();
                return;
            }

            UpdateSpinPose();
            if (!VaultUtils.isServer) {
                //杆尖电尘（原版 dust226 语言）
                if (Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(HeadPos, DustID.Electric,
                        (rot + (dir * MathHelper.PiOver2)).ToRotationVector2() * Main.rand.NextFloat(0.5f, 1.6f),
                        100, default, 0.5f);
                    d.noGravity = true;
                    d.noLight = true;
                }
            }
            Lighting.AddLight(HeadPos, GsMonkStaffT3.ThunderBlue.ToVector3()
                * (0.25f + (0.06f * BreathLayers)));
        }

        /// <summary>自然收尾与提前收共用：owner 补原版 reuseDelay=2</summary>
        private void EndSpin() {
            if (Owner.whoAmI == Main.myPlayer) {
                Owner.reuseDelay = 2;
            }
            Projectile.Kill();
        }

        private void UpdateSpinPose() {
            Owner.ChangeDir(dir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (RotVec * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot - MathHelper.PiOver2);
            float p = timer / (float)spinDur;
            Projectile.Center = Hand + (RotVec * (p * 8f));
            Projectile.rotation = rot;

            float target = timer >= spinDur - 2 ? 0f : dir * 0.06f * speedFrac;
            bodyLean = MathHelper.Lerp(bodyLean, target, 0.32f);
            ApplyBodyLean();
        }

        //==================== 掷球相 ====================

        private void ThrowAI() {
            timer++;
            //臂姿时间线：抬臂过肩蓄 → 甩臂过顶掷出（过冲）→ 收势回正
            float swing;
            if (timer <= throwFrame) {
                float q = timer / (float)throwFrame;
                swing = MathHelper.Lerp(0.7f, 2.1f, SmoothStep01(q));
            }
            else if (timer <= throwFrame + 2) {
                float q = (timer - throwFrame) / 2f;
                swing = MathHelper.Lerp(2.1f, -0.35f, MathF.Pow(q, 0.7f));
            }
            else {
                float q = MathHelper.Clamp((timer - throwFrame - 2f) / MathF.Max(1f, throwDur - throwFrame - 2f), 0f, 1f);
                swing = MathHelper.Lerp(-0.35f, 0.12f, SmoothStep01(q));
            }
            armAngle = baseAngle - (dir * swing);

            //出手帧：owner 掷雷龙球（保真原版 初速×1.5 伤害×0.5 击退+4），高音调抡棍音
            if (timer == throwFrame && !orbThrown) {
                orbThrown = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.85f, Pitch = 0.4f }, Owner.Center);
                }
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Item),
                        Hand + (baseAngle.ToRotationVector2() * 18f),
                        baseAngle.ToRotationVector2() * 36f, //24 shootSpeed × 1.5
                        ModContent.ProjectileType<GsMonkStaffT3OrbProj>(),
                        Math.Max(1, (int)(Projectile.damage * 0.5f)), Projectile.knockBack + 4f,
                        Owner.whoAmI, DragonThrow ? 1f : 0f);
                }
            }

            Owner.ChangeDir(dir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (armAngle.ToRotationVector2() * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle - MathHelper.PiOver2);
            Projectile.Center = Hand;
            Projectile.rotation = armAngle;

            float target = timer <= throwFrame ? -dir * 0.06f : dir * 0.12f;
            if (timer >= throwDur - 2) {
                target = 0f;
            }
            bodyLean = MathHelper.Lerp(bodyLean, target, timer <= throwFrame ? 0.3f : 0.7f);
            ApplyBodyLean();

            if (timer >= throwDur) {
                Projectile.Kill();
            }
        }

        //==================== 共用 ====================

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

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        /// <summary>掷球相不判伤，伤害全在旋抡与雷龙球</summary>
        public override bool? CanDamage() => IsThrow ? false : null;

        /// <summary>贪婪判定：本帧扫过角度区间逐段采样杆线（中心±60px）；翻杆瞬间只判当前姿态</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (IsThrow) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(6, 6);
            Vector2 center = Projectile.Center;
            float delta = MathHelper.WrapAngle(rot - prevRot);
            int steps = MathF.Abs(delta) > 1.5f ? 0 : Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * PoleHalf / 16f), 1, 8);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = steps == 0 ? rot : MathHelper.Lerp(prevRot, rot, i / (float)steps);
                Vector2 half = ang.ToRotationVector2() * PoleHalf;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(),
                    center - half, center + half, 36f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (IsThrow) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 half = RotVec * PoleHalf;
            Utils.PlotTileLine(Projectile.Center - half, Projectile.Center + half, 40f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = dir;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次旋抡对同一目标只转发一次外部命中钩子（喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }
            //雷息记账：每次命中回报一层（参考 GsSolarEruption.AddCrownCharge 模式，owner 守门）
            if (Projectile.owner == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.MonkStaffT3, out GodSmithScheme scheme)
                && scheme is GsMonkStaffT3 t3) {
                t3.AddBreath();
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f);
                    Color c = Main.rand.NextBool(3) ? GsMonkStaffT3.ThunderWhite : GsMonkStaffT3.ThunderBlue;
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(true, Main.rand.Next(10, 16));
                }
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Electric,
                    Main.rand.NextVector2Unit() * 2f, 100, default, 0.6f);
                d.noGravity = true;
            }
        }

        private float DrawRand01(int salt) => GsMonkStaffT3Arcs.Rand01(Projectile.identity, salt);

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - (2f * x));
        }

        //==================== 绘制：涂抹 + 残影 + 杆体 + 龙珠辉光随雷息 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            if (!IsThrow) {
                DrawSpinSmear(sb);
            }
            DrawStaff(sb, lightColor);
            DrawDragonPearl(sb);
            return false;
        }

        /// <summary>旋抡涂抹：青雷弧形涂抹随杆角走，亮度∝角速度（加色 A=0）</summary>
        private void DrawSpinSmear(SpriteBatch sb) {
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = 0.08f + (0.28f * speedFrac * speedFrac);
            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 scale = new Vector2(0.40f, 0.30f) * (PoleHalf / 40f);
            Color outer = GsMonkStaffT3.ThunderBlue * alpha;
            outer.A = 0;
            sb.Draw(wave, at, null, outer, rot - (dir * 0.8f), wave.Size() / 2f, scale, SpriteEffects.None, 0f);
            Color inner = GsMonkStaffT3.ThunderWhite * (alpha * 0.55f);
            inner.A = 0;
            sb.Draw(wave, at, null, inner, rot - (dir * 0.45f), wave.Size() / 2f, scale * 0.8f, SpriteEffects.None, 0f);
        }

        /// <summary>杆体：原版物品贴图 origin 按握把端；旋抡期补一道速度残影</summary>
        private void DrawStaff(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(ItemID.MonkStaffT3);
            Texture2D tex = TextureAssets.Item[ItemID.MonkStaffT3].Value;
            Vector2 origin = new(8f, tex.Height - 8f);
            float diag = new Vector2(tex.Width, tex.Height).Length();
            float poleAngle = IsThrow ? armAngle : rot;
            float visLen = IsThrow ? 96f : (PoleHalf * 2f) + 16f;
            float scale = visLen / MathF.Max(diag - 16f, 1f);
            Vector2 gripAnchor = IsThrow ? Hand - (armAngle.ToRotationVector2() * 10f)
                : Projectile.Center - (RotVec * PoleHalf);

            //速度残影（旋抡期，加色 A=0）
            if (!IsThrow && speedFrac > 0.6f) {
                float ghostAng = poleAngle - (dir * 0.26f);
                Vector2 gPos = Projectile.Center - (ghostAng.ToRotationVector2() * PoleHalf) - Main.screenPosition;
                Color ghost = GsMonkStaffT3.ThunderBlue * (0.30f * speedFrac);
                ghost.A = 0;
                sb.Draw(tex, gPos, null, ghost, ghostAng + MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
            }

            Vector2 gripPos = gripAnchor - Main.screenPosition;

            //风暴深影垫底
            Color shadow = new Color(GsMonkStaffT3.StormDeep.R, GsMonkStaffT3.StormDeep.G, GsMonkStaffT3.StormDeep.B, 190) * 0.45f;
            sb.Draw(tex, gripPos + new Vector2(dir, 2f), null, shadow, poleAngle + MathHelper.PiOver4, origin, scale * 1.02f, SpriteEffects.None, 0f);

            sb.Draw(tex, gripPos, null, lightColor, poleAngle + MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>杖头龙珠：辉光亮度/电弧数随雷息层数；掷球相出手后珠随球离手不再画</summary>
        private void DrawDragonPearl(SpriteBatch sb) {
            if (IsThrow && orbThrown) {
                return;
            }
            Texture2D soft = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (soft == null) {
                return;
            }
            int layers = BreathLayers;
            Vector2 at = HeadPos - Main.screenPosition;
            float pulse = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 10f) + (DrawRand01(9) * 6.28f)));

            Color halo = GsMonkStaffT3.ThunderBlue * ((0.16f + (0.08f * layers)) * pulse);
            halo.A = 0;
            sb.Draw(soft, at, null, halo, 0f, soft.Size() / 2f, 0.30f + (0.06f * layers), SpriteEffects.None, 0f);

            if (layers > 0 && star != null) {
                Color cross = GsMonkStaffT3.ThunderWhite * ((0.12f + (0.06f * layers)) * pulse);
                cross.A = 0;
                sb.Draw(star, at, null, cross, Main.GlobalTimeWrappedHourly * 1.4f, star.Size() / 2f,
                    0.05f + (0.012f * layers), SpriteEffects.None, 0f);
                //电弧数随层
                GsMonkStaffT3Arcs.DrawArcCluster(sb, Projectile.identity, HeadPos, 13f, layers, 0.45f * pulse);
            }
        }
    }

    /// <summary>
    /// 雷龙球：原版弹幕 708 贴图垫底+青白电弧缠绕+拖尾。初速 ×1.5 掷出、每帧 ×0.985 缓减速、
    /// 穿透 3、idStatic 复击 10、寿命 90，命中电爆。<br/>
    /// 天龙下凡态（ai[0]=1 随生成包过线）：scale×1.5，飞行中每 24 帧 owner 于当前位置降落雷，共至多 3 道
    /// </summary>
    internal class GsMonkStaffT3OrbProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT3");

        private int flightTimer;
        private int boltsFired;

        private bool IsDragon => Projectile.ai[0] >= 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (IsDragon) {
                    //天龙下凡：球体 ×1.5，判定箱同步撑大
                    Projectile.scale = 1.5f;
                    Projectile.Resize(39, 39);
                }
            }
            flightTimer++;
            Projectile.velocity *= 0.985f; //缓减速，不匀速直飞
            Projectile.rotation += 0.16f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            //天龙化：每 24 帧在当前位置降一道落雷，共至多 3 道（owner 端生成）
            if (IsDragon && boltsFired < 3 && flightTimer % 24 == 0
                && Projectile.owner == Main.myPlayer) {
                boltsFired++;
                //落雷 0.6× 武器伤：球伤为武器 0.5×，×1.2 还原（生成端算好传入）
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsMonkStaffT3BoltProj>(),
                    Math.Max(1, (int)(Projectile.damage * 1.2f)), 2f, Projectile.owner);
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(IsDragon ? 1 : 2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f) * Projectile.scale,
                    DustID.Electric, -Projectile.velocity * 0.05f, 100, default, 0.55f);
                d.noGravity = true;
                d.noLight = true;
            }
            Lighting.AddLight(Projectile.Center, GsMonkStaffT3.ThunderBlue.ToVector3()
                * (IsDragon ? 0.7f : 0.4f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中电爆：青白 8 粒电火花 + 软光
            SoundEngine.PlaySound(SoundID.DD2_SkyDragonsFuryShot with { Volume = 0.7f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsMonkStaffT3.ThunderBlue,
                0.16f + (IsDragon ? 0.08f : 0f))?.Configure(10, 0.8f);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);
                Color c = Main.rand.NextBool() ? GsMonkStaffT3.ThunderWhite : GsMonkStaffT3.ThunderBlue;
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.38f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < (IsDragon ? 10 : 6); i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f) * Projectile.scale;
                Color c = Main.rand.NextBool() ? GsMonkStaffT3.ThunderWhite : GsMonkStaffT3.ThunderBlue;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsMonkStaffT3.ThunderBlue, 0.14f * Projectile.scale)?.Configure(9, 0.7f);
        }

        private float DrawRand01(int salt) => GsMonkStaffT3Arcs.Rand01(Projectile.identity, salt);

        /// <summary>原版 708 贴图垫底 + 翠鳞加色 + 青白拖尾 + 电弧缠绕（天龙态更大更密+龙鳞光环）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.MonkStaffT3_Alt);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.MonkStaffT3_Alt].Value;
            Texture2D soft = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Vector2 origin = tex.Size() / 2f;

            //青白拖尾（加色 A=0）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                float fadeT = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size / 2f) - Main.screenPosition;
                Color trail = Color.Lerp(GsMonkStaffT3.ThunderBlue, GsMonkStaffT3.ThunderWhite, fadeT)
                    * (0.20f * fadeT);
                trail.A = 0;
                Main.EntitySpriteDraw(tex, at, null, trail, Projectile.oldRot[i], origin,
                    Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 9f) + (DrawRand01(4) * 6.28f)));

            //光晕垫底
            if (soft != null) {
                Color halo = GsMonkStaffT3.ThunderBlue * (0.30f * pulse);
                halo.A = 0;
                Main.EntitySpriteDraw(soft, drawPos, null, halo, 0f, soft.Size() / 2f,
                    0.55f * Projectile.scale, SpriteEffects.None, 0);
            }

            //本体 + 翠鳞加色
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0);
            Color jade = GsMonkStaffT3.DragonJade * (0.28f * pulse);
            jade.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, jade, Projectile.rotation, origin,
                Projectile.scale * 1.05f, SpriteEffects.None, 0);

            //青白电弧缠绕：2~3 段短折线随帧换形（identity 播种，禁 shader）
            GsMonkStaffT3Arcs.DrawArcCluster(Main.spriteBatch, Projectile.identity, Projectile.Center,
                15f * Projectile.scale, IsDragon ? 5 : 3, 0.5f * pulse);

            //天龙态：龙鳞微十字
            if (IsDragon && star != null) {
                Color cross = GsMonkStaffT3.DragonJade * (0.4f * pulse);
                cross.A = 0;
                Main.EntitySpriteDraw(star, drawPos, null, cross, Main.GlobalTimeWrappedHourly * 1.1f,
                    star.Size() / 2f, 0.11f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 落雷：出生于球位，瞬时雷柱向下探地（至多 320px，探到实心块止），
    /// 竖线宽 40 一击（伤害由生成端按 0.6× 武器伤算好传入），寿命 12 帧、伤害窗前 5 帧。
    /// 自绘：StarTexture 纵向大拉伸双层（白芯+青缘）+落点 SoftGlow 爆点+电火花
    /// </summary>
    internal class GsMonkStaffT3BoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT3");

        private const int Life = 12;
        /// <summary>伤害窗：前 5 帧</summary>
        private const int HitWindow = 5;
        /// <summary>向下探地上限（px）</summary>
        private const float MaxLen = 320f;

        /// <summary>雷柱长度，首帧按各端一致的物块数据算出</summary>
        private float strikeLen = MaxLen;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一道落雷对同一目标只命中一次
            Projectile.timeLeft = Life;
        }

        public override bool ShouldUpdatePosition() => false;

        private Vector2 StrikeBottom => Projectile.Center + new Vector2(0f, strikeLen);

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //向下逐格扫实心块定柱底（物块数据各端一致）
                Point tile = Projectile.Center.ToTileCoordinates();
                strikeLen = MaxLen;
                for (int j = 0; j <= 20; j++) {
                    if (WorldGen.InWorld(tile.X, tile.Y + j) && WorldGen.SolidTile(tile.X, tile.Y + j)) {
                        strikeLen = ((tile.Y + j) * 16f) - Projectile.Center.Y;
                        break;
                    }
                }
                strikeLen = MathHelper.Clamp(strikeLen, 48f, MaxLen);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.9f, Pitch = -0.2f }, StrikeBottom);
                    PRTLoader.NewParticle<PRT_Light>(StrikeBottom, Vector2.Zero, GsMonkStaffT3.ThunderWhite, 0.2f)
                        ?.Configure(10, 0.85f);
                    for (int i = 0; i < 7; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1.5f, 5f));
                        Color c = Main.rand.NextBool() ? GsMonkStaffT3.ThunderWhite : GsMonkStaffT3.ThunderBlue;
                        PRTLoader.NewParticle<PRT_Spark>(StrikeBottom, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                            ?.Configure(true, Main.rand.Next(12, 20));
                    }
                }
            }
            Lighting.AddLight(Vector2.Lerp(Projectile.Center, StrikeBottom, 0.5f),
                GsMonkStaffT3.ThunderBlue.ToVector3() * (0.8f * (Projectile.timeLeft / (float)Life)));
        }

        public override bool? CanDamage() => Projectile.timeLeft > Life - HitWindow ? null : false;

        /// <summary>判定：球位→柱底的竖线宽 40</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, StrikeBottom, 40f, ref collisionPoint);
        }

        private float DrawRand01(int salt) => GsMonkStaffT3Arcs.Rand01(Projectile.identity, salt);

        /// <summary>雷柱双层：白芯+青缘纵向大拉伸（星贴图两端自然收口），宽度随生命衰减，落点爆点</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D soft = CWRAsset.SoftGlow?.Value;
            if (star == null || soft == null) {
                return false;
            }
            float t = 1f - (Projectile.timeLeft / (float)Life);
            float bright = MathF.Pow(1f - t, 1.35f) * (Projectile.timeLeft >= Life - 1 ? 1.3f : 1f);
            float flicker = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 40f) + (DrawRand01(3) * 6.28f)));
            float widthLife = 0.65f + (0.5f * (1f - t)); //宽度生命周期：出生最粗

            Vector2 mid = Vector2.Lerp(Projectile.Center, StrikeBottom, 0.5f) - Main.screenPosition;
            float lenScale = strikeLen / star.Height * 1.1f;

            //青缘
            Color edge = GsMonkStaffT3.ThunderBlue * (0.5f * bright * flicker);
            edge.A = 0;
            Main.EntitySpriteDraw(star, mid, null, edge, 0f, star.Size() / 2f,
                new Vector2(0.13f * widthLife, lenScale), SpriteEffects.None, 0);
            //白芯
            Color core = GsMonkStaffT3.ThunderWhite * (0.9f * bright);
            core.A = 0;
            Main.EntitySpriteDraw(star, mid, null, core, 0f, star.Size() / 2f,
                new Vector2(0.055f * widthLife, lenScale * 0.98f), SpriteEffects.None, 0);

            //落点爆点：白芯 + 青晕 + 微十字
            Vector2 bottom = StrikeBottom - Main.screenPosition;
            Color hitCore = GsMonkStaffT3.ThunderWhite * (0.6f * bright);
            hitCore.A = 0;
            Main.EntitySpriteDraw(soft, bottom, null, hitCore, 0f, soft.Size() / 2f, 0.42f, SpriteEffects.None, 0);
            Color hitHalo = GsMonkStaffT3.ThunderBlue * (0.4f * bright);
            hitHalo.A = 0;
            Main.EntitySpriteDraw(soft, bottom, null, hitHalo, 0f, soft.Size() / 2f, 0.8f + (t * 0.3f), SpriteEffects.None, 0);
            Color hitCross = GsMonkStaffT3.DragonJade * (0.35f * bright);
            hitCross.A = 0;
            Main.EntitySpriteDraw(star, bottom, null, hitCross, DrawRand01(6) * MathHelper.TwoPi, star.Size() / 2f,
                0.09f, SpriteEffects.None, 0);

            //球位顶光
            Color topGlow = GsMonkStaffT3.ThunderBlue * (0.3f * bright);
            topGlow.A = 0;
            Main.EntitySpriteDraw(soft, Projectile.Center - Main.screenPosition, null, topGlow, 0f,
                soft.Size() / 2f, 0.34f, SpriteEffects.None, 0);
            return false;
        }
    }
}
