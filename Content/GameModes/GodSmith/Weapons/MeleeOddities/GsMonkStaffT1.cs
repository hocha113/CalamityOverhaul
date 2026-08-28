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
    /// 【瞌睡章鱼棒】材质：沉睡章鱼盘踞的硬木长棍。签名：①抡棍起圈慢末圈快的加速度曲线，
    /// 章鱼头软体滞后拖影两拍 ②收尾砸地（保真原版 3 倍砸击/半程换向/松键提前收棍）掀起
    /// 压扁冲击环与土屑 ③连砸催眠——同一目标 8 秒内挨第 3 记砸击陷入熟睡
    /// （1.3 倍伤害+长缓速+头顶淡紫 Z 泡逐个上浮）
    /// </summary>
    internal class GsMonkStaffT1 : GodSmithScheme
    {
        public override int TargetItemID => ItemID.MonkStaffT1;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: the twirl winds up slow and finishes fast; " +
            "ground slams stay at triple damage, and the third slam on the same target lulls it into deep drowse";

        //瞌睡章鱼色板：淡紫睡意 + 原版绿光 + 霜乳白 + 硬木深褐
        internal static readonly Color SleepPurple = new(196, 156, 255); //睡意淡紫
        internal static readonly Color OctoGreen = new(20, 255, 100);    //原版章鱼绿光
        internal static readonly Color NapBright = new(236, 228, 255);   //乳白亮芯
        internal static readonly Color WoodDeep = new(64, 46, 34);       //硬木深影

        /// <summary>同目标连砸记忆窗（8 秒）</summary>
        internal const int SlamMemoryFrames = 480;

        /// <summary>连砸记账：NPC 索引 → (砸击数, 最后砸击时刻)。
        /// 方案单例跨玩家共享，只在 owner 命中路径写（OnHitNPC 只跑攻击方端，myPlayer 天然守门）</summary>
        private readonly Dictionary<int, (int count, int lastTime)> slamLedger = [];
        /// <summary>过期清扫节拍，只在 myPlayer 路径消费</summary>
        private int ledgerSweepTimer;

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 抡棍总帧)，两者都吃攻速）
            if (HeldAlive<GsMonkStaffT1Held>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsMonkStaffT1Held>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版抡棍；远端靠弹幕同步看到动作
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //定期清掉 8 秒未再砸的目标计数
            if (++ledgerSweepTimer < 120) {
                return;
            }
            ledgerSweepTimer = 0;
            if (slamLedger.Count == 0) {
                return;
            }
            int now = (int)Main.GameUpdateCount;
            List<int> stale = null;
            foreach (KeyValuePair<int, (int count, int lastTime)> kv in slamLedger) {
                if (now - kv.Value.lastTime > SlamMemoryFrames) {
                    (stale ??= []).Add(kv.Key);
                }
            }
            if (stale != null) {
                foreach (int key in stale) {
                    slamLedger.Remove(key);
                }
            }
        }

        /// <summary>本记砸击是否为该目标的第 3 砸（ModifyHitNPC 时机先于记账，只在攻击方端被调）</summary>
        internal bool IsThirdSlam(int npcWho) {
            if (!slamLedger.TryGetValue(npcWho, out (int count, int lastTime) entry)) {
                return false;
            }
            if ((int)Main.GameUpdateCount - entry.lastTime > SlamMemoryFrames) {
                return false;
            }
            return entry.count >= 2;
        }

        /// <summary>记一次砸击命中；达成第 3 砸返回 true 并清零计数（只在攻击方端被调）</summary>
        internal bool RegisterSlam(int npcWho) {
            int now = (int)Main.GameUpdateCount;
            int count = 0;
            if (slamLedger.TryGetValue(npcWho, out (int count, int lastTime) entry)
                && now - entry.lastTime <= SlamMemoryFrames) {
                count = entry.count;
            }
            count++;
            if (count >= 3) {
                slamLedger.Remove(npcWho);
                return true;
            }
            slamLedger[npcWho] = (count, now);
            return false;
        }

        //底伤不加成（×1.0）：3 倍砸击 + 第 3 砸 1.3 倍即预算大头，综合 DPS 落在原版 100%~115%
    }

    /// <summary>
    /// 瞌睡章鱼棒手持：单次完整抡棍，50 帧转整 2 圈（÷攻速）。
    /// 加速度曲线起圈 ×0.7 末圈 ×1.4（线性权重中点采样，积分归一保总转角）。<br/>
    /// 保真原版：半程帧松键提前收棍（reuseDelay=10）、半程帧可随鼠标换向（rotation-π 翻杆）、
    /// 尾程命中或杆尖探地触发 3 倍砸击。杆线判定 中心±40px，复击 12 帧
    /// </summary>
    internal class GsMonkStaffT1Held : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT1");

        /// <summary>基准抡棍总帧（除以攻速），同原版 50</summary>
        private const int BaseDur = 50;
        /// <summary>总转角：整 2 圈，同原版</summary>
        private const float TotalAngle = MathHelper.TwoPi * 2f;
        /// <summary>杆线半长（px），判定与绘制共用</summary>
        private const float PoleHalf = 40f;

        private int spinDur = BaseDur;
        private int halfFrame;
        private int slamFrame;
        /// <summary>尾段命中记 lateHit 的起始帧（原版 ai[0]>=42/50 = 84%）</summary>
        private int lateThresh;

        private int timer;
        private int dir = 1;
        private float rot;
        private float prevRot;
        /// <summary>章鱼头软体滞后角（两拍拖影的第一拍）</summary>
        private float rotLag;
        /// <summary>软体拖影第二拍，追第一拍</summary>
        private float rotLag2;
        /// <summary>当前角速度权重 0.5~1（涂抹亮度、体态用）</summary>
        private float speedFrac = 0.5f;
        /// <summary>尾程命中标记（owner 端 OnHitNPC 写，owner 端砸地判定读，同原版 localAI[1]）</summary>
        private bool lateHit;
        private bool slamChecked;
        /// <summary>砸地帧顿计时，冻结 timer 与转角</summary>
        private int freezeTimer;
        private float bodyLean;
        private bool bodyLeanApplied;
        private readonly HashSet<int> hitNPCs = [];

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 RotVec => rot.ToRotationVector2();
        /// <summary>章鱼头端（+rot 方向端），砸地探测与粒子锚点</summary>
        private Vector2 TipPos => Projectile.Center + (RotVec * PoleHalf);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12; //原版复击节奏
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 150;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>朝向镜像角：dir=1 用原角，dir=-1 关于竖直轴镜像</summary>
        private static float MirrorAngle(float angle, int direction)
            => direction == 1 ? angle : MathHelper.Pi - angle;

        private void InitSpin() {
            dir = MathF.Abs(Projectile.velocity.X) < 0.001f
                ? Owner.direction : Math.Sign(Projectile.velocity.X);
            //方向寄存在 velocity（±1,0），换向时 netUpdate 随包过线，远端在 AI 里侦测翻杆
            Projectile.velocity = new Vector2(dir, 0f);

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            spinDur = Math.Max(20, (int)MathF.Round(BaseDur / speed));
            halfFrame = spinDur / 2;
            slamFrame = spinDur - 3;          //原版 num-3 帧做砸地判定
            lateThresh = (int)(spinDur * 0.84f);

            //起角取「杆头前下方压杆」姿态：整 2 圈后收尾时杆头正落在身前下方，砸地探测朝地
            rot = rotLag = rotLag2 = MirrorAngle(1.44f, dir);
            prevRot = rot;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.9f }, Owner.Center);
            }
        }

        public override void AI() {
            if (Item.type != ItemID.MonkStaffT1 || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            if (timer == 0 && Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                InitSpin();
            }

            //远端换向侦测：owner 半程翻向后 velocity 随 netUpdate 过线，各端一致翻杆（同原版 rotation-π）
            int dirNow = Projectile.velocity.X >= 0f ? 1 : -1;
            if (dirNow != dir) {
                dir = dirNow;
                rot -= MathHelper.Pi;
            }

            //砸地帧顿：timer 与转角冻结，软体滞后照常回弹
            if (freezeTimer > 0) {
                freezeTimer--;
            }
            else if (!AdvanceSpin()) {
                return; //本帧内已 Kill
            }

            //章鱼头 secondary motion：滞后角以 0.25 追真实角，第二拍再追第一拍
            rotLag += MathHelper.WrapAngle(rot - rotLag) * 0.25f;
            rotLag2 += MathHelper.WrapAngle(rotLag - rotLag2) * 0.25f;

            UpdatePose();
            HandleParticles();

            float tail = timer / (float)spinDur;
            Lighting.AddLight(TipPos, GsMonkStaffT1.OctoGreen.ToVector3() * (tail >= 0.75f ? 0.35f : 0.12f));
        }

        /// <summary>推进一帧转角与时间线；返回 false 表示本帧已 Kill</summary>
        private bool AdvanceSpin() {
            timer++;
            prevRot = rot;
            //加速度曲线：线性权重 0.7→1.4，中点采样积分恰为均值 1.05，总转角严格 2 圈
            float pMid = MathHelper.Clamp((timer - 0.5f) / spinDur, 0f, 1f);
            float w = MathHelper.Lerp(0.7f, 1.4f, pMid);
            speedFrac = w / 1.4f;
            rot += TotalAngle / spinDur * (w / 1.05f) * dir;

            //半程帧：松键提前收棍；仍按住则允许随鼠标换向（保真原版）
            if (timer == halfFrame) {
                if (!Owner.controlUseItem) {
                    EndSpin();
                    return false;
                }
                if (Projectile.owner == Main.myPlayer) {
                    int side = Main.MouseWorld.X > Owner.Center.X ? 1 : -1;
                    if (side != dir) {
                        dir = side;
                        Owner.ChangeDir(side);
                        Projectile.velocity = new Vector2(side, 0f);
                        rot -= MathHelper.Pi; //同原版翻杆，远端由 velocity 变化补做
                        Projectile.netUpdate = true;
                    }
                }
            }

            //收尾砸地判定（owner 端，同原版 num-3 帧）
            if (timer == slamFrame && !slamChecked) {
                slamChecked = true;
                if (Projectile.owner == Main.myPlayer) {
                    TrySlam();
                }
            }

            if (timer >= spinDur) {
                EndSpin();
                return false;
            }
            return true;
        }

        /// <summary>自然收尾与提前收棍共用：owner 补原版 reuseDelay=10 的收势僵直</summary>
        private void EndSpin() {
            if (Owner.whoAmI == Main.myPlayer) {
                Owner.reuseDelay = 10;
            }
            Projectile.Kill();
        }

        /// <summary>砸地：尾程命中过敌（lateHit）或杆尖向下 4 格内有实心物块 → 3 倍砸击弹（保真原版）</summary>
        private void TrySlam() {
            Vector2 tip = TipPos;
            bool grounded = false;
            Point tile = tip.ToTileCoordinates();
            for (int j = 0; j <= 4 && !grounded; j++) {
                grounded = WorldGen.InWorld(tile.X, tile.Y + j) && WorldGen.SolidTile(tile.X, tile.Y + j);
            }
            if (!lateHit && !grounded) {
                //空挥收尾，原版落空音
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundMiss with { Volume = 0.8f }, Projectile.Center);
                return;
            }

            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item),
                tip + new Vector2(dir * 20f, -60f), Vector2.Zero,
                ModContent.ProjectileType<GsMonkStaffT1SlamProj>(),
                Projectile.damage * 3, Projectile.knockBack, Owner.whoAmI);
            freezeTimer = 2; //砸地帧顿

            //杆尖土屑（owner 客户端路径，非服务器）
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(tip, DustID.Dirt,
                        new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(1.5f, 4f)),
                        40, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = false;
                }
            }
        }

        /// <summary>持械姿态：手臂随杆转，体态随角速度前倾</summary>
        private void UpdatePose() {
            Owner.ChangeDir(dir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (RotVec * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot - MathHelper.PiOver2);

            //杆心随行程微微向头端漂（原版 vector2 漂移语言的收敛版）
            float p = timer / (float)spinDur;
            Projectile.Center = Hand + (RotVec * (p * 10f));
            Projectile.rotation = rot;

            float target = freezeTimer > 0 ? bodyLean : dir * 0.05f * speedFrac;
            if (timer >= spinDur - 2) {
                target = 0f;
            }
            bodyLean = MathHelper.Lerp(bodyLean, target, 0.3f);
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

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        /// <summary>粒子保真原版：杆尖烟尘常驻，末四分之一程绿光尘，收尾前爆一轮绿光</summary>
        private void HandleParticles() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 tip = TipPos;
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(tip, DustID.Smoke,
                    Projectile.DirectionTo(tip) * 0.4f, 150, default, 1f);
                d.velocity += Owner.velocity * 0.1f;
            }
            float p = timer / (float)spinDur;
            if (p >= 0.75f) {
                Dust d = Dust.NewDustPerfect(tip, DustID.Pixie, Vector2.Zero,
                    50, new Color(20, 255, 100, 160), 1f);
                d.noGravity = true;
                d.velocity = Projectile.DirectionTo(tip) * 0.6f;
            }
            if (timer >= spinDur - 8 && timer < spinDur - 2) {
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(tip, DustID.Pixie,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.4f),
                        50, new Color(20, 255, 100, 160), 1.1f);
                    d.noGravity = true;
                }
            }
        }

        /// <summary>贪婪判定：本帧扫过的角度区间逐段采样杆线（中心±40px）；翻杆瞬间只判当前姿态</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(6, 6);
            Vector2 center = Projectile.Center;
            float delta = MathHelper.WrapAngle(rot - prevRot);
            int steps = MathF.Abs(delta) > 1.5f ? 0 : Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * PoleHalf / 16f), 1, 6);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = steps == 0 ? rot : MathHelper.Lerp(prevRot, rot, i / (float)steps);
                Vector2 half = ang.ToRotationVector2() * PoleHalf;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(),
                    center - half, center + half, 34f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            //同原版 40 半径线切
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 half = RotVec * PoleHalf;
            Utils.PlotTileLine(Projectile.Center - half, Projectile.Center + half, 40f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = dir; //击退跟抡向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次抡棍对同一目标只转发一次外部命中钩子（喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }
            //尾程命中记 lateHit：收尾必触发砸击（保真原版 ai[0]>=42 → localAI[1]=1）
            if (timer >= lateThresh) {
                lateHit = true;
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                    Color c = Main.rand.NextBool(3) ? GsMonkStaffT1.SleepPurple : GsMonkStaffT1.OctoGreen;
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.32f, 0.5f))
                        ?.Configure(true, Main.rand.Next(10, 16));
                }
                Dust smoke = Dust.NewDustPerfect(target.Center, DustID.Smoke,
                    Main.rand.NextVector2Unit() * 1.5f, 140, default, 1.1f);
                smoke.noGravity = true;
            }
        }

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        //==================== 绘制：圆周涂抹 + 软体拖影 + 原版物品贴图杆体 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            DrawSmearRing(sb);
            DrawStaffSet(sb, lightColor);
            return false;
        }

        /// <summary>圆周涂抹：双层弧形涂抹随杆角走（加色 A=0），亮度∝当前角速度，末圈最亮</summary>
        private void DrawSmearRing(SpriteBatch sb) {
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null || freezeTimer > 0) {
                return;
            }
            float alpha = 0.08f + (0.30f * speedFrac * speedFrac);
            Vector2 at = Projectile.Center - Main.screenPosition;
            float smearRot = rot - (dir * 0.8f);
            Vector2 scale = new Vector2(0.40f, 0.30f) * (PoleHalf / 40f);
            Color outer = GsMonkStaffT1.OctoGreen * alpha;
            outer.A = 0;
            sb.Draw(wave, at, null, outer, smearRot, wave.Size() / 2f, scale, SpriteEffects.None, 0f);
            Color inner = GsMonkStaffT1.SleepPurple * (alpha * 0.6f);
            inner.A = 0;
            sb.Draw(wave, at, null, inner, smearRot + (dir * 0.35f), wave.Size() / 2f, scale * 0.8f, SpriteEffects.None, 0f);
        }

        /// <summary>杆体：软体拖影两拍 + 暗影垫底 + 本体 + 末段头端绿辉</summary>
        private void DrawStaffSet(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(ItemID.MonkStaffT1);
            Texture2D tex = TextureAssets.Item[ItemID.MonkStaffT1].Value;
            //物品贴图沿对角指向章鱼头，origin 取杆握把端（左下角内缩）
            Vector2 origin = new(8f, tex.Height - 8f);
            float diag = new Vector2(tex.Width, tex.Height).Length();
            float scale = ((PoleHalf * 2f) + 14f) / MathF.Max(diag - 16f, 1f);
            Vector2 center = Projectile.Center;

            //章鱼头软体拖影：头端区域裁切太复杂，改为整贴图低透明滞后两拍（第二拍更淡），注释说明
            Span<(float ang, float alpha)> ghosts = [(rotLag2, 0.14f), (rotLag, 0.30f)];
            foreach ((float ang, float alpha) in ghosts) {
                if (MathF.Abs(MathHelper.WrapAngle(ang - rot)) < 0.05f) {
                    continue;
                }
                Vector2 gPos = center - (ang.ToRotationVector2() * PoleHalf) - Main.screenPosition;
                sb.Draw(tex, gPos, null, lightColor * alpha, ang + MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
            }

            Vector2 gripPos = center - (RotVec * PoleHalf) - Main.screenPosition;

            //硬木暗影垫底
            Color shadow = new Color(14, 12, 18, 190) * 0.45f;
            sb.Draw(tex, gripPos + new Vector2(dir, 2f), null, shadow, rot + MathHelper.PiOver4, origin, scale * 1.02f, SpriteEffects.None, 0f);

            sb.Draw(tex, gripPos, null, lightColor, rot + MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);

            //末段绿光：贴图加色叠影 + 头端软光点（保真原版绿光语言）
            float p = timer / (float)spinDur;
            if (p >= 0.72f) {
                float glowT = MathHelper.Clamp((p - 0.72f) / 0.28f, 0f, 1f);
                Color glow = GsMonkStaffT1.OctoGreen * (0.28f * glowT);
                glow.A = 0;
                sb.Draw(tex, gripPos, null, glow, rot + MathHelper.PiOver4, origin, scale * 1.03f, SpriteEffects.None, 0f);
                Texture2D soft = CWRAsset.SoftGlow?.Value;
                if (soft != null) {
                    float flick = 0.8f + (0.2f * MathF.Sin((Main.GlobalTimeWrappedHourly * 11f) + (DrawRand01(5) * 6.28f)));
                    Color tipGlow = GsMonkStaffT1.OctoGreen * (0.4f * glowT * flick);
                    tipGlow.A = 0;
                    sb.Draw(soft, TipPos - Main.screenPosition, null, tipGlow, 0f, soft.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
                }
            }
        }
    }

    /// <summary>
    /// 睡意砸击：贴地 130×90 一击 AoE（伤害 = 杆伤 ×3，保真原版），击退纯上抬。
    /// 签名「连砸催眠」：命中挂缓速 120 帧并在方案记账；同目标 8 秒内第 3 砸
    /// 1.3 倍伤害+缓速 240+头顶淡紫 Z 泡演出。自绘：压扁冲击环双层+慢半拍淡紫涟漪+土屑
    /// </summary>
    internal class GsMonkStaffT1SlamProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT1");

        private const int Life = 12;
        /// <summary>伤害窗：前 6 帧</summary>
        private const int HitWindow = 6;

        private float LifeT => 1f - (Projectile.timeLeft / (float)Life);

        public override void SetDefaults() {
            Projectile.width = 130;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一记砸击对同一目标只命中一次
            Projectile.timeLeft = Life;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    //砸地音随弹幕各端自播，旁观者也听得见
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f }, Projectile.Center);
                    for (int i = 0; i < 12; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-50f, 50f), -4f),
                            DustID.Dirt, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1.5f, 5f)),
                            30, default, Main.rand.NextFloat(0.9f, 1.5f));
                        d.noGravity = false; //土屑带重力
                    }
                }
            }
            Lighting.AddLight(Projectile.Bottom, GsMonkStaffT1.SleepPurple.ToVector3() * (0.4f * (1f - LifeT)));
        }

        public override bool? CanDamage() => Projectile.timeLeft > Life - HitWindow ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退向上：方向置 0 只留原版击退的竖直上抬分量
            modifiers.HitDirectionOverride = 0;
            //第 3 砸读账加伤（记账在 OnHitNPC，本钩子先行判读）
            if (GodSmithScheme.TryGetScheme(ItemID.MonkStaffT1, out GodSmithScheme scheme)
                && scheme is GsMonkStaffT1 t1 && t1.IsThirdSlam(target.whoAmI)) {
                modifiers.FinalDamage *= 1.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool third = false;
            //连砸记账：OnHitNPC 只跑攻击方端，方案单例字段天然只被本地玩家写
            if (GodSmithScheme.TryGetScheme(ItemID.MonkStaffT1, out GodSmithScheme scheme)
                && scheme is GsMonkStaffT1 t1) {
                third = t1.RegisterSlam(target.whoAmI);
            }
            //催眠缓速：AddBuff 自带跨端同步
            target.AddBuff(BuffID.Slow, third ? 240 : 120);

            if (third && !VaultUtils.isServer) {
                //熟睡演出：淡紫 Z 泡三连逐个上浮（小→大、慢→快）
                for (int i = 0; i < 3; i++) {
                    Vector2 at = new(target.Center.X + ((i - 1) * 7f), target.position.Y - 8f - (i * 5f));
                    PRTLoader.NewParticle<PRT_Spark>(at, new Vector2((i - 1) * 0.18f, -0.7f - (i * 0.45f)),
                        GsMonkStaffT1.SleepPurple, 0.32f + (i * 0.1f))?.Configure(false, 24 + (i * 7));
                }
                PRTLoader.NewParticle<PRT_Light>(target.Top, Vector2.Zero, GsMonkStaffT1.SleepPurple, 0.15f)
                    ?.Configure(12, 0.7f);
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种，绘制禁 Main.rand）</summary>
        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>地面压扁冲击环双层扩散渐灭 + 慢半拍淡紫睡意涟漪，加色批全 A=0</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float t = LifeT;
            Vector2 at = Projectile.Bottom - Main.screenPosition - new Vector2(0f, 8f);
            Vector2 texOrigin = glow.Size() / 2f;
            //压扁横拉基准比例 (1.6, 0.5)
            Vector2 aspect = new(1.6f, 0.5f);

            float fade = MathF.Pow(1f - t, 1.3f);
            float expand = 0.5f + (t * 1.35f);
            Color bright = GsMonkStaffT1.NapBright * (0.62f * fade);
            bright.A = 0;
            Main.EntitySpriteDraw(glow, at, null, bright, 0f, texOrigin, aspect * expand * 0.72f, SpriteEffects.None, 0);
            Color mid = GsMonkStaffT1.OctoGreen * (0.4f * fade);
            mid.A = 0;
            Main.EntitySpriteDraw(glow, at, null, mid, 0f, texOrigin, aspect * expand, SpriteEffects.None, 0);

            //淡紫睡意涟漪：慢半拍出场，扩得更宽更慢灭
            float t2 = MathHelper.Clamp((t - 0.28f) / 0.72f, 0f, 1f);
            if (t2 > 0f) {
                float wobble = 1f + (0.05f * MathF.Sin((Main.GlobalTimeWrappedHourly * 9f) + (DrawRand01(2) * 6.28f)));
                Color purple = GsMonkStaffT1.SleepPurple * (0.36f * (1f - t2));
                purple.A = 0;
                Main.EntitySpriteDraw(glow, at, null, purple, 0f, texOrigin,
                    aspect * (0.5f + (t2 * 1.75f)) * wobble, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
