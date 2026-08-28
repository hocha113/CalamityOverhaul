using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【昼星终曲】材质：白金星芒淬锋的星光刺剑，淡青彗尾随刺成线。
    /// 签名：①挥砍换成突刺运动语言：一次使用=一段三连刺（疾出过冲、刺尖驻帧、缓回收），
    /// 三刺各有伤害窗（原版连刺手感保留升级：刺尖星屑爆点、刺路留星光线）
    /// ②第三刺射出贯穿星光弹（星核+彗尾+闪烁星屑，减速滑行消散）
    /// ③连续三段三刺全中，星光弹升格星暴，命中炸开星屑环
    /// </summary>
    internal class GsPiercingStarlight : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PiercingStarlight;

        protected override int HeldProjID => ModContent.ProjectileType<GsPiercingStarlightHeld>();

        /// <summary>一次使用就是完整一段三连刺</summary>
        protected override int ComboBeats => 1;

        protected override string GsDescFallback =>
            "Reforged: each use is a three-thrust starlight cadence; the final thrust " +
            "fires a piercing star bolt, and three faultless cadences in a row make " +
            "the bolt burst into a starburst on hit";

        //星光色板
        internal static readonly Color StarBright = new(240, 248, 255); //白金星芒
        internal static readonly Color StarMain = new(150, 214, 255);   //淡青彗尾
        internal static readonly Color StarHot = new(255, 244, 198);    //昼星暖芯
        internal static readonly Color StarDeep = new(16, 20, 34);      //夜幕垫影

        /// <summary>连续全中段数（0~3，满 3 段星光弹升格星暴）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Cadence;

        //底伤不加成（原版快刺 DPS 已高）：三刺 0.88/0.88/1.02（第三刺经 ModifyHitExtra 补 1.16x）
        //+ 星光弹 0.50x + 星暴 0.30x（三段全中才有），单段合计 3.28~3.58x，
        //不超原版一个 useAnimation 周期（3 刺 x1.0 = 3.0x）的 120% 上限；
        //段长约 20 帧对原版 18 帧，基线 DPS 约 98%，穿透 3 与星暴 AoE 把综合抬进 100%~110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 昼星终曲手持：整体重写 UpdateBladeTransform 换成突刺运动语言。
    /// 时间线：蓄势后引（Raise）→ 三连刺（Slash 均分三份：疾出 35% / 驻帧 20% / 缓回 45%）
    /// → 收势（Recover）。突刺角度基本不变，基类扫角判定退化，
    /// CanDamage/Colliding/CutTiles 全部重接成刺线；localNPCHitCooldown 改短让三刺各中一次。
    /// ai[0]=拍号（恒 0）ai[1]=交替符号（三刺扇形偏角次序）
    /// </summary>
    internal class GsPiercingStarlightHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PiercingStarlight;
        protected override int BeatCount => 1;
        protected override Color EdgeBright => GsPiercingStarlight.StarBright;
        protected override Color BodyMain => GsPiercingStarlight.StarMain;
        protected override Color HotAccent => GsPiercingStarlight.StarHot;
        protected override Color DeepShadow => GsPiercingStarlight.StarDeep;

        //刺剑：窄刺线判定，贴身兜底小
        protected override float BaseReach => 122f;
        protected override float CollisionWidth => 26f;
        protected override float PointBlankRadius => 36f;

        //星光常辉；角度残影关掉（突刺残影走 DrawExtra 的行程残像）
        protected override bool GlowAlways => true;
        protected override int GhostCount => 0;
        //星光是魔质剑，血肉命中不补血尘
        protected override bool BleedOnFlesh => false;

        /// <summary>三刺完整命中记录</summary>
        private readonly bool[] thrustLanded = new bool[3];
        /// <summary>各刺出手事件已放</summary>
        private readonly bool[] thrustCalled = new bool[3];
        /// <summary>行程残像环形缓存（X=reach，Y=angle）</summary>
        private readonly Vector2[] history = new Vector2[4];
        private int historyCount;

        private int thrustIndex = -1;   //当前刺序（-1=未进入连刺）
        private float subPhase;         //当前刺内进度 0~1
        private bool damageActive;      //本帧伤害窗
        private float laneGlow;         //刺路星光线亮度
        private float laneReach;        //本刺最远行程
        private bool boltFired;
        private bool tipBurstDone;      //本刺刺尖爆点已放

        private GsPiercingStarlight Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsPiercingStarlight : null;

        protected override GsBroadBeat GetBeat(int stage) => new() {
            //Slash=13 均分三刺；Raise 短蓄、Recover 短收，整段约 20 帧（除以攻速）
            Raise = 2, Hold = 1, Slash = 13, Recover = 4,
            RaiseBack = 0.3f, Follow = 0.1f, ReachScale = 1f, LeanAmp = 0.03f,
            DamageMult = 0.88f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.3f,
        };

        protected override void SetSwordDefaults() {
            //短冷却：三刺可对同一目标各结算一次（刺间隔约 4~5 帧，单刺伤害窗约 2 帧不致重复吃）
            Projectile.localNPCHitCooldown = 3;
        }

        /// <summary>三刺微扇形偏角：先低后高再正中（次序符号随 ai[1] 交替）</summary>
        private float FanOffset(int idx) => idx switch {
            0 => -0.055f * swingDir,
            1 => 0.048f * swingDir,
            _ => 0f,
        };

        /// <summary>突刺运动语言：角度锁死出手向（带微扇偏角），行程做出-驻-回三段</summary>
        protected override void UpdateBladeTransform(int phase) {
            float reach01;
            switch (phase) {
                case PhaseRaise: {
                    //蓄势后引：刃往手边收
                    float p = timer / (float)raiseDur;
                    reach01 = MathHelper.Lerp(0.62f, 0.40f, EaseOutQuad(p));
                    mainAngle = baseAngle + FanOffset(0);
                    thrustIndex = -1;
                    subPhase = 0f;
                    damageActive = false;
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    //瞄定一帧
                    reach01 = 0.40f;
                    mainAngle = baseAngle + FanOffset(0);
                    thrustIndex = -1;
                    subPhase = 0f;
                    damageActive = false;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float s = (timer - raiseDur - holdDur) / (float)slashDur; //(0,1]
                    slashProgress = s;
                    int idx = Math.Min(2, (int)(s * 3f - 0.0001f));
                    float pThird = MathHelper.Clamp(s * 3f - idx, 0f, 1f);
                    if (idx != thrustIndex) {
                        //新刺开跑：行程记录清零
                        thrustIndex = idx;
                        tipBurstDone = false;
                        laneReach = 0f;
                    }
                    subPhase = pThird;
                    mainAngle = baseAngle + FanOffset(idx);
                    float deep = idx == 2 ? 1.10f : 1f; //终刺贯得更深
                    if (pThird < 0.35f) {
                        //疾出：1~2 帧带 6% 过冲
                        float q = pThird / 0.35f;
                        reach01 = MathHelper.Lerp(0.42f, 1.06f * deep, 1f - MathF.Pow(1f - q, 3f));
                        damageActive = true;
                    }
                    else if (pThird < 0.55f) {
                        //刺尖驻帧：过冲回坐
                        float q = (pThird - 0.35f) / 0.2f;
                        reach01 = MathHelper.Lerp(1.06f, 1f, SmoothStep01(q)) * deep;
                        damageActive = true;
                    }
                    else {
                        //缓回收
                        float q = (pThird - 0.55f) / 0.45f;
                        reach01 = MathHelper.Lerp(deep, 0.45f, SmoothStep01(q));
                        damageActive = false;
                    }
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    reach01 = MathHelper.Lerp(0.45f, 0.60f, EaseOutQuad(q));
                    mainAngle = baseAngle;
                    damageActive = false;
                    slashProgress = 1f;
                    fanFade = MathHelper.Clamp(1f - q * 1.2f, 0f, 1f);
                    break;
                }
            }
            mainReach = FullReach * reach01;
            laneReach = MathF.Max(laneReach, mainReach);
            laneGlow *= 0.86f;
            if (damageActive) {
                laneGlow = 1f;
            }
            //行程残像入环（只在连刺期记录）
            if (CurrentPhase == PhaseSlash) {
                for (int i = history.Length - 1; i > 0; i--) {
                    history[i] = history[i - 1];
                }
                history[0] = new Vector2(mainReach, mainAngle);
                historyCount = Math.Min(historyCount + 1, history.Length);
            }
            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>自管事件编排：每刺出手音+刃闪、终刺前压与星光弹（不走基类单次斩切事件）</summary>
        protected override void HandlePhaseEvents(int phase) {
            if (phase != PhaseSlash || thrustIndex < 0) {
                return;
            }
            int idx = thrustIndex;
            //每刺出手瞬间：星芒短哨 + 刃闪；终刺补一步前压
            if (!thrustCalled[idx]) {
                thrustCalled[idx] = true;
                SetFlash(4);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.30f + idx * 0.07f }, Owner.Center);
                }
                if (idx == 2 && Owner.whoAmI == Main.myPlayer && !Owner.mount.Active) {
                    Owner.velocity.X += facingDir * 2.2f;
                }
            }
            //终刺驻帧瞬间射出星光弹（方案段数只在 owner 读）
            if (idx == 2 && !boltFired && subPhase >= 0.35f) {
                boltFired = true;
                if (Projectile.owner == Main.myPlayer) {
                    GsPiercingStarlight scheme = Scheme;
                    float burst = scheme != null && scheme.Cadence >= 3 ? 1f : 0f;
                    int boltDamage = Math.Max(1, (int)(Projectile.damage * 0.57f));
                    SpawnOwnedProj(ModContent.ProjectileType<GsPiercingStarlightBoltProj>(),
                        mainTip, baseAngle.ToRotationVector2() * 16f, boltDamage, Projectile.knockBack * 0.5f, burst);
                }
            }
        }

        public override bool? CanDamage() => damageActive ? null : false;

        /// <summary>刺线判定：手→刺尖线段 + 贴身兜底</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!damageActive) {
                return false;
            }
            Rectangle box = targetHitbox;
            box.Inflate(6, 6);
            Vector2 hand = Hand;
            if (box.Distance(hand) <= PointBlankRadius) {
                return true;
            }
            float cp = 0f;
            Vector2 tip = hand + mainAngle.ToRotationVector2() * (mainReach * 1.05f + 8f);
            return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(), hand, tip, CollisionWidth, ref cp);
        }

        public override void CutTiles() {
            if (!damageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Hand, mainTip, CollisionWidth * 0.85f, DelegateMethods.CutTiles);
        }

        /// <summary>终刺贯劲：0.88 拍基线补至约 1.02x</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (thrustIndex == 2) {
                modifiers.SourceDamage *= 1.16f;
            }
        }

        /// <summary>记录当前刺命中（供整段全中判定）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (thrustIndex >= 0) {
                thrustLanded[thrustIndex] = true;
            }
        }

        /// <summary>星屑迸溅 + 清脆星音</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.3f, Pitch = 0.5f }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.2f),
                    Main.rand.NextBool() ? GsPiercingStarlight.StarBright : GsPiercingStarlight.StarMain,
                    Main.rand.NextFloat(0.06f, 0.11f))?.Configure(11, 0.7f);
            }
        }

        /// <summary>段末记账：三刺窗口走完且三刺全中 → 段数 +1，否则清零（守 myPlayer）</summary>
        protected override void OnKillEffects() {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsPiercingStarlight scheme = Scheme;
            if (scheme == null) {
                return;
            }
            bool completed = timer > raiseDur + holdDur + slashDur;
            if (completed && thrustLanded[0] && thrustLanded[1] && thrustLanded[2]) {
                int old = scheme.Cadence;
                scheme.Cadence = Math.Min(3, scheme.Cadence + 1);
                if (old < 3 && scheme.Cadence == 3) {
                    //满段提示：一声上扬星哨
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = 0.5f }, Owner.Center);
                }
            }
            else {
                scheme.Cadence = 0;
            }
        }

        /// <summary>刺尖星屑爆点（驻帧首帧）+ 刺路星尘拂落</summary>
        protected override void HandleParticles(int phase) {
            if (phase != PhaseSlash) {
                return;
            }
            if (subPhase >= 0.35f && subPhase < 0.55f && !tipBurstDone) {
                tipBurstDone = true;
                int count = thrustIndex == 2 ? 7 : 4;
                for (int i = 0; i < count; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(mainTip,
                        (baseAngle + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2() * Main.rand.NextFloat(2.5f, 6.5f),
                        Main.rand.NextBool(3) ? GsPiercingStarlight.StarHot : GsPiercingStarlight.StarBright,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 16));
                }
            }
            else if (Main.rand.NextBool(3)) {
                //刺路星尘
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                PRTLoader.NewParticle<PRT_Light>(at, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f),
                    GsPiercingStarlight.StarMain, Main.rand.NextFloat(0.04f, 0.07f))?.Configure(8, 0.55f);
            }
        }

        /// <summary>角度涂抹不适用于突刺：改画刺路星光线（沿刺路拉伸的双层软光）</summary>
        protected override void DrawSmearArc(SpriteBatch sb) {
            if (laneGlow <= 0.03f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Vector2 dir = mainAngle.ToRotationVector2();
            float len = laneReach * 1.02f;
            Vector2 mid = Hand + dir * (len * 0.5f) - Main.screenPosition;
            Color haze = GsPiercingStarlight.StarMain * (0.28f * laneGlow);
            haze.A = 0;
            sb.Draw(glow, mid, null, haze, mainAngle, glow.Size() * 0.5f,
                new Vector2(len / glow.Width * 1.1f, 0.16f), SpriteEffects.None, 0f);
            Color core = GsPiercingStarlight.StarBright * (0.45f * laneGlow);
            core.A = 0;
            sb.Draw(glow, mid, null, core, mainAngle, glow.Size() * 0.5f,
                new Vector2(len / glow.Width, 0.06f), SpriteEffects.None, 0f);
        }

        /// <summary>行程残像 + 驻帧刺尖星芒 + 段数刻星（段数只画给 owner）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            //行程残像：贴图按旧行程重画，读作速度拉痕
            if (CurrentPhase == PhaseSlash && historyCount > 1) {
                Main.instance.LoadItem(SwordItemID);
                Texture2D tex = TextureAssets.Item[SwordItemID].Value;
                Vector2 origin = tex.Size() / 2f;
                GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
                Vector2 hand = Hand;
                for (int i = 1; i < historyCount; i++) {
                    float reach = history[i].X;
                    float ang = history[i].Y;
                    float scale = reach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
                    Color ghost = GsPiercingStarlight.StarBright * (i switch { 1 => 0.26f, 2 => 0.14f, _ => 0.07f });
                    ghost.A = 0;
                    Vector2 at = hand + ang.ToRotationVector2() * (reach * BladePark) - Main.screenPosition;
                    sb.Draw(tex, at, null, ghost, ang + rotOffset, origin, scale, effect, 0f);
                }
            }
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D starGlow = CWRAsset.StarGlow01?.Value;
            if (star == null || starGlow == null) {
                return;
            }
            //驻帧刺尖星芒
            if (CurrentPhase == PhaseSlash && subPhase >= 0.32f && subPhase < 0.6f) {
                float k = MathHelper.Clamp(1f - MathF.Abs(subPhase - 0.45f) / 0.14f, 0f, 1f);
                Vector2 tipAt = mainTip - Main.screenPosition;
                Color flash = GsPiercingStarlight.StarBright * (0.7f * k);
                flash.A = 0;
                sb.Draw(star, tipAt, null, flash, DrawRand01(thrustIndex) * 6.28f + Main.GlobalTimeWrappedHourly * 1.5f,
                    star.Size() * 0.5f, (thrustIndex == 2 ? 0.22f : 0.15f) + 0.08f * k, SpriteEffects.None, 0f);
                Color halo = GsPiercingStarlight.StarHot * (0.4f * k);
                halo.A = 0;
                sb.Draw(starGlow, tipAt, null, halo, 0f, starGlow.Size() * 0.5f, 0.3f + 0.12f * k, SpriteEffects.None, 0f);
            }
            //段数刻星：owner 侧近手处的小星
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsPiercingStarlight scheme = Scheme;
            int cadence = scheme?.Cadence ?? 0;
            if (cadence <= 0 || fanFade <= 0.05f) {
                return;
            }
            Vector2 hand2 = Hand;
            for (int i = 0; i < cadence; i++) {
                Vector2 at = hand2 + mainAngle.ToRotationVector2() * (mainReach * (0.2f + 0.1f * i)) - Main.screenPosition;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + i * 1.5f);
                Color c = (cadence >= 3 ? GsPiercingStarlight.StarHot : GsPiercingStarlight.StarBright) * (0.5f * fanFade * pulse);
                c.A = 0;
                sb.Draw(starGlow, at, null, c, 0f, starGlow.Size() * 0.5f, 0.10f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 贯穿星光弹：星核+彗尾+闪烁星屑，出膛 16 减速滑行至约 5 后消散，穿透 3。
    /// ai[0]=星暴旗（三段全中的升格：首次命中炸开星屑环）
    /// </summary>
    internal class GsPiercingStarlightBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool Burst => Projectile.ai[0] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];
        /// <summary>星暴已放（owner 端权威，生成走同步包）</summary>
        private ref float BurstDone => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 42;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }
            //减速滑行：16 → 约 5，昼星滑向消散
            if (Projectile.velocity.Length() > 5f) {
                Projectile.velocity *= 0.955f;
            }
            Projectile.rotation += 0.22f * (Projectile.velocity.X >= 0f ? 1f : -1f);
            Lighting.AddLight(Projectile.Center, GsPiercingStarlight.StarMain.ToVector3() * 0.45f);
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //彗尾星屑
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.06f,
                    Main.rand.NextBool() ? GsPiercingStarlight.StarBright : GsPiercingStarlight.StarMain,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(10, 0.65f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //星暴升格：首次命中炸开星屑环（owner 端生成，随生成包同步）
            if (Burst && BurstDone == 0f) {
                BurstDone = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    int ringDamage = Math.Max(1, (int)(Projectile.damage * 0.6f));
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsPiercingStarlightBurstProj>(), ringDamage,
                        Projectile.knockBack, Projectile.owner);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.4f }, target.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool(3) ? GsPiercingStarlight.StarHot : GsPiercingStarlight.StarBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //昼星散场：星尘四逸
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.3f, 1f),
                    GsPiercingStarlight.StarMain, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(12, 0.6f);
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (star == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            float speed01 = MathHelper.Clamp(Projectile.velocity.Length() / 16f, 0f, 1f);

            //彗尾：旧位置串软光，近粗远细，隔节缀闪烁星屑（确定性相位）
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color tail = GsPiercingStarlight.StarMain * (0.24f * k * fade);
                tail.A = 0;
                Main.EntitySpriteDraw(glow, at, null, tail, 0f, glow.Size() * 0.5f, 0.22f * k + 0.05f, SpriteEffects.None, 0);
                if (i % 3 == 1) {
                    float tw = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + SegRand(i) * 6.28f);
                    Color dust = GsPiercingStarlight.StarBright * (0.4f * k * fade * tw);
                    dust.A = 0;
                    Main.EntitySpriteDraw(star, at, null, dust, SegRand(i + 20) * 6.28f, star.Size() * 0.5f,
                        0.06f + 0.04f * tw, SpriteEffects.None, 0);
                }
            }

            //星核：软光晕 + 速度拉伸青尾锥 + 白金星芒自旋 + 暖芯反旋
            Color haloC = GsPiercingStarlight.StarMain * (0.5f * fade);
            haloC.A = 0;
            Main.EntitySpriteDraw(glow, center, null, haloC, 0f, glow.Size() * 0.5f, 0.34f + 0.1f * speed01, SpriteEffects.None, 0);
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            Color cone = GsPiercingStarlight.StarMain * (0.35f * fade * speed01);
            cone.A = 0;
            Main.EntitySpriteDraw(glow, center + back * 10f, null, cone, Projectile.velocity.ToRotation(),
                glow.Size() * 0.5f, new Vector2(0.5f + 0.5f * speed01, 0.14f), SpriteEffects.None, 0);
            Color coreC = GsPiercingStarlight.StarBright * (0.85f * fade);
            coreC.A = 0;
            Main.EntitySpriteDraw(star, center, null, coreC, Projectile.rotation, star.Size() * 0.5f,
                (Burst ? 0.17f : 0.13f) + 0.02f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f), SpriteEffects.None, 0);
            Color warm = GsPiercingStarlight.StarHot * (0.45f * fade);
            warm.A = 0;
            Main.EntitySpriteDraw(star, center, null, warm, -Projectile.rotation * 0.7f, star.Size() * 0.5f,
                Burst ? 0.11f : 0.08f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 星暴：星光弹升格后的命中爆环。7 帧过冲撑满后回坐，伤害只在扩张期结算一次；
    /// 绘制全走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsPiercingStarlightBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 20;
        private const float MaxRadius = 88f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：7 帧过冲 5% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 7f, 0f, 1f);
                float burst = p < 0.7f ? 1.05f * (p / 0.7f) : MathHelper.Lerp(1.05f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.6f }, Projectile.Center);
                //爆心星屑喷环
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6.5f),
                        Main.rand.NextBool(3) ? GsPiercingStarlight.StarHot : GsPiercingStarlight.StarBright,
                        Main.rand.NextFloat(0.32f, 0.55f))?.Configure(false, Main.rand.Next(12, 20));
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.2f),
                        GsPiercingStarlight.StarMain, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(12, 0.7f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsPiercingStarlight.StarMain.ToVector3() * (0.8f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 8f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (star == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //爆心星芒：首帧最亮随后蚀散
            Color flash = GsPiercingStarlight.StarBright * (0.8f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(star, center, null, flash, SegRand(9) * 6.28f + Life * 0.06f,
                star.Size() * 0.5f, 0.34f, SpriteEffects.None, 0);

            //扩张星屑环：光珠沿当前半径排布，杂小星芒，相位确定性错开
            const int beads = 12;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.4f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + SegRand(i + 30) * 6.28f);
                Color bead = GsPiercingStarlight.StarMain * (0.5f * fade * pulse);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.22f + 0.08f * SegRand(i + 60), SpriteEffects.None, 0);
                if (i % 3 == 0) {
                    Color glint = GsPiercingStarlight.StarBright * (0.45f * fade * pulse);
                    glint.A = 0;
                    Main.EntitySpriteDraw(star, at, null, glint, SegRand(i + 90) * 6.28f, star.Size() * 0.5f,
                        0.07f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
