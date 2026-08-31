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
    /// 山铜戟重铸：花瓣锋阵。<br/>
    /// 材质：山铜粉晶戟刃。签名行为：①每次刺中自目标伤口绽出两枚追击花瓣，
    /// 螺旋咬向近旁猎物（呼应山铜套装的花瓣风暴）②花瓣自绘粉紫双层瓣形、旋落拖光
    /// ③命中反馈是花瓣簌落的柔响与粉紫光雨，与金属矛的脆响截然不同
    /// </summary>
    internal class GsOrichalcumHalberd : GsSpearScheme
    {
        public override int TargetItemID => ItemID.OrichalcumHalberd;

        protected override string GsDescFallback =>
            "Reforged: every thrust that lands blooms two homing petals from the wound," +
            "\neach spiraling after nearby prey";

        protected override int HeldProjType => ModContent.ProjectileType<GsOrichalcumHalberdHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//花瓣追击是主要机制收益，底伤只小补，综合 DPS 落在原版 105%~118%
    }

    /// <summary>
    /// 山铜戟手持突刺：首个命中绽出两枚追击花瓣（owner 端生成，各 30% 伤害）
    /// </summary>
    internal class GsOrichalcumHalberdHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.OrichalcumHalberd;

        //山铜粉晶色板
        internal static readonly Color PetalPink = new(255, 132, 196);    //粉瓣亮
        internal static readonly Color PetalViolet = new(196, 96, 232);   //紫晶
        internal static readonly Color PetalDeep = new(96, 34, 92);       //深紫底

        protected override float WindupFrames => 5f;
        protected override float ThrustFrames => 5f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => 15f;
        protected override float StabReach => 64f;
        protected override float BladeLength => 94f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.04f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.18f;

        protected override Color EdgeColor => PetalPink;
        protected override Color CoreColor => PetalViolet;
        protected override Color ShaftColor => PetalDeep with { A = 235 };

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //每次突刺只在首个命中绽瓣（owner 端生成，随生成包过线）
            if (!firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                //两瓣沿刺向两侧张开，ai0=螺旋方向
                float side = i == 0 ? 1f : -1f;
                Vector2 vel = stabUnit.RotatedBy(side * 1.1f) * 6f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, vel,
                    ModContent.ProjectileType<GsOrichalcumHalberdPetalProj>(),
                    (int)(BaseDamage * 0.3f), Projectile.knockBack * 0.2f, Owner.whoAmI, side);
            }
        }

        /// <summary>命中反馈：花瓣簌落柔响 + 粉紫光雨（无金属脆响）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, PetalPink, 0.18f)?.Configure(10, 0.7f);
            for (int i = 0; i < 7; i++) {
                //粉紫光雨：向上散开后受重力簌落
                Vector2 vel = stabUnit.RotatedByRandom(0.9) * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f);
                Color c = Main.rand.NextBool(3) ? PetalViolet : PetalPink;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
        }
    }

    /// <summary>
    /// 山铜追击花瓣：自伤口绽出，先沿切向张开再螺旋咬向近旁猎物。<br/>
    /// 自绘双层瓣形：紫晶垫底 + 粉瓣主体（四芒星光横向压扁成瓣），
    /// 自旋相位吃 whoAmI 种子，旋落拖光渐淡。ai[0]=螺旋方向
    /// </summary>
    internal class GsOrichalcumHalberdPetalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.OrichalcumHalberd");

        private ref float Life => ref Projectile.localAI[0];
        private float SpinDir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private float Seed => Projectile.whoAmI * 0.917f % 3.7f;

        /// <summary>出生 3 帧淡入、末尾 8 帧淡出</summary>
        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 75;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;

            //前 8 帧张开，之后螺旋追击：追踪之上叠一层持续切向旋转 = 螺旋轨迹
            if (Life > 8f) {
                NPC target = Projectile.Center.FindClosestNPC(480f);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1.01f, 0.10f);
                    Projectile.velocity = Projectile.velocity.RotatedBy(SpinDir * 0.05f);
                }
            }
            float speed = Projectile.velocity.Length();
            if (speed < 5f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 5f;
            }
            //瓣面自旋
            Projectile.rotation += SpinDir * 0.28f;

            Lighting.AddLight(Projectile.Center, GsOrichalcumHalberdHeld.PetalPink.ToVector3() * (0.24f * VisualFade));

            if (VaultUtils.isServer) {
                return;
            }
            if (Life % 3f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.1f,
                    Main.rand.NextBool() ? GsOrichalcumHalberdHeld.PetalViolet : GsOrichalcumHalberdHeld.PetalPink,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(8, 13), 0.5f, 1.3f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.3f, Pitch = 0.6f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f);
                Color c = Main.rand.NextBool() ? GsOrichalcumHalberdHeld.PetalPink : GsOrichalcumHalberdHeld.PetalViolet;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.25f, 0.45f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>双层瓣形自绘：四芒星光横向压扁成花瓣，紫晶垫底 + 粉瓣主体 + 亮芯（无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D petal = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (petal == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = petal.Size() * 0.5f;
            //瓣面张合呼吸（whoAmI 种子，禁 Main.rand）
            float breath = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Seed * 4f);

            //粉晕垫底
            Main.EntitySpriteDraw(glow, pos, null,
                (GsOrichalcumHalberdHeld.PetalDeep with { A = 0 }) * (0.6f * fade), 0f,
                glow.Size() * 0.5f, 0.5f * breath, SpriteEffects.None, 0);
            //紫晶瓣（横向压扁的星光 = 瓣形）
            Main.EntitySpriteDraw(petal, pos, null,
                (GsOrichalcumHalberdHeld.PetalViolet with { A = 0 }) * (0.85f * fade), Projectile.rotation,
                origin, new Vector2(0.34f, 0.18f) * breath, SpriteEffects.None, 0);
            //粉瓣主体（错相叠瓣，双层瓣形）
            Main.EntitySpriteDraw(petal, pos, null,
                (GsOrichalcumHalberdHeld.PetalPink with { A = 0 }) * fade, Projectile.rotation + MathHelper.PiOver4,
                origin, new Vector2(0.28f, 0.14f) * breath, SpriteEffects.None, 0);
            //亮芯
            Main.EntitySpriteDraw(glow, pos, null,
                (Color.White with { A = 0 }) * (0.4f * fade), 0f,
                glow.Size() * 0.5f, 0.14f * breath, SpriteEffects.None, 0);
            return false;
        }
    }
}
