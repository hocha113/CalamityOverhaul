using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using InnoVault.GameContent.BaseEntity;
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
    /// 【阴森刀锋】材质：缠着怨魂的骨柄长刃。签名：①绕臂椭圆蓄势-爆刺-尖停四相点刺
    /// （保真原版无 autoReuse 的单刺手感与每刺纵幅随机）②每刺命中至多召一只怨魂
    /// （保真原版 800px 随机选目标、侧方 120px 生成）③怨魂环舞——幽灵全额穿过目标后
    /// 绕其一圈补一记半伤，连三刺各命中则第三刺双幽灵
    /// </summary>
    internal class GsMonkStaffT2 : GodSmithScheme
    {
        public override int TargetItemID => ItemID.MonkStaffT2;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: a coiling four-beat thrust; each landed stab calls one ghast that pierces its mark, then rings around it for a half-damage encore. Three landed stabs in a row call twin ghasts";
        /// <summary>连续命中刺数；方案单例跨玩家共享，只在 myPlayer 守门路径（owner 命中/收刺）消费</summary>
        private int thrustStreak;

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却；原版无 autoReuse（物品自身 autoReuse=false），
            //按住不松只出一刺，点刺手感保真，方案不做连发
            if (HeldAlive<GsMonkStaffT2Held>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsMonkStaffT2Held>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版刺击；远端靠弹幕同步看到动作
            return false;
        }

        /// <summary>记一刺命中（只在 owner 命中路径被调）；返回含本刺的连击数，第 3 刺自动清零</summary>
        internal int AddThrustHit() {
            thrustStreak++;
            int result = thrustStreak;
            if (thrustStreak >= 3) {
                thrustStreak = 0;
            }
            return result;
        }

        /// <summary>断刺清零（收刺未命中时由 held 在 owner 端回报）</summary>
        internal void BreakThrustStreak() => thrustStreak = 0;

        //底伤不加成（×1.0）：怨魂全额 + 环舞 0.5 倍补口已计入 DPS 包络（约 105%~118%）
    }

    /// <summary>
    /// 阴森刀锋手持：27 帧四相点刺（÷攻速）= 绕臂蓄势 14f（枪沿椭圆环绕一周，
    /// 纵幅 identity 播种 ±30~70px 复刻原版每刺随机）→ 刺出 2f（爆）→ 尖停 3f（判定最强）→ 收 8f。<br/>
    /// 判定 = 手→枪尖线宽 26，伤害窗 = 刺出+尖停，每刺每目标一击；
    /// 命中召怨魂（每刺至多一只，目标选取镜像原版）
    /// </summary>
    internal class GsMonkStaffT2Held : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT2");

        private const int PhaseGather = 0;
        private const int PhaseThrust = 1;
        private const int PhaseApex = 2;
        private const int PhaseRecover = 3;

        /// <summary>蓄势期收拢触及（px）</summary>
        private const float RestReach = 44f;
        /// <summary>满刺触及：原版 shootSpeed42+22+40 ≈ 104px</summary>
        private const float FullReach = 104f;
        /// <summary>蓄势椭圆纵轴（沿瞄准向的回收深度）</summary>
        private const float BackAmp = 42f;

        private int gatherDur = 14;
        private int thrustDur = 2;
        private int apexDur = 3;
        private int recoverDur = 8;
        private int totalDur = 27;

        private int timer;
        private float baseAngle;
        private int facingDir = 1;
        /// <summary>本刺椭圆纵幅（identity 播种 ±30~70px，复刻原版每刺纵幅随机）</summary>
        private float vertAmp;
        private float reach = RestReach;
        private Vector2 posOffset;
        private Vector2 tipPos;
        private bool thrustSoundPlayed;
        /// <summary>每刺至多一只怨魂的闸门</summary>
        private bool ghastSpawned;
        private float bodyLean;
        private bool bodyLeanApplied;
        private readonly HashSet<int> hitNPCs = [];

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 AimVec => baseAngle.ToRotationVector2();

        private int CurrentPhase {
            get {
                if (timer <= gatherDur) {
                    return PhaseGather;
                }
                if (timer <= gatherDur + thrustDur) {
                    return PhaseThrust;
                }
                if (timer <= gatherDur + thrustDur + apexDur) {
                    return PhaseApex;
                }
                return PhaseRecover;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //每刺每目标一击
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private void InitThrust() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);

            //identity 播种纵幅：各端一致，复刻原版 ai0=随机值×速×0.75×方向 的每刺纵幅
            float mag = 30f + (SeedRand01(3) * 40f);
            vertAmp = (Projectile.identity % 2 == 0 ? 1f : -1f) * mag;

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));
            gatherDur = D(14);
            thrustDur = D(2);
            apexDur = D(3);
            recoverDur = D(8);
            totalDur = gatherDur + thrustDur + apexDur + recoverDur;
            tipPos = Hand + (AimVec * RestReach);
        }

        public override void AI() {
            if (Item.type != ItemID.MonkStaffT2 || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            if (timer == 0) {
                InitThrust();
            }
            timer++;

            int phase = CurrentPhase;
            UpdateGlaiveTransform(phase);
            UpdatePose(phase);
            HandlePhaseEvents(phase);

            if (timer >= totalDur) {
                Projectile.Kill();
            }
        }

        /// <summary>枪位时间线：蓄势沿椭圆环绕一周（慢速=收），爆刺直线满伸，尖停驻留，收势回撤</summary>
        private void UpdateGlaiveTransform(int phase) {
            Vector2 perp = AimVec.RotatedBy(MathHelper.PiOver2);
            switch (phase) {
                case PhaseGather: {
                    float q = timer / (float)gatherDur;
                    //慢-快-慢走完一整圈椭圆，收尾归位蓄满
                    float phi = MathHelper.TwoPi * SmoothStep01(q);
                    posOffset = (AimVec * ((MathF.Cos(phi) - 1f) * 0.5f * BackAmp))
                        + (perp * (MathF.Sin(phi) * vertAmp));
                    reach = RestReach;
                    break;
                }
                case PhaseThrust: {
                    float q = (timer - gatherDur) / (float)thrustDur;
                    posOffset = Vector2.Zero;
                    reach = MathHelper.Lerp(RestReach, FullReach, MathF.Pow(q, 0.75f));
                    break;
                }
                case PhaseApex: {
                    posOffset = Vector2.Zero;
                    reach = FullReach;
                    break;
                }
                default: {
                    float q = (timer - gatherDur - thrustDur - apexDur) / (float)recoverDur;
                    posOffset = Vector2.Zero;
                    reach = MathHelper.Lerp(FullReach, RestReach + 8f, SmoothStep01(q));
                    break;
                }
            }
            tipPos = Hand + posOffset + (AimVec * reach);
        }

        /// <summary>伤害窗 = 刺出 + 尖停</summary>
        private bool DamageActive => CurrentPhase is PhaseThrust or PhaseApex;

        public override bool? CanDamage() => DamageActive ? null : false;

        /// <summary>持械姿态：手臂追枪身，蓄势后仰爆刺前压</summary>
        private void UpdatePose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Vector2 toTip = tipPos - Hand;
            float armAngle = toTip.LengthSquared() < 400f ? baseAngle : toTip.ToRotation();
            Owner.itemRotation = (armAngle.ToRotationVector2() * Owner.direction).ToRotation();
            Player.CompositeArmStretchAmount stretch = phase is PhaseThrust or PhaseApex
                ? Player.CompositeArmStretchAmount.Full
                : Player.CompositeArmStretchAmount.ThreeQuarters;
            Owner.SetCompositeArmFront(true, stretch, armAngle - MathHelper.PiOver2);

            Projectile.Center = Vector2.Lerp(Hand, tipPos, 0.6f);
            Projectile.rotation = baseAngle;

            (float target, float rate) = phase switch {
                PhaseGather => (-facingDir * 0.05f, 0.22f),
                PhaseThrust => (facingDir * 0.10f, 0.65f),
                PhaseApex => (facingDir * 0.10f, 0.4f),
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

        private void HandlePhaseEvents(int phase) {
            //爆刺首帧补播原版刺音（UseSound 被 CanUseItem=false 压掉，在此保真）
            if (!thrustSoundPlayed && phase == PhaseThrust) {
                thrustSoundPlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 0.9f }, Owner.Center);
                }
            }
        }

        /// <summary>贪婪判定：手→枪尖线判宽 26 + 贴身兜底</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!DamageActive) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(6, 6);
            Vector2 hand = Hand;
            if (greedyBox.Distance(hand) <= 30f) {
                return true;
            }
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(),
                hand, tipPos, 26f, ref collisionPoint);
        }

        public override void CutTiles() {
            if (!DamageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Hand, tipPos, 24f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = facingDir; //击退跟刺向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本刺对同一目标只转发一次外部命中钩子（喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //召怨魂：每刺至多一只（owner 端）；连三刺各命中则第三刺双幽灵
            if (!ghastSpawned && Projectile.owner == Main.myPlayer) {
                ghastSpawned = true;
                int streak = 0;
                if (GodSmithScheme.TryGetScheme(ItemID.MonkStaffT2, out GodSmithScheme scheme)
                    && scheme is GsMonkStaffT2 t2) {
                    streak = t2.AddThrustHit();
                }
                NPC ghastTarget = PickGhastTarget();
                if (ghastTarget != null) {
                    int side = Main.rand.NextBool() ? 1 : -1;
                    SpawnGhast(ghastTarget, side);
                    if (streak >= 3) {
                        SpawnGhast(ghastTarget, -side);
                    }
                }
            }
        }

        /// <summary>目标选取镜像原版 SummonMonkGhast：玩家 800px 内可追踪敌人随机一只</summary>
        private NPC PickGhastTarget() {
            List<int> candidates = null;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile)
                    && npc.Distance(Owner.Center) < 800f) {
                    (candidates ??= []).Add(i);
                }
            }
            if (candidates == null) {
                return null;
            }
            return Main.npc[candidates[Main.rand.Next(candidates.Count)]];
        }

        /// <summary>怨魂从目标侧方 120px 生成，velocity 朝目标（镜像原版），全额伤害</summary>
        private void SpawnGhast(NPC target, int side) {
            Vector2 pos = target.Center + new Vector2(side * 120f, 0f);
            Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.UnitX * -side) * 6f;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), pos, vel,
                ModContent.ProjectileType<GsMonkStaffT2GhastProj>(),
                Projectile.damage, Projectile.knockBack * 0.5f, Owner.whoAmI, target.whoAmI);
        }

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
            //断刺清零：本刺落空即断连击（owner 端回报方案）
            if (!ghastSpawned && Projectile.owner == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.MonkStaffT2, out GodSmithScheme scheme)
                && scheme is GsMonkStaffT2 t2) {
                t2.BreakThrustStreak();
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种），蓄势纵幅用</summary>
        private float SeedRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - (2f * x));
        }

        /// <summary>枪体本体一笔：原版物品贴图沿枪角（对角贴图补 π/4），origin 取杆尾握把端</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0) {
                return false;
            }
            Main.instance.LoadItem(ItemID.MonkStaffT2);
            Texture2D tex = TextureAssets.Item[ItemID.MonkStaffT2].Value;
            Vector2 origin = new(8f, tex.Height - 8f);
            float diag = new Vector2(tex.Width, tex.Height).Length();
            float visLen = 100f;
            float scale = visLen / MathF.Max(diag - 14f, 1f);
            Vector2 gripPos = tipPos - (AimVec * visLen) - Main.screenPosition;
            Main.spriteBatch.Draw(tex, gripPos, null, lightColor, baseAngle + MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 怨魂：直线全额穿过目标（越过其中心 40px）后转入环舞相——绕目标 70px 一圈
    /// （角速 0.18/帧，随目标移动），环舞中补一记 0.5 倍（复击冷却 30 帧自然放行），
    /// 绕满一圈散场。贴图借原版怨魂（700）默认绘制，半透走 Projectile.alpha。<br/>
    /// ai[0]=目标索引（随生成包过线），ai[1]=相位 0 直线 1 环舞（ModifyHitNPC 减伤标记）
    /// </summary>
    internal class GsMonkStaffT2GhastProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MonkStaffT2Ghast;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT2");

        /// <summary>环舞半径（px）</summary>
        private const float OrbitRadius = 70f;
        /// <summary>环舞角速（弧度/帧）</summary>
        private const float OrbitStep = 0.18f;

        private float orbitAngle;
        private float orbitAccum;
        private int orbitSign = 1;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.MonkStaffT2Ghast];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = false; //怨魂穿墙穿体
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30; //直线击后自然放行环舞补击
            Projectile.timeLeft = 120;
            Projectile.alpha = 96; //怨魂半透
        }

        /// <summary>环舞相位置由 AI 直写，直线相走原生位移</summary>
        public override bool ShouldUpdatePosition() => Projectile.ai[1] == 0f;

        private NPC OrbitTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.CanBeChasedBy(Projectile) ? npc : null;
            }
        }

        public override void AI() {
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
            NPC target = OrbitTarget;

            if (Projectile.ai[1] == 0f) {
                //直线穿行：越过目标中心 40px 即入环舞（各端按同步位置各自判定，owner 补 netUpdate 校正）
                if (target != null) {
                    Vector2 rel = Projectile.Center - target.Center;
                    Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    if (Vector2.Dot(rel, dir) > 40f) {
                        Projectile.ai[1] = 1f;
                        orbitAngle = rel.ToRotation();
                        float crossZ = (rel.X * Projectile.velocity.Y) - (rel.Y * Projectile.velocity.X);
                        orbitSign = MathF.Abs(crossZ) < 0.05f
                            ? (Projectile.identity % 2 == 0 ? 1 : -1)
                            : Math.Sign(crossZ); //顺着当前切向动量绕
                        if (Projectile.owner == Main.myPlayer) {
                            Projectile.netUpdate = true;
                        }
                    }
                }
            }
            else {
                //环舞：目标没了魂雾即散
                if (target == null) {
                    Projectile.Kill();
                    return;
                }
                orbitAngle += OrbitStep * orbitSign;
                orbitAccum += OrbitStep;
                Projectile.Center = target.Center + (orbitAngle.ToRotationVector2() * OrbitRadius);
                //切向速度只喂朝向（位置由上行直写）
                Projectile.velocity = (orbitAngle + (orbitSign * MathHelper.PiOver2)).ToRotationVector2()
                    * (OrbitStep * OrbitRadius);
                if (orbitAccum >= MathHelper.TwoPi) {
                    Projectile.Kill();
                    return;
                }
            }

            //幽灵朝向同原版：贴速度向，左行翻面
            Projectile.spriteDirection = Projectile.direction = Projectile.velocity.X >= 0f ? 1 : -1;
            Projectile.rotation = Projectile.velocity.ToRotation()
                + (Projectile.spriteDirection == -1 ? MathHelper.Pi : 0f);
        }

        /// <summary>环舞补击减伤 0.5 倍（ai[1] 相位标记即减伤标记）</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Projectile.ai[1] == 1f) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaiveImpactGhost with { Volume = 0.8f }, Projectile.Center);
        }
    }
}
