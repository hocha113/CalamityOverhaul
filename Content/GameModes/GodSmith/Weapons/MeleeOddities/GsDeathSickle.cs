using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords;
using CalamityOverhaul.Content.PRTTypes;
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
    /// 【亡魂锁镰】材质：冷黑铁刃 + 苍绿魂焰缘的双质死神镰。签名：①穿墙旋镰「越割越凶」，
    /// 每割中一名新敌成长一档（变大变重变凶，上限 3 档）②满 3 档停驻原地爆出魂焰环后自散
    /// ③命中魂魄吸入演出 + 穿墙暗剪影层让隔墙也读得到轮廓
    /// </summary>
    internal class GsDeathSickle : GsOdditiesComboScheme
    {
        public override int TargetItemID => ItemID.DeathSickle;

        protected override int HeldProjID => ModContent.ProjectileType<GsDeathSickleHeld>();

        protected override int ComboBeats => 3;

        protected override string GsDescFallback =>
            "Reforged: the wall-piercing scythe grows a tier for each new enemy it reaps;\n" +
            "at three tiers it erupts in a ring of soulfire where it halts";

        //亡魂色板
        internal static readonly Color SoulGreen = new(125, 255, 158); //苍绿魂焰
        internal static readonly Color SoulDim = new(52, 120, 84);     //幽绿暗焰
        internal static readonly Color IronBlack = new(38, 42, 40);    //冷黑铁刃
        internal static readonly Color BoneWhite = new(214, 226, 208); //骨白强调

        //数值包络 ×1.0：底伤一分不加——每挥全额穿墙旋镰是原版保真非增益，
        //净增收益（每档 +6% 弹伤/+8% 判定体积、满档 0.9× 魂焰环）已占满预算（公约 §5），
        //故不重写 GsModifyWeaponDamage

        /// <summary>
        /// 压掉原版挥舞的物理尾巴：held 每帧强撑 itemAnimation&gt;0 而本器 noMelee=false，
        /// ItemCheck 近战尾巴（挥舞碰撞箱直击+切割）在 owner 端仍会逐帧执行，不压则与 held 双份直击
        /// </summary>
        public override void GsUseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
            => noHitbox = true;
    }

    /// <summary>
    /// 亡魂锁镰手持：三拍全重斩（大弧、顿帧 2、音色沉），2 为收魂重斩（前压）。
    /// 每拍斩切爆发掷全额旋镰（初速 9）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsDeathSickleHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.DeathSickle;
        protected override Color EdgeBright => GsDeathSickle.SoulGreen;
        protected override Color BodyMain => GsDeathSickle.SoulDim;
        protected override Color HotAccent => GsDeathSickle.BoneWhite;
        protected override Color DeepShadow => GsDeathSickle.IronBlack;

        /// <summary>原版 scale 1.1 的大镰，触及放大</summary>
        protected override float BaseReach => 126f;

        /// <summary>铁刃压向冷黑，魂焰缘由辉光层供色（双质）</summary>
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsDeathSickle.IronBlack, 0.35f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsDeathSickle.BoneWhite : GsDeathSickle.SoulGreen;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //收魂重斩：最大弧前压
                return new GsBroadBeat {
                    Raise = 9, Hold = 4, Slash = 6, Recover = 13,
                    RaiseBack = 2.45f, Follow = 1.35f, ReachScale = 1.18f, LeanAmp = 0.09f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2f, SwingPitch = -0.5f,
                };
            }
            //0/1 交替重劈：拍全程比 Standard 沉重（大弧、顿帧 2、音色沉）
            return new GsBroadBeat {
                Raise = stage == 0 ? 8 : 7, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = stage == 0 ? 2.1f : 2.2f, Follow = stage == 0 ? 1.2f : 1.15f,
                ReachScale = stage == 0 ? 1.05f : 1.08f, LeanAmp = 0.065f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = stage == 0 ? -0.38f : -0.3f,
            };
        }

        /// <summary>斩切爆发：掷全额穿墙旋镰（原版每挥必发的保真，初速 9）</summary>
        protected override void OnSlashBegin() {
            Vector2 aim = baseAngle.ToRotationVector2();
            SpawnOwnedProj(ModContent.ProjectileType<GsDeathSickleSpinProj>(),
                Hand + aim * 24f, aim * 9f, Projectile.damage, Projectile.knockBack);
        }

        /// <summary>挥弧沿途逸散魂焰（已在非服务器端调用）</summary>
        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase != PhaseSlash || !Main.rand.NextBool(2)) {
                return;
            }
            Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                DustID.GreenTorch, Vector2.Zero, 110, default, Main.rand.NextFloat(0.9f, 1.3f));
            d.noGravity = true;
            d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 1.6f;
        }

        /// <summary>命中补魂焰舔舐（基类钢/肉分流照常）</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenTorch,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 100, default,
                    Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 亡魂旋镰：原版 274 保真（42px、穿透 5、tileCollide=false 穿墙、scale 1.1、timeLeft 180、
    /// idStatic 10、×0.96 减速），自旋同冰镰但更缓。签名「越割越凶」：每割中一名新敌成长一档
    /// （上限 3；owner 记账写 ai[0]=tier、netUpdate 过线，远端只读 ai[0] 演绎体积），
    /// 每档判定与 scale +8%、owner 伤害 +6%、魂焰缘变长；满 3 档且停驻（速度&lt;0.45）时
    /// owner 置 ai[1]=1 转爆环态：Resize 150、6 帧伤害窗一击 0.9×、魂焰环演出后自散；
    /// 未满档停下则按原版淡出。自绘：穿墙暗剪影垫底 + 原版贴图 + 魂焰缘错相闪变 + 3 拍旋转残影
    /// </summary>
    internal class GsDeathSickleSpinProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.DeathSickle");

        private const int MaxTier = 3;
        /// <summary>爆环态总帧数（伤害窗只开前 6 帧，余下纯演出）</summary>
        private const int BurstLife = 22;

        /// <summary>自旋方向（首帧按横速符号定，各端同式）</summary>
        private int spinDir;
        /// <summary>出生伤害基准（各端首帧各自缓存，仅 owner 消费）</summary>
        private int baseDamage;
        /// <summary>已套用的成长档（各端从 ai[0] 演绎）</summary>
        private int appliedTier = -1;
        /// <summary>爆环态本地已初始化</summary>
        private bool burstStarted;
        private int burstTimer;
        /// <summary>已命中过的目标（owner 端记账用）</summary>
        private readonly HashSet<int> struck = [];

        private int Tier => Math.Clamp((int)Projectile.ai[0], 0, MaxTier);
        private bool InBurst => Projectile.ai[1] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            //镜像原版 274：Projectile.cs SetDefaults
            Projectile.width = Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.tileCollide = false; //穿墙是原版身份
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.scale = 1.1f;
            Projectile.timeLeft = 180;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
        }

        /// <summary>原版 aiStyle 106（含 274）不切物块，照封</summary>
        public override bool? CanCutTiles() => false;

        /// <summary>爆环态只开前 6 帧伤害窗；飞行期照原版全程可判</summary>
        public override bool? CanDamage() => burstStarted ? (burstTimer <= 6 ? null : false) : null;

        public override void AI() {
            if (spinDir == 0) {
                spinDir = Projectile.velocity.X >= 0f ? 1 : -1;
                baseDamage = Projectile.damage;
            }

            //满 3 档且停驻 → owner 记账转爆环态，ai[1] 过线；各端读到后各自本地初始化
            if (Projectile.owner == Main.myPlayer && !InBurst
                && Tier >= MaxTier && Projectile.velocity.Length() < 0.45f) {
                Projectile.ai[1] = 1f;
                Projectile.netUpdate = true;
            }
            if (InBurst && !burstStarted) {
                InitBurst();
            }
            if (burstStarted) {
                BurstAI();
                return;
            }

            //成长档演绎（各端从 ai[0] 读）：每档判定与 scale +8%
            if (Tier != appliedTier) {
                appliedTier = Tier;
                Projectile.scale = 1.1f * (1f + 0.08f * appliedTier);
                int size = (int)(42 * (1f + 0.08f * appliedTier));
                Projectile.Resize(size, size);
            }

            //飞行：×0.96 减速（原版保真）；自旋同冰镰但更缓
            Projectile.rotation += spinDir * (0.10f + 0.35f * (1f - Projectile.timeLeft / 180f));
            Projectile.velocity *= 0.96f;
            Lighting.AddLight(Projectile.Center, GsDeathSickle.SoulGreen.ToVector3() * (0.35f + 0.1f * Tier));

            //魂焰拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool(3 - Math.Min(Tier, 2))) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.GreenTorch, -Projectile.velocity * 0.12f, 110, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        /// <summary>爆环态本地初始化：撑大判定、锁伤害窗、演出一次</summary>
        private void InitBurst() {
            burstStarted = true;
            burstTimer = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.penetrate = -1; //环窗内不因穿透耗尽早夭
            Projectile.Resize(150, 150);
            Projectile.timeLeft = BurstLife;
            if (Projectile.owner == Main.myPlayer) {
                Projectile.damage = (int)(baseDamage * 0.9f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = -0.15f }, Projectile.Center);
                for (int i = 0; i < 18; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / 18f).ToRotationVector2();
                    Dust d = Dust.NewDustPerfect(Projectile.Center + dir * 12f, DustID.GreenTorch,
                        dir * Main.rand.NextFloat(3f, 7f), 100, default, Main.rand.NextFloat(1.1f, 1.6f));
                    d.noGravity = true;
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsDeathSickle.SoulGreen, 0.32f)
                    ?.Configure(12, 0.85f);
            }
        }

        private void BurstAI() {
            burstTimer++;
            Lighting.AddLight(Projectile.Center,
                GsDeathSickle.SoulGreen.ToVector3() * (0.9f * Projectile.timeLeft / BurstLife));
            //环缘零星喷焰
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(30f, 70f),
                    DustID.GreenTorch, ang.ToRotationVector2() * 1.5f, 110, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中钩子只在 owner 端执行：成长记账天然 owner 独占，档位走 ai[0]+netUpdate 过线
            if (Projectile.owner == Main.myPlayer && !burstStarted && struck.Add(target.whoAmI)) {
                int tier = Tier;
                if (tier < MaxTier) {
                    tier++;
                    Projectile.ai[0] = tier;
                    //owner 端权威伤害：每档 +6%（命中判定只在 owner 端解算，远端 damage 不消费）
                    Projectile.damage = (int)(baseDamage * (1f + 0.06f * tier));
                    Projectile.netUpdate = true;
                }
            }
            //魂魄吸入演出：目标处苍绿光斑 + 2~3 粒魂火飞向镰体（owner 屏演出，纯表现分歧可接受）
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsDeathSickle.SoulGreen, 0.2f)
                    ?.Configure(10, 0.8f);
                int wisps = Main.rand.Next(2, 4);
                for (int i = 0; i < wisps; i++) {
                    Vector2 vel = (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitY)
                        * Main.rand.NextFloat(3.5f, 6.5f);
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, GsDeathSickle.SoulGreen,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 16), Projectile);
                }
            }
        }

        /// <summary>消散演出：爆环态的主演出在 InitBurst，这里只补余焰；未满档停下按原版淡出后轻散</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            int puffs = burstStarted ? 8 : 4;
            for (int i = 0; i < puffs; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.GreenTorch, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.8f), 120, default,
                    Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
        private float SeedRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.DeathSickle);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.DeathSickle].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 at = Projectile.Center - Main.screenPosition;
            SpriteEffects fx = spinDir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (burstStarted) {
                DrawBurst(at);
                //爆环头几帧镰体速融进环里
                float melt = MathHelper.Clamp(1f - burstTimer / 8f, 0f, 1f);
                if (melt > 0f) {
                    Main.EntitySpriteDraw(tex, at, null, Color.Lerp(lightColor, GsDeathSickle.SoulGreen, 0.5f) * melt,
                        Projectile.rotation, origin, Projectile.scale, fx, 0);
                }
                return false;
            }

            //镜像原版 GetAlpha(274)：末 85 帧淡出消失
            float bodyAlpha = Projectile.timeLeft < 85 ? Projectile.timeLeft / 85f : 1f;

            //穿墙暗剪影层：真 alpha 暗色放大垫底，光照归零的墙内也读得到轮廓
            Color silhouette = new Color(30, 40, 36, 200) * (0.6f * bodyAlpha);
            Main.EntitySpriteDraw(tex, at, null, silhouette, Projectile.rotation,
                origin, Projectile.scale * 1.02f, fx, 0);

            //3 拍旋转残影（oldRot/oldPos 渐淡）
            for (int i = 5; i >= 1; i -= 2) {
                Color ghost = GsDeathSickle.SoulGreen * (0.22f * (1f - i / 6f) * bodyAlpha);
                ghost.A = 0;
                Main.EntitySpriteDraw(tex, Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition,
                    null, ghost, Projectile.oldRot[i], origin, Projectile.scale, fx, 0);
            }

            //铁刃本体
            Main.EntitySpriteDraw(tex, at, null, lightColor * bodyAlpha, Projectile.rotation,
                origin, Projectile.scale, fx, 0);

            //魂焰缘：加色 A=0，错相 2 帧闪变（identity 播种），档位越高焰缘越长越亮
            int step = (int)(Main.GameUpdateCount / 2u);
            float flick = 0.65f + 0.35f * SeedRand01(step & 0x3FFF);
            Color rim = GsDeathSickle.SoulGreen * ((0.20f + 0.10f * Tier) * flick * bodyAlpha);
            rim.A = 0;
            Main.EntitySpriteDraw(tex, at, null, rim, Projectile.rotation,
                origin, Projectile.scale * (1.04f + 0.025f * Tier), fx, 0);
            Color rimOuter = GsDeathSickle.SoulDim * ((0.14f + 0.07f * Tier) * flick * bodyAlpha);
            rimOuter.A = 0;
            Main.EntitySpriteDraw(tex, at, null, rimOuter, Projectile.rotation,
                origin, Projectile.scale * (1.09f + 0.035f * Tier), fx, 0);
            return false;
        }

        /// <summary>魂焰环：SoftGlow 大环双层扩散 + StarTexture 十字微光，加色批全 A=0</summary>
        private void DrawBurst(Vector2 at) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null) {
                return;
            }
            float bp = MathHelper.Clamp(burstTimer / (float)BurstLife, 0f, 1f);
            float fade = MathF.Pow(1f - bp, 1.4f);

            //外环苍绿扩散
            Color outer = GsDeathSickle.SoulGreen * (0.5f * fade);
            outer.A = 0;
            float outerDiam = 210f * (0.35f + 0.9f * bp);
            Main.EntitySpriteDraw(glow, at, null, outer, 0f, glow.Size() / 2f,
                outerDiam / glow.Width, SpriteEffects.None, 0);
            //内环骨白亮芯（更快熄灭）
            Color inner = GsDeathSickle.BoneWhite * (0.55f * MathF.Pow(1f - bp, 2.2f));
            inner.A = 0;
            Main.EntitySpriteDraw(glow, at, null, inner, 0f, glow.Size() / 2f,
                outerDiam * 0.55f / glow.Width, SpriteEffects.None, 0);
            //十字微光（identity 播种初始角缓旋收缩）
            if (star != null) {
                Color cross = Color.Lerp(GsDeathSickle.BoneWhite, GsDeathSickle.SoulGreen, bp) * (0.6f * fade);
                cross.A = 0;
                float crossRot = (SeedRand01(3) * MathHelper.TwoPi) + (bp * 0.5f);
                Main.EntitySpriteDraw(star, at, null, cross, crossRot, star.Size() / 2f,
                    0.22f - 0.08f * bp, SpriteEffects.None, 0);
            }
        }
    }
}
