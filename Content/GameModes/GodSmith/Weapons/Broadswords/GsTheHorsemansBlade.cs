using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【无头骑士猎首魔剑】材质：狱火淬燃的南瓜骑士黑铁。
    /// 签名：①命中唤出烈焰南瓜头弧线追撞目标（原版召瓜保留并升级自绘）
    /// ②对同一目标叠猎首印（上限 4），印满后终结斩命中召四骑南瓜阵列队冲锋碾过目标线
    /// ③命中南瓜瓤迸溅+狱火星，印记以咧嘴鬼脸烙在目标头顶
    /// </summary>
    internal class GsTheHorsemansBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TheHorsemansBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsTheHorsemansBladeHeld>();

        protected override string GsDescFallback =>
            "Reforged: striking a foe summons a flaming jack-o'-lantern to run it down; " +
            "repeated strikes on one victim brand Headhunt marks, and at full marks " +
            "the finishing slash calls a cavalry of four burning pumpkins to trample the line";

        //狱火南瓜色板
        internal static readonly Color JackBright = new(255, 208, 128); //灼橙刃缘
        internal static readonly Color JackMain = new(255, 128, 34);    //南瓜狱火橙
        internal static readonly Color JackHot = new(255, 70, 16);      //炽核红橙
        internal static readonly Color JackDeep = new(24, 11, 9);       //焦黑骑士铁

        /// <summary>猎首印上限</summary>
        internal const int HuntMarksMax = 4;
        /// <summary>猎首印层数；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int HuntMarks;
        /// <summary>当前被烙印的目标 whoAmI（-1 无）</summary>
        internal int HuntTargetWhoAmI = -1;

        //底伤不加成：拍伤 1.0/1.0/1.3 + 南瓜头每斩首个命中 0.7x + 印满(4)骑阵 4×0.55x 约每两循环一次
        //循环 79 帧（26+26+27）单体口径 (3.3+2.1+1.1)=6.5 单位 vs 原版全套(挥1.0+南瓜1.0)/26 帧同窗 6.08 → 综合约 107%
        //南瓜自寻与骑阵穿透对群是 AoE 收益；未攒印的开局地板约 89%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 猎首魔剑手持：三拍连击。0/1 交替斩，2 猎首重劈（长举燃焰+前压+重顿帧）。
    /// 每斩首个命中放出追撞南瓜头；印满后的终结拍命中引出南瓜骑阵。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTheHorsemansBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TheHorsemansBlade;
        protected override Color EdgeBright => GsTheHorsemansBlade.JackBright;
        protected override Color BodyMain => GsTheHorsemansBlade.JackMain;
        protected override Color HotAccent => GsTheHorsemansBlade.JackHot;
        protected override Color DeepShadow => GsTheHorsemansBlade.JackDeep;

        //骑士黑铁吸光；狱火橙常年渗刃
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsTheHorsemansBlade.JackDeep, 0.22f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsTheHorsemansBlade.JackHot : GsTheHorsemansBlade.JackMain;
        //南瓜瓤代血，不补血尘
        protected override bool BleedOnFlesh => false;

        private bool jackSpawned;
        private bool cavalryFired;

        private GsTheHorsemansBlade Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsTheHorsemansBlade : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.08f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.15f,
            },
            //拍2 猎首重劈：长举燃焰、前压、重顿帧
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.25f, Follow = 1.3f, ReachScale = 1.16f, LeanAmp = 0.09f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.2f, SwingPitch = -0.3f,
            },
        };

        //==================== 猎首演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //重劈：狱火轰腔 + 厚响垫底
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.55f, Pitch = -0.3f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.45f }, Owner.Center);
            }
        }

        protected override void OnSlashBegin() {
            if (IsFinisher) {
                SetFlash(7);
            }
        }

        /// <summary>命中记账：首个命中放南瓜头；同目标叠猎首印；印满的终结拍引出骑阵</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            //南瓜头一斩只出一颗（除回拍伤取底伤摊账）
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            if (!jackSpawned) {
                jackSpawned = true;
                Vector2 from = Owner.Center + new Vector2(-facingDir * 26f, -42f);
                SpawnOwnedProj(ModContent.ProjectileType<GsTheHorsemansBladeJackProj>(),
                    from, new Vector2(-facingDir * 2.2f, -6f),
                    Math.Max(1, (int)(baseDamage * 0.7f)), Projectile.knockBack * 0.6f,
                    0f, target.whoAmI);
            }

            GsTheHorsemansBlade scheme = Scheme;
            if (scheme == null) {
                return;
            }
            //猎首印：换目标重烙，同目标累印
            if (target.whoAmI != scheme.HuntTargetWhoAmI) {
                scheme.HuntTargetWhoAmI = target.whoAmI;
                scheme.HuntMarks = 1;
            }
            else if (scheme.HuntMarks < GsTheHorsemansBlade.HuntMarksMax) {
                scheme.HuntMarks++;
                if (scheme.HuntMarks == GsTheHorsemansBlade.HuntMarksMax) {
                    //印满：狱火点燃提示
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = 0.2f }, Owner.Center);
                    SetFlash(6);
                }
            }

            //骑阵：印满 + 终结拍，一拍只发一次
            if (IsFinisher && !cavalryFired && scheme.HuntMarks >= GsTheHorsemansBlade.HuntMarksMax) {
                cavalryFired = true;
                scheme.HuntMarks = 0;
                int dir = Math.Sign(target.Center.X - Owner.Center.X);
                if (dir == 0) {
                    dir = facingDir;
                }
                float laneY = target.Center.Y;
                int rideDamage = Math.Max(1, (int)(baseDamage * 0.55f));
                for (int i = 0; i < 4; i++) {
                    Vector2 at = new(Owner.Center.X - dir * (46f + i * 36f), laneY + (i - 1.5f) * 22f);
                    SpawnOwnedProj(ModContent.ProjectileType<GsTheHorsemansBladeJackProj>(),
                        at, new Vector2(dir, 0f), rideDamage, Projectile.knockBack * 0.5f,
                        1f, i * 5f);
                }
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.55f, Pitch = -0.2f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            GsTheHorsemansBlade scheme = Scheme;
            bool full = Owner.whoAmI == Main.myPlayer
                && scheme != null && scheme.HuntMarks >= GsTheHorsemansBlade.HuntMarksMax;
            if (IsFinisher && phase is PhaseRaise or PhaseHold) {
                //重劈蓄势：狱火余烬自四周卷入刀身，印满加倍
                int count = full ? 2 : 1;
                for (int i = 0; i < count; i++) {
                    Vector2 blade = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f));
                    Vector2 from = blade + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 66f);
                    PRTLoader.NewParticle<PRT_Light>(from, (blade - from) * 0.14f,
                        Main.rand.NextBool() ? GsTheHorsemansBlade.JackMain : GsTheHorsemansBlade.JackHot,
                        Main.rand.NextFloat(0.06f, 0.11f))?.Configure(9, 0.6f);
                }
            }
            else if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                //斩切期刃面燎起火舌
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                    DustID.Torch, Vector2.Zero, 90, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
                d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 2f
                    - Vector2.UnitY * 0.8f;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //南瓜瓤迸溅（带重力）+ 狱火星
            int pulp = IsFinisher ? 7 : 4;
            for (int i = 0; i < pulp; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Pumpkin,
                    (-Vector2.UnitY).RotatedByRandom(0.9) * Main.rand.NextFloat(2f, 5f),
                    40, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                    Main.rand.NextBool() ? GsTheHorsemansBlade.JackHot : GsTheHorsemansBlade.JackMain,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>猎首印鬼脸烙在目标头顶 + 印满重劈蓄势的狱火光环（层数 owner 独有，只画给 owner）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsTheHorsemansBlade scheme = Scheme;
            if (scheme == null) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (glow == null || smear == null || flare == null) {
                return;
            }

            //印满 + 重劈蓄势：刀身狱火光环回旋
            bool full = scheme.HuntMarks >= GsTheHorsemansBlade.HuntMarksMax;
            if (full && IsFinisher && CurrentPhase <= PhaseHold) {
                float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
                Vector2 anchor = Vector2.Lerp(Hand, mainTip, 0.55f) - Main.screenPosition;
                float rot = Main.GlobalTimeWrappedHourly * 1.6f * swingDir + DrawRand01(1) * 6.28f;
                Color halo = GsTheHorsemansBlade.JackMain * (0.26f + 0.3f * p);
                halo.A = 0;
                sb.Draw(flare, anchor, null, halo, rot, flare.Size() * 0.5f, 0.34f + 0.16f * p, SpriteEffects.None, 0f);
            }

            //鬼脸烙印：咧嘴 + 双眼悬在被印目标头顶，狱火明灭
            int idx = scheme.HuntTargetWhoAmI;
            if (scheme.HuntMarks <= 0 || idx < 0 || idx >= Main.maxNPCs) {
                return;
            }
            NPC npc = Main.npc[idx];
            if (!npc.active) {
                return;
            }
            float strength = scheme.HuntMarks / (float)GsTheHorsemansBlade.HuntMarksMax;
            float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + DrawRand01(5) * 6.28f);
            Vector2 head = npc.Top + new Vector2(0f, -26f) - Main.screenPosition;

            Color grin = (full ? GsTheHorsemansBlade.JackHot : GsTheHorsemansBlade.JackMain) * (0.55f * pulse * (0.4f + 0.6f * strength));
            grin.A = 0;
            //咧嘴弧：弯口朝上
            sb.Draw(smear, head + new Vector2(0f, 6f), null, grin, MathHelper.Pi,
                smear.Size() * 0.5f, new Vector2(0.085f, 0.05f), SpriteEffects.None, 0f);
            //双眼
            for (int e = -1; e <= 1; e += 2) {
                sb.Draw(glow, head + new Vector2(e * 7f, -3f), null, grin, 0f,
                    glow.Size() * 0.5f, 0.10f, SpriteEffects.None, 0f);
            }
            //印痕刻点：已攒层数排成小弧
            for (int i = 0; i < scheme.HuntMarks; i++) {
                Vector2 at = head + new Vector2((i - (GsTheHorsemansBlade.HuntMarksMax - 1) * 0.5f) * 9f, -16f);
                Color pip = GsTheHorsemansBlade.JackBright * (0.5f * pulse);
                pip.A = 0;
                sb.Draw(glow, at, null, pip, 0f, glow.Size() * 0.5f, 0.07f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 烈焰南瓜头：ai[0]=0 追撞（ai[1]=目标 whoAmI，弧线加速追击，命中或坠地即爆）；
    /// ai[0]=1 骑阵（ai[1]=出阵错帧，原地聚焰后沿目标线冲锋，颠簸疾驰穿透碾压）。
    /// 自绘：Extra_98 三瓣暗瓜体 + 狱火眼口辉光 + 顶焰 + 旧位残焰拖尾；绘制抖动 identity 播种
    /// </summary>
    internal class GsTheHorsemansBladeJackProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int CavalryWindupBase = 12;
        private const int CavalryRideFrames = 52;

        private int Mode => (int)Projectile.ai[0];
        private bool IsCavalry => Mode == 1;
        private int Windup => (int)Projectile.ai[1] + CavalryWindupBase;
        private ref float Age => ref Projectile.localAI[0];
        private ref float LaneY => ref Projectile.localAI[1];
        private ref float RideDir => ref Projectile.localAI[2];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            //一颗瓜对同一目标只结算一次：追撞头命中即爆，骑阵头整程碾一下
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 110;
        }

        /// <summary>确定性伪随机（identity+salt 播种，各端一致且逐帧稳定）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool? CanDamage() {
            if (IsCavalry) {
                return Age > Windup ? null : false;
            }
            return Age >= 2f ? null : false;
        }

        public override void AI() {
            Age++;
            if (IsCavalry) {
                CavalryAI();
            }
            else {
                ChaseAI();
            }

            if (!VaultUtils.isServer) {
                bool riding = !IsCavalry || Age > Windup;
                if (riding && Main.rand.NextBool(2)) {
                    //焰尾余烬顺体后甩
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                        DustID.Torch, -Projectile.velocity * 0.18f - Vector2.UnitY * 0.6f,
                        100, default, Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, GsTheHorsemansBlade.JackMain.ToVector3() * 0.55f);
            }
        }

        /// <summary>追撞：转向率与速度随寿命抬升，弧线咬合；无标可追转坠地自爆</summary>
        private void ChaseAI() {
            if (Age == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.42f, Pitch = 0.15f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                    GsTheHorsemansBlade.JackMain, 0.2f)?.Configure(10, 0.7f);
            }

            NPC target = null;
            int idx = (int)Projectile.ai[1];
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC n = Main.npc[idx];
                if (n.active && n.CanBeChasedBy(Projectile)) {
                    target = n;
                }
            }
            if (target == null) {
                target = FindChaseTarget(560f);
                if (target != null) {
                    Projectile.ai[1] = target.whoAmI;
                }
            }

            if (target != null) {
                float speed = MathF.Min(6f + Age * 0.5f, 17f);
                float turn = MathF.Min(0.05f + Age * 0.005f, 0.16f);
                Vector2 cur = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = Vector2.Lerp(cur, desired, turn).SafeNormalize(Vector2.UnitY) * speed;
            }
            else {
                //无标：横速衰减、重力接管、落地即爆
                Projectile.tileCollide = true;
                Projectile.velocity.X *= 0.985f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.32f, 14f);
            }
            //头身随横速前倾
            Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.035f, -0.5f, 0.5f);
        }

        /// <summary>骑阵：错帧聚焰起阵，随后沿锁定线颠簸冲锋，末段收力渐隐</summary>
        private void CavalryAI() {
            if (Age == 1f) {
                LaneY = Projectile.Center.Y;
                RideDir = Projectile.velocity.X >= 0f ? 1f : -1f;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = Windup + CavalryRideFrames + 6;
            }
            if (Age <= Windup) {
                Projectile.velocity = Vector2.Zero;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    //起阵聚焰：余烬向瓜体收拢
                    Vector2 from = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(20f, 42f);
                    PRTLoader.NewParticle<PRT_Light>(from, (Projectile.Center - from) * 0.16f,
                        GsTheHorsemansBlade.JackHot, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(8, 0.55f);
                }
                return;
            }
            if (Age == Windup + 1f && !VaultUtils.isServer) {
                //冲锋号：狱火喷腔
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.25f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                    GsTheHorsemansBlade.JackHot, 0.24f)?.Configure(10, 0.8f);
            }

            float ride = Age - Windup;
            //提速-巡航-衰力三段，全程不匀速
            float speed = MathF.Abs(Projectile.velocity.X);
            speed = ride <= 8f ? MathF.Min(19f, speed + 2.4f)
                : ride > 36f ? MathF.Max(13f, speed * 0.97f) : 19f;
            Projectile.velocity.X = RideDir * speed;
            //颠簸疾驰 + 回归锁定线
            Projectile.velocity.Y = MathF.Sin(ride * 0.5f + SegRand(3) * 6.28f) * 1.3f
                + MathHelper.Clamp((LaneY - Projectile.Center.Y) * 0.03f, -1.2f, 1.2f);
            Projectile.rotation = RideDir * 0.10f + MathF.Sin(ride * 0.5f) * 0.05f;
        }

        private NPC FindChaseTarget(float maxDist) {
            NPC best = null;
            float bestDist = maxDist;
            foreach (NPC n in Main.ActiveNPCs) {
                if (!n.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = Vector2.Distance(n.Center, Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = n;
                }
            }
            return best;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!IsCavalry) {
                //追撞头命中即爆
                Projectile.Kill();
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //骑阵碾过：轻量瓜瓤扬起
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Pumpkin,
                    (-Vector2.UnitY).RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 4f),
                    60, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中相：瓜体炸裂——瓤块抛洒 + 狱火星放射 + 焰光一闪
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.32f, Pitch = 0.35f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsTheHorsemansBlade.JackHot, 0.3f)?.Configure(12, 0.85f);
            for (int i = 0; i < 9; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Pumpkin,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6.5f),
                    30, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = false;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f),
                    Main.rand.NextBool(3) ? GsTheHorsemansBlade.JackBright : GsTheHorsemansBlade.JackHot,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 22));
            }
            //余痕相：狱火余烬缓缓上飘
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    GsTheHorsemansBlade.JackMain, Main.rand.NextFloat(0.06f, 0.1f))?.Configure(14, 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            if (blot == null || glow == null || smear == null) {
                return false;
            }
            //出场/收场可见度：追撞头 5 帧现身；骑阵起阵期渐显、末段渐隐
            float presence;
            if (IsCavalry) {
                presence = Age <= Windup
                    ? MathHelper.Clamp(Age / CavalryWindupBase, 0f, 1f) * 0.85f
                    : MathHelper.Clamp((Windup + CavalryRideFrames + 4 - Age) / 10f, 0f, 1f);
            }
            else {
                presence = MathHelper.Clamp(Age / 5f, 0f, 1f);
            }
            if (presence <= 0.02f) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float lean = Projectile.rotation;
            float flick = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + SegRand(7) * 6.28f);

            //飞行拖尾：旧位残焰逐节缩淡
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                pos += Projectile.Size / 2f - Main.screenPosition;
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Color tail = GsTheHorsemansBlade.JackMain * (0.26f * k * presence);
                tail.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, tail, 0f, glow.Size() * 0.5f,
                    0.5f * k + 0.1f, SpriteEffects.None, 0);
            }

            //焰环底光
            Color aura = GsTheHorsemansBlade.JackMain * (0.5f * presence * flick);
            aura.A = 0;
            Main.EntitySpriteDraw(glow, center, null, aura, 0f, glow.Size() * 0.5f, 0.82f * presence, SpriteEffects.None, 0);

            //南瓜体：真 alpha 暗瓜三瓣（中瓣大、侧瓣小，读出棱线）
            Color rind = new Color(46, 18, 8) * (0.92f * presence);
            Main.EntitySpriteDraw(blot, center, null, rind, lean,
                blot.Size() * 0.5f, new Vector2(0.33f, 0.29f) * presence, SpriteEffects.None, 0);
            for (int s = -1; s <= 1; s += 2) {
                Main.EntitySpriteDraw(blot, center + lean.ToRotationVector2() * (s * 9f), null, rind * 0.9f, lean,
                    blot.Size() * 0.5f, new Vector2(0.24f, 0.27f) * presence, SpriteEffects.None, 0);
            }

            //狱火脸：双眼 + 咧嘴（口弧弯向下颌，读出鬼笑）
            Color face = GsTheHorsemansBlade.JackBright * (0.85f * presence * flick);
            face.A = 0;
            for (int e = -1; e <= 1; e += 2) {
                Vector2 eye = center + new Vector2(e * 7f, -4f).RotatedBy(lean);
                Main.EntitySpriteDraw(glow, eye, null, face, 0f, glow.Size() * 0.5f, 0.10f * presence, SpriteEffects.None, 0);
            }
            Vector2 mouth = center + new Vector2(0f, 7f).RotatedBy(lean);
            Main.EntitySpriteDraw(smear, mouth, null, face, MathHelper.Pi + lean,
                smear.Size() * 0.5f, new Vector2(0.09f, 0.05f) * presence, SpriteEffects.None, 0);

            //顶焰：两层火舌交叠，明灭错相
            Vector2 crown = center + new Vector2(0f, -14f).RotatedBy(lean);
            float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + SegRand(11) * 6.28f) * 0.25f;
            Color fireOut = GsTheHorsemansBlade.JackHot * (0.6f * presence * flick);
            fireOut.A = 0;
            Main.EntitySpriteDraw(smear, crown, null, fireOut, -MathHelper.PiOver2 + lean + wave,
                smear.Size() * 0.5f, new Vector2(0.10f, 0.07f) * presence, SpriteEffects.None, 0);
            Color fireIn = GsTheHorsemansBlade.JackBright * (0.5f * presence * flick);
            fireIn.A = 0;
            Main.EntitySpriteDraw(smear, crown + new Vector2(0f, -3f).RotatedBy(lean), null, fireIn,
                -MathHelper.PiOver2 + lean - wave, smear.Size() * 0.5f,
                new Vector2(0.06f, 0.05f) * presence, SpriteEffects.None, 0);
            return false;
        }
    }
}
