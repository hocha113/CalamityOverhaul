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
    /// 【腐蚀者之灾·A档】材质：腐化甲壳标枪，枪身渗腐液。
    /// 签名：①标枪命中在目标身上钉出腐化疮口，小噬魂者优先俯冲疮口目标
    /// ②疮口吃满 3 口爆浆：0.8×武器伤 AoE + 咒火 ③落点炸出 2~4 只小噬魂者（1% 十五只彩蛋保留）
    /// </summary>
    internal class GsScourgeoftheCorruptor : GodSmithScheme
    {
        public override int TargetItemID => ItemID.ScourgeoftheCorruptor;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: the javelin bursts into a brood of tiny eaters on impact; it also pins a festering sore on whatever it strikes - eaters dive at the sore, and three bites make it erupt in a caustic blast";
        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 弹幕总帧)，两者都吃攻速）
            if (HeldAlive<GsScourgeHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsScourgeHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版投掷；远端靠弹幕同步看到动作
            return false;
        }

        //底伤 ×1.0：疮口爆浆（0.8× AoE+咒火）与噬魂者定向俯冲的收益已计入 DPS 包络（约原版 105%~118%）
    }

    /// <summary>
    /// 腐蚀者之灾手持投掷。三相 过肩蓄势-掷-收；掷出帧前倾爆发
    /// </summary>
    internal class GsScourgeHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ScourgeoftheCorruptor");

        private const int PhaseCharge = 0;
        private const int PhaseThrow = 1;
        private const int PhaseRecover = 2;

        //阶段时长，InitStage 写入（已含攻速缩放）
        private int chargeDur = 6;
        private int throwDur = 3;
        private int recoverDur = 8;
        private int totalDur;

        private float baseAngle;
        private int facingDir = 1;
        private float armAngle;
        private float bodyLean;
        private bool bodyLeanApplied;
        private bool javelinThrown;
        private int timer;

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 HandPos => Hand + (armAngle.ToRotationVector2() * 20f);

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
            Projectile.friendly = false; //纯演出手持，伤害全在标枪
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
            recoverDur = D(8);
            totalDur = chargeDur + throwDur + recoverDur;
        }

        public override void AI() {
            if (Item.type != ItemID.ScourgeoftheCorruptor || Owner.dead || !Owner.active) {
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

        /// <summary>臂角时间线：过肩举枪后拉，掷出过冲下压，收势渐直</summary>
        private void UpdateArm(int phase) {
            float lift;
            switch (phase) {
                case PhaseCharge: {
                    float p = timer / (float)chargeDur;
                    lift = MathHelper.Lerp(0.75f, 0.55f, EaseOutQuad(p));
                    break;
                }
                case PhaseThrow: {
                    float p = (timer - chargeDur) / (float)throwDur;
                    lift = MathHelper.Lerp(0.55f, -0.18f, EaseOutQuad(Math.Min(1f, p * 1.4f)));
                    break;
                }
                default: {
                    float p = (timer - chargeDur - throwDur) / (float)recoverDur;
                    lift = MathHelper.Lerp(-0.18f, 0.04f, SmoothStep01(p));
                    break;
                }
            }
            armAngle = baseAngle - (facingDir * lift);
        }

        /// <summary>持械姿态，蓄势后仰掷出前倾</summary>
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
                PhaseCharge => (-facingDir * 0.05f, 0.3f),
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
            if (javelinThrown || phase != PhaseThrow) {
                return;
            }
            javelinThrown = true;
            if (Projectile.owner == Main.myPlayer) {
                float speed = Item.shootSpeed;
                if (speed <= 0f) {
                    speed = 14f; //原版 shootSpeed 14 兜底
                }
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), HandPos,
                    baseAngle.ToRotationVector2() * speed,
                    ModContent.ProjectileType<GsScourgeEatersBiteProj>(),
                    Projectile.damage, Projectile.knockBack, Owner.whoAmI);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item39 with { Volume = 0.9f, Pitch = -0.15f }, Owner.Center);
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

        /// <summary>蓄势期手中标枪本体一笔：原版弹幕 306 贴图（掷出后手空不画）</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0 || CurrentPhase != PhaseCharge) {
                return false;
            }
            Main.instance.LoadProjectile(ProjectileID.EatersBite);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.EatersBite].Value;
            Vector2 at = HandPos - Main.screenPosition;
            Main.spriteBatch.Draw(tex, at, null, lightColor, armAngle + MathHelper.PiOver4, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 噬咬标枪：轻重力直掷（40 更新后渐坠），单穿 extraUpdates1。
    /// 命中钉疮口（owner 守门，同目标不重复）；死亡镜像原版爆虫
    /// （2~4 只、1% 十五只彩蛋、伤害×0.75 击退×0.35 初速±7）。贴图借原版噬咬标枪（306）默认绘制
    /// </summary>
    internal class GsScourgeEatersBiteProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EatersBite;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ScourgeoftheCorruptor");

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;      //原版单穿
            Projectile.extraUpdates = 1;   //原版双更新
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            Projectile.ai[0]++;
            //轻重力：40 更新（约 20 游戏帧）后渐坠
            if (Projectile.ai[0] > 40f) {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.045f, 14f);
            }
            //枪头顺飞行向（306 贴图斜 45°，镜像原版姿态）
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //疮口轮猎：钉疮口（owner 守门；同目标已有本 owner 疮口则不重复钉）
            if (Projectile.owner == Main.myPlayer) {
                int soreType = ModContent.ProjectileType<GsScourgeSoreProj>();
                bool exists = false;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == soreType && p.owner == Projectile.owner
                        && (int)p.ai[0] == target.whoAmI) {
                        exists = true;
                        break;
                    }
                }
                if (!exists) {
                    //疮口伤害=0.8×武器伤，经 NewProjectile damage 参数过线
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                        soreType, (int)(Projectile.damage * 0.8f), 0f, Projectile.owner, target.whoAmI);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //爆虫：镜像原版（owner 端；2~4 只，1% 彩蛋 15 只；伤害×0.75 击退×0.35 初速±7）
            if (Projectile.owner == Main.myPlayer) {
                int count = Main.rand.Next(2, 5);
                if (Main.rand.Next(1, 101) == 100) {
                    count = 15;
                }
                for (int i = 0; i < count; i++) {
                    Vector2 vel = new(Main.rand.Next(-35, 36) * 0.2f, Main.rand.Next(-35, 36) * 0.2f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        ModContent.ProjectileType<GsScourgeTinyEaterProj>(),
                        (int)(Projectile.damage * 0.75), Projectile.knockBack * 0.35f, Projectile.owner);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 小噬魂者：extraUpdates3 单穿 timeLeft600，追踪镜像原版 aiStyle36
    /// （曼哈顿 800 视线索敌、巡航 13、逐分量转向 0.35、磁砖反弹 5 次）；
    /// 索敌优先「身上有本 owner 疮口的目标」，命中疮口目标为疮口记一口（owner 守门）。<br/>
    /// 贴图借原版小噬魂者（307）默认绘制（2 帧动画）+ 速度正弦扭摆（相位 identity 播种）
    /// </summary>
    internal class GsScourgeTinyEaterProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TinyEater;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ScourgeoftheCorruptor");

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.TinyEater];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;      //原版单穿
            Projectile.extraUpdates = 3;   //原版四倍更新
            Projectile.timeLeft = 600;     //原版寿命
        }

        public override void AI() {
            Projectile.localAI[0]++;

            //索敌：疮口目标优先，其次镜像原版就近
            int targetIdx = FindTarget();
            if (targetIdx >= 0) {
                HomeTo(Main.npc[targetIdx]);
            }

            //速度正弦扭摆（相位 identity 播种，各端确定性一致）
            float phase = SeedRand01(7) * MathHelper.TwoPi;
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin((Projectile.localAI[0] * 0.22f) + phase) * 0.028f);

            //镜像原版 307 姿态与 2 帧动画（每 6 更新换帧）
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            if (++Projectile.frameCounter >= 6) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        /// <summary>一环：本 owner 疮口所在目标（疮口即狩猎信标，不要求视线）；二环：镜像原版曼哈顿 800+视线就近</summary>
        private int FindTarget() {
            int soreType = ModContent.ProjectileType<GsScourgeSoreProj>();
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != soreType || p.owner != Projectile.owner || p.ai[1] >= 3f) {
                    continue;
                }
                int idx = (int)p.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    continue;
                }
                NPC npc = Main.npc[idx];
                if (!npc.active || !npc.CanBeChasedBy(this)) {
                    continue;
                }
                float d = Projectile.Distance(npc.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = idx;
                }
            }
            if (best >= 0) {
                return best;
            }
            bestDist = 800f; //原版索敌半径（曼哈顿距离）
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(this)) {
                    continue;
                }
                float d = Math.Abs(Projectile.Center.X - npc.Center.X) + Math.Abs(Projectile.Center.Y - npc.Center.Y);
                if (d < bestDist && Collision.CanHit(Projectile.position, Projectile.width, Projectile.height,
                    npc.position, npc.width, npc.height)) {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>镜像原版 aiStyle36：巡航 13，逐分量每更新逼近 0.35（反向回头双倍）</summary>
        private void HomeTo(NPC target) {
            Vector2 want = target.Center - Projectile.Center;
            float len = want.Length();
            if (len < 1f) {
                return;
            }
            want *= 13f / len;
            const float step = 0.35f;
            if (Projectile.velocity.X < want.X) {
                Projectile.velocity.X += step;
                if (Projectile.velocity.X < 0f && want.X > 0f) {
                    Projectile.velocity.X += step * 2f;
                }
            }
            else if (Projectile.velocity.X > want.X) {
                Projectile.velocity.X -= step;
                if (Projectile.velocity.X > 0f && want.X < 0f) {
                    Projectile.velocity.X -= step * 2f;
                }
            }
            if (Projectile.velocity.Y < want.Y) {
                Projectile.velocity.Y += step;
                if (Projectile.velocity.Y < 0f && want.Y > 0f) {
                    Projectile.velocity.Y += step * 2f;
                }
            }
            else if (Projectile.velocity.Y > want.Y) {
                Projectile.velocity.Y -= step;
                if (Projectile.velocity.Y > 0f && want.Y < 0f) {
                    Projectile.velocity.Y -= step * 2f;
                }
            }
        }

        /// <summary>镜像原版：磁砖反弹至多 5 次（ai[1] 计数）</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[1] < 5f) {
                Projectile.ai[1]++;
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X;
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = -oldVelocity.Y;
                }
                return false;
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //噬咬记账：命中疮口目标，为疮口 ai[1]++ 并 netUpdate（owner 守门）
            if (Projectile.owner == Main.myPlayer) {
                int soreType = ModContent.ProjectileType<GsScourgeSoreProj>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == soreType && p.owner == Projectile.owner
                        && (int)p.ai[0] == target.whoAmI && p.ai[1] < 3f) {
                        p.ai[1]++;
                        p.netUpdate = true;
                        break;
                    }
                }
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种），扭摆相位用</summary>
        private float SeedRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>
    /// 腐化疮口：钉附目标的驻场标记（friendly=false 常态），timeLeft 180，目标死亡即自杀。
    /// ai[0]=宿主 whoAmI，ai[1]=噬咬数（随 netUpdate 过线）；吃满 3 口爆浆：
    /// Resize 120、friendly=true 开 6 帧伤害窗（伤害=生成时 0.8×武器伤）、命中挂咒火 120 后自杀。<br/>
    /// 贴图借原版咒火（244）默认绘制
    /// </summary>
    internal class GsScourgeSoreProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CursedFlameFriendly;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ScourgeoftheCorruptor");

        private const int BurstWindow = 6;

        private int HostIdx => (int)Projectile.ai[0];
        /// <summary>localAI[0]>0 即爆浆中（各端观测 ai[1] 同步进入）</summary>
        private bool Bursting => Projectile.localAI[0] > 0f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false; //常态纯标记
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一次爆浆对同一目标只命中一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (HostIdx < 0 || HostIdx >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }
            NPC host = Main.npc[HostIdx];
            if (!host.active) {
                Projectile.Kill();
                return;
            }

            //贴附目标身位（漂移点 identity 播种，各端一致）
            Vector2 drift = new((SeedRand01(1) - 0.5f) * host.width * 0.6f,
                (SeedRand01(2) - 0.5f) * host.height * 0.6f);
            Projectile.Center = host.Center + drift;

            if (Bursting) {
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] > BurstWindow + 2f) {
                    Projectile.Kill();
                }
                return;
            }

            //吃满 3 口爆浆（各端观测同步的 ai[1] 各自进入，owner 端判伤权威）
            if (Projectile.ai[1] >= 3f) {
                StartBurst();
            }
        }

        /// <summary>爆浆：撑判定箱开伤害窗</summary>
        private void StartBurst() {
            Projectile.localAI[0] = 1f;
            Projectile.Resize(120, 120);
            Projectile.friendly = true;
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, BurstWindow + 4);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
        }

        /// <summary>伤害窗只开爆浆前 6 帧</summary>
        public override bool? CanDamage() => Bursting && Projectile.localAI[0] <= BurstWindow ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.CursedInferno, 120);

        /// <summary>确定性伪随机（identity+salt 播种），贴附漂移点用</summary>
        private float SeedRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
