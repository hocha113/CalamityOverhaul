using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 叶绿队矛重铸：控孢。<br/>
    /// 材质：活体叶绿晶矛刃。签名行为：①驻相时矛尖聚孢——绿光粒子自四周向尖端收束，
    /// 聚孢过程肉眼可见 ②驻相结束的收相首帧，尖端释放一团缓飘追踪孢子云，
    /// 命中挂剧毒（孢子听驻相号令，与原版的随机孢子相反）③命中反馈是湿软的孢爆绿雾
    /// </summary>
    internal class GsChlorophytePartisan : GsSpearScheme
    {
        public override int TargetItemID => ItemID.ChlorophytePartisan;

        protected override string GsDescFallback =>
            "Reforged: while the blade dwells at full reach, spores gather on its tip;" +
            "\nas the spear withdraws it looses a drifting spore cloud that hunts and poisons";

        protected override int HeldProjType => ModContent.ProjectileType<GsChlorophytePartisanHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;//孢子云 50% 伤害 + 剧毒是主要机制收益，综合 DPS 落在原版 106%~118%
    }

    /// <summary>
    /// 叶绿队矛手持突刺：驻相 6 帧聚孢（收束粒子），收相首帧自尖端放孢子云（50% 伤害，挂剧毒）
    /// </summary>
    internal class GsChlorophytePartisanHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.ChlorophytePartisan;

        //活体叶绿色板
        internal static readonly Color ChloroBright = new(178, 255, 96);   //叶绿荧光
        internal static readonly Color ChloroGreen = new(88, 196, 64);     //叶绿主
        internal static readonly Color ChloroDeep = new(28, 84, 40);       //深叶影

        //叶绿轻快，驻相拉长给聚孢演出留时间
        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 6f;
        protected override float RecoverFrames => 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 64f;
        protected override float BladeLength => 94f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.04f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.14f;

        protected override Color EdgeColor => ChloroBright;
        protected override Color CoreColor => ChloroGreen;

        /// <summary>本次突刺是否已放孢（收相首帧只放一次）</summary>
        private bool sporeReleased;

        /// <summary>驻相聚孢：绿光粒子自四周收束向矛尖</summary>
        protected override void OnTick(int phase) {
            if (phase == PhaseDwell) {
                if (VaultUtils.isServer) {
                    return;
                }
                //收束粒子：密度随驻相推进增高
                float dwellT = (Elapsed - WindupFrames - ThrustFrames) / DwellFrames;
                if (Main.rand.NextFloat() < 0.45f + dwellT * 0.4f) {
                    Vector2 tip = TipPos;
                    Vector2 from = tip + Main.rand.NextVector2Unit() * Main.rand.NextFloat(28f, 52f);
                    Color c = Main.rand.NextBool(3) ? ChloroBright : ChloroGreen;
                    PRTLoader.NewParticle<PRT_Light>(from, (tip - from) * 0.16f, c,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(7, 11), 0.55f, 1.3f);
                }
                return;
            }
            //收相首帧 = 驻相结束：孢子听号令出膛
            if (phase == PhaseRecover && !sporeReleased) {
                sporeReleased = true;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), TipPos, stabUnit * 3.2f,
                        ModContent.ProjectileType<GsChlorophytePartisanSporeProj>(),
                        (int)(BaseDamage * 0.5f), Projectile.knockBack * 0.2f, Owner.whoAmI);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f, Pitch = 0.3f }, TipPos);
                }
            }
        }

        /// <summary>命中反馈：湿软孢爆——绿雾光团 + 慢速荧光屑，无金属音</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, ChloroGreen, 0.22f)?.Configure(12, 0.6f, 1.2f);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 4.5f);
                Color c = Main.rand.NextBool(3) ? ChloroBright : ChloroGreen;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(false, Main.rand.Next(14, 22));
            }
        }

        /// <summary>驻相聚孢期矛刃荧光升档</summary>
        protected override float ExtraGlowStrength() {
            if (CurrentPhase != PhaseDwell) {
                return 0f;
            }
            float dwellT = (Elapsed - WindupFrames - ThrustFrames) / DwellFrames;
            return MathHelper.Clamp(dwellT, 0f, 1f) * 0.35f;
        }
    }

    /// <summary>
    /// 叶绿孢子云：驻相聚出的活体孢团，缓飘追踪最近猎物，命中挂剧毒。<br/>
    /// 自绘三层呼吸光团 + 绕核孢点（whoAmI 种子），飞行沿途洒荧光屑
    /// </summary>
    internal class GsChlorophytePartisanSporeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ChlorophytePartisan");

        private ref float Life => ref Projectile.localAI[0];
        private float Seed => Projectile.whoAmI * 1.173f % 4.9f;

        /// <summary>出生 4 帧淡入、末尾 10 帧淡出</summary>
        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 4f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
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
            Life++;

            //缓飘追踪：低速巡飞，锁定后温和转向（孢子云不冲刺）
            NPC target = Projectile.Center.FindClosestNPC(420f);
            if (target != null) {
                Projectile.SmoothHomingBehavior(target.Center, 1f, 0.06f);
            }
            float speed = Projectile.velocity.Length();
            if (speed > 4.2f) {
                Projectile.velocity *= 0.96f;
            }
            else if (speed < 2.2f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 2.2f;
            }

            Lighting.AddLight(Projectile.Center, GsChlorophytePartisanHeld.ChloroGreen.ToVector3() * (0.3f * VisualFade));

            if (VaultUtils.isServer) {
                return;
            }
            if (Life % 4f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(0.5f, 0.5f) - Vector2.UnitY * 0.3f,
                    Main.rand.NextBool(3) ? GsChlorophytePartisanHeld.ChloroBright : GsChlorophytePartisanHeld.ChloroGreen,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(10, 16), 0.5f, 1.2f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 300);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = -0.4f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f);
                Color c = Main.rand.NextBool() ? GsChlorophytePartisanHeld.ChloroBright : GsChlorophytePartisanHeld.ChloroGreen;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.28f, 0.5f))
                    ?.Configure(false, Main.rand.Next(12, 18));
            }
        }

        /// <summary>三层呼吸光团 + 三粒绕核孢点（whoAmI 种子，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;
            float breath = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + Seed);

            //深叶影底晕
            Main.spriteBatch.Draw(glow, pos, null,
                (GsChlorophytePartisanHeld.ChloroDeep with { A = 0 }) * (0.6f * fade), 0f,
                origin, 0.62f * breath, SpriteEffects.None, 0f);
            //叶绿主团
            Main.spriteBatch.Draw(glow, pos, null,
                (GsChlorophytePartisanHeld.ChloroGreen with { A = 0 }) * (0.8f * fade), 0f,
                origin, 0.42f * breath, SpriteEffects.None, 0f);
            //荧光芯
            Main.spriteBatch.Draw(glow, pos, null,
                (GsChlorophytePartisanHeld.ChloroBright with { A = 0 }) * (0.6f * fade), 0f,
                origin, 0.2f * breath, SpriteEffects.None, 0f);
            //绕核孢点三粒
            for (int i = 0; i < 3; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 3.2f + Seed + i * MathHelper.TwoPi / 3f;
                Vector2 orbit = pos + ang.ToRotationVector2() * (12f * breath);
                Main.spriteBatch.Draw(glow, orbit, null,
                    (GsChlorophytePartisanHeld.ChloroBright with { A = 0 }) * (0.5f * fade), 0f,
                    origin, 0.09f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
