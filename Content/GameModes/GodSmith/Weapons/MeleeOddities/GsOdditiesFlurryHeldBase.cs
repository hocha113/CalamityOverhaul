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
    /// 【乱舞组共享手持基类】原版 Terragrim/Arkhalis（aiStyle75）的重铸骨架：
    /// 按住持续乱舞，驻场跟随玩家、朝向光标，判定=玩家前方 68×64 大盒（原版 5 帧复击、12 帧一记挥砍音）。<br/>
    /// 乱舞视觉：每 FlashInterval 帧闪现一道刃影姿势（identity+姿势序号播种的确定性随机角，
    /// 各端看到同一场乱舞），新姿势前 2 帧最亮（本体+加色高亮），随后衰减为残影，
    /// 同屏保留最近数道渐淡旧姿势，每道配短涂抹，乱舞区低密度光尘。<br/>
    /// 联机：松手信号走 InnoVault DownLeft（方案 CanUseItem 全程压原版，player.channel 永不置位，
    /// 已对 TML 源核实）；瞄准向存 velocity，owner 侧 4 帧节流 netUpdate；粒子/音效守 !isServer；
    /// 绘制禁 Main.rand，抖动一律 identity 播种
    /// </summary>
    internal abstract class GsOdditiesFlurryHeldBase : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName
            => Language.GetText("ItemName." + ItemID.Search.GetName(SwordItemID));

        //==================== 子类契约 ====================

        /// <summary>目标物品 ID：物品切换自杀检查 + 刃影贴图来源</summary>
        protected abstract int SwordItemID { get; }

        /// <summary>刃缘亮色（新姿势高亮/火星主色）</summary>
        protected abstract Color EdgeBright { get; }

        /// <summary>刃身主色（涂抹/光尘/灯光）</summary>
        protected abstract Color BodyMain { get; }

        /// <summary>热点强调色（重击/满蓄反馈）</summary>
        protected abstract Color HotAccent { get; }

        /// <summary>暗部垫底色，默认近黑钢影</summary>
        protected virtual Color DeepShadow => new(20, 22, 28);

        /// <summary>几帧换一次刃影姿势</summary>
        protected virtual int FlashInterval => 5;

        /// <summary>刃影相对瞄准向的最大偏角（弧度）</summary>
        protected virtual float SpreadArc => 0.7f;

        /// <summary>同屏保留的旧姿势残影道数</summary>
        protected virtual int GhostKeep => 2;

        /// <summary>乱舞强度 0~1+，乘在刃影亮度与光尘密度上</summary>
        protected virtual float FlurryIntensity => 1f;

        /// <summary>挥砍音基准音高</summary>
        protected virtual float SwingPitch => 0f;

        /// <summary>手→刃影贴图中心的距离（px）</summary>
        protected virtual float BladeReach => 52f;

        /// <summary>刃影贴图缩放</summary>
        protected virtual float BladeScale => 1.15f;

        /// <summary>true 时暂停乱舞（不生成新姿势、不响挥砍音、大盒判定关闭）；旧姿势照常渐隐</summary>
        protected virtual bool FlurrySuspended => false;

        /// <summary>0~1：刃影姿势向瞄准线收拢的程度（处决突刺的收束演出用）</summary>
        protected virtual float PoseConvergence => 0f;

        /// <summary>乱舞命中追加（识破/生长等记账；OnHitNPC 只在攻击方端跑）</summary>
        protected virtual void OnFlurryHit(NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>松手/缴械/被控收刀前的结算；各端都会被调，弹幕生成自守 owner</summary>
        protected virtual void OnRelease() { }

        /// <summary>子类每帧扩展（生长曲线/突刺状态机）</summary>
        protected virtual void FlurryAI() { }

        //==================== 常量与状态 ====================

        /// <summary>判定盒中心 = 玩家中心 + 瞄准向 × 52（原版 aiStyle75 几何）</summary>
        private const float BoxForward = 52f;
        /// <summary>新姿势的高亮帧数（闪现语法：先亮后衰）</summary>
        private const int FlashBrightFrames = 2;
        /// <summary>开局宽限：远端首包 DownLeft 未到前不做松手判定</summary>
        private const int SpawnGrace = 4;

        private struct FlurryPose
        {
            public float Angle;
            public float TiltSide;
            public int Birth;
            public bool Valid;
        }

        /// <summary>姿势环形缓存，[0] 最新；容量 = 最大保留数（当前 1 + 残影 3）</summary>
        private readonly FlurryPose[] poses = new FlurryPose[4];
        private int poseCountdown;
        private int poseIndex;
        private Vector2 lastSyncedAim;
        private float bodyLean;
        private bool bodyLeanApplied;
        /// <summary>本场乱舞已转发过外部命中钩子的目标（每目标只喂一次饰品链）</summary>
        private readonly HashSet<int> hitNPCs = [];

        /// <summary>存活帧计数，子类只读驱动生长/节奏</summary>
        protected int timer;
        /// <summary>出手朝向 ±1，按瞄准向每帧刷新</summary>
        protected int facingDir = 1;

        protected Vector2 Hand => Owner.GetPlayerStabilityCenter();
        /// <summary>瞄准单位向量：owner 每帧写自鼠标，远端读 velocity 同步值</summary>
        protected Vector2 AimUnit => Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
        protected float AimAngle => AimUnit.ToRotation();

        /// <summary>压掉基类逐帧鼠标移动发包，瞄准同步改走 velocity 的 4 帧节流</summary>
        public override bool CanFire => false;

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 64;   //原版 595/735 判定箱
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5; //原版 5 帧复击节奏
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        //==================== 主循环 ====================

        public override void AI() {
            //物品切换/死亡自杀
            if (Item.type != SwordItemID || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            //模式中途关闭：静默收场，不触发松手结算
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }

            timer++;
            UpdateAim();

            //松手/缴械/被控 → 结算后收刀（开局留宽限等远端首包 DownLeft）
            if (timer > SpawnGrace && (!DownLeft || Owner.noItems || Owner.CCed)) {
                OnRelease();
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2; //自续：AI 停跑即自然消亡
            Projectile.Center = Owner.Center + (AimUnit * BoxForward);
            Projectile.rotation = AimAngle;

            UpdateHeldPose();
            if (!FlurrySuspended) {
                UpdateFlurryPoses();
                HandleSoundAndDust();
            }
            FlurryAI();

            Lighting.AddLight(Projectile.Center, BodyMain.ToVector3() * (0.35f * FlurryIntensity));
        }

        /// <summary>owner 每帧读鼠标写 velocity；同步 4 帧节流（DownLeft 变化由基类即时发包）</summary>
        private void UpdateAim() {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            Vector2 aim = ToMouse.SafeNormalize(Vector2.UnitX * Owner.direction);
            Projectile.velocity = aim;
            if (timer % 4 == 0 && (aim - lastSyncedAim).LengthSquared() > 0.0001f) {
                lastSyncedAim = aim;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>持械姿态：itemTime 钉住、前臂指向瞄准向、朝向跟手</summary>
        private void UpdateHeldPose() {
            float aimAngle = AimAngle;
            float cos = MathF.Cos(aimAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (AimUnit * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, aimAngle - MathHelper.PiOver2);

            //乱舞压身前倾，收束时略加深
            float target = facingDir * (0.05f + (0.04f * PoseConvergence));
            bodyLean = MathHelper.Lerp(bodyLean, target, 0.2f);
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

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        /// <summary>确定性乱舞角：左右交替 × identity+姿势序号播种的随机幅度，各端同一场乱舞</summary>
        private void UpdateFlurryPoses() {
            if (--poseCountdown > 0) {
                return;
            }
            poseCountdown = Math.Max(2, FlashInterval);
            poseIndex++;
            float side = poseIndex % 2 == 0 ? 1f : -1f;
            float mag = 0.25f + (0.75f * DrawRand01(poseIndex));
            for (int i = poses.Length - 1; i > 0; i--) {
                poses[i] = poses[i - 1];
            }
            poses[0] = new FlurryPose {
                Angle = AimAngle + (side * mag * SpreadArc),
                TiltSide = side,
                Birth = timer,
                Valid = true,
            };
        }

        /// <summary>原版 12 帧一记挥砍音 + 乱舞区低密度光尘（AI 内 Main.rand 允许）</summary>
        private void HandleSoundAndDust() {
            if (VaultUtils.isServer) {
                return;
            }
            if (timer % 12 == 1) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.62f,
                    Pitch = SwingPitch + Main.rand.NextFloat(-0.08f, 0.08f),
                }, Owner.Center);
            }
            if (Main.rand.NextFloat() < 0.35f * FlurryIntensity) {
                Vector2 at = Projectile.Center
                    + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-26f, 26f));
                Vector2 vel = AimUnit.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(1.5f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(at, vel, Main.rand.NextBool(3) ? HotAccent : EdgeBright,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        //==================== 判定与命中 ====================

        public override bool? CanDamage() => FlurrySuspended ? false : null;

        public override void CutTiles() {
            if (FlurrySuspended) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Owner.Center, Owner.Center + (AimUnit * 90f), 60f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = facingDir;//击退跟出手朝向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本场乱舞对同一目标只转发一次外部命中钩子（模拟物品直击链，喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            OnFlurryHit(target, hit, damageDone);

            //基类默认命中反馈：色板火星，材质分流由子类在 OnFlurryHit 追加
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = AimUnit.RotatedByRandom(0.9) * Main.rand.NextFloat(2.5f, 6.5f);
                    Color c = Main.rand.NextBool(3) ? HotAccent : EdgeBright;
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(true, Main.rand.Next(10, 18));
                }
            }
        }

        //==================== 绘制（禁 Main.rand，identity 播种） ====================

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种）</summary>
        protected float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawFlurry(Main.spriteBatch, lightColor);
            return false;
        }

        /// <summary>刃影姿势：旧→新叠画，新姿势闪现最亮，旧姿势加色残影渐隐</summary>
        protected void DrawFlurry(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            float aimAngle = AimAngle;
            int fadeSpan = Math.Max(6, FlashInterval * (GhostKeep + 1));

            for (int i = poses.Length - 1; i >= 0; i--) {
                if (!poses[i].Valid) {
                    continue;
                }
                int age = timer - poses[i].Birth;
                float fade = 1f - MathHelper.Clamp((age - FlashBrightFrames) / (float)fadeSpan, 0f, 1f);
                if (fade <= 0.03f) {
                    continue;
                }
                //收束演出：姿势角向瞄准线归拢（取最短角差路径）
                float angle = poses[i].Angle
                    + (MathHelper.WrapAngle(aimAngle - poses[i].Angle) * PoseConvergence);
                DrawPose(sb, tex, smear, angle, poses[i].TiltSide, fade, age <= FlashBrightFrames, lightColor);
            }
        }

        private void DrawPose(SpriteBatch sb, Texture2D tex, Texture2D smear, float angle,
            float tiltSide, float fade, bool bright, Color lightColor) {
            float inten = FlurryIntensity;
            Vector2 dirV = angle.ToRotationVector2();

            //姿势短涂抹：加色 A=0 沿姿势角小尺度
            if (smear != null) {
                Color sc = BodyMain * (0.30f * fade * inten);
                sc.A = 0;
                sb.Draw(smear, Hand + (dirV * BladeReach * 0.72f) - Main.screenPosition, null, sc,
                    angle + (tiltSide * 0.3f), smear.Size() / 2f,
                    new Vector2(0.32f, 0.15f), SpriteEffects.None, 0f);
            }

            if (bright) {
                //闪现语法：新姿势前两帧最亮——本体+加色高亮
                DrawBladeAt(sb, lightColor, angle, BladeReach, 0.55f * inten);
            }
            else {
                //残影：只留加色鬼影
                GetBladeDrawOrientation(out SpriteEffects fx, out float rotOff);
                Color ghost = Color.Lerp(EdgeBright, BodyMain, 0.5f) * (0.42f * fade * inten);
                ghost.A = 0;
                sb.Draw(tex, Hand + (dirV * BladeReach) - Main.screenPosition, null, ghost,
                    angle + rotOff, tex.Size() / 2f, BladeScale, fx, 0f);
            }
        }

        /// <summary>实体刃影：暗影垫底 + 本体 + 可选加色辉边（突刺/闪现共用）</summary>
        protected void DrawBladeAt(SpriteBatch sb, Color lightColor, float angle, float reach, float glowStrength) {
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            GetBladeDrawOrientation(out SpriteEffects fx, out float rotOff);
            Vector2 at = Hand + (angle.ToRotationVector2() * reach) - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            Color shadow = DeepShadow;
            shadow.A = 200;
            sb.Draw(tex, at + new Vector2(facingDir, 2f), null, shadow * 0.4f,
                angle + rotOff, origin, BladeScale * 1.02f, fx, 0f);
            sb.Draw(tex, at, null, lightColor, angle + rotOff, origin, BladeScale, fx, 0f);
            if (glowStrength > 0.01f) {
                Color hi = EdgeBright * glowStrength;
                hi.A = 0;
                sb.Draw(tex, at, null, hi, angle + rotOff, origin, BladeScale * 1.04f, fx, 0f);
            }
        }

        /// <summary>反向朝向翻刃：刃口镜像，双向朝向都读得对（同范例映射）</summary>
        protected void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool flip = facingDir < 0;
            effect = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flip ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }
    }
}
