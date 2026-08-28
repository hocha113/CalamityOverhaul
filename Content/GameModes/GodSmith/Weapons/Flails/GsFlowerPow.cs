using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·花之力】粉樱花冠链锤：樱粉花瓣叶绿点缀。签名行为：①甩转每 33 帧绽一片环绕花瓣（至多 5 片，自绘公转）
    /// ②掷出命中瞬间花瓣全数化作追踪花刃齐射目标 ③收链回手花瓣保留不散
    /// </summary>
    internal class GsFlowerPow : GsFlailScheme
    {
        public override int TargetItemID => ItemID.FlowerPow;

        protected override int FlailProjType => ModContent.ProjectileType<GsFlowerPowHead>();

        protected override string GsDescFallback =>
            "Reforged: petals bloom around the head while spinning, up to five" +
            "\nLanding a throw sends every petal homing at the target as razor blades";

        //花瓣齐射（5×45%）是主要增伤来源，底伤只补半成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 花之力锤头。花瓣不是弹幕：计数存锤头字段、由 PostDrawHead 确定性公转自绘；
    /// 花瓣消耗事件经 ai[2]=1 + netUpdate 过线，远端同步收瓣
    /// </summary>
    internal class GsFlowerPowHead : GsFlailHeadProj
    {
        /// <summary>樱粉</summary>
        internal static readonly Color PetalPink = new(246, 148, 182);
        /// <summary>樱芯亮粉白</summary>
        internal static readonly Color PetalCore = new(255, 226, 236);
        /// <summary>叶绿</summary>
        internal static readonly Color LeafGreen = new(112, 182, 92);

        public override int SourceItemID => ItemID.FlowerPow;
        public override int VanillaProjID => ProjectileID.FlowerPow;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain19;
        public override Color GlowColor => PetalPink;

        /// <summary>花瓣上限</summary>
        private const int PetalCap = 5;
        /// <summary>绽放间隔帧</summary>
        private const int BloomInterval = 33;
        /// <summary>花刃伤害系数</summary>
        private const float PetalDamageMul = 0.45f;

        /// <summary>当前环绕花瓣数；各端由各自 OnSpinTick 长出，节奏确定性一致</summary>
        private int petalCount;

        /// <summary>identity 播种的公转相位</summary>
        private float Seed => Projectile.identity * 0.917f;

        protected override void OnSpinTick(float charge) {
            //甩转每 33 帧绽一片，最多 5 片
            if (petalCount >= PetalCap || spinTimer % BloomInterval != 0) {
                return;
            }
            petalCount++;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.55f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, PetalPink, 0.12f)
                    ?.Configure(10, 0.7f);
            }
        }

        protected override void PostStateAI() {
            //owner 消耗花瓣后把 ai[2] 写成 1 并 netUpdate，远端在此同步收瓣
            if (Projectile.ai[2] >= 1f) {
                petalCount = 0;
            }
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || Owner.whoAmI != Main.myPlayer || petalCount <= 0) {
                return;
            }
            //花瓣全数化作追踪花刃齐射目标；出射角按序号确定性散开
            Vector2 baseDir = Projectile.Center.To(target.Center).SafeNormalize(Vector2.UnitX);
            int volley = petalCount;
            for (int i = 0; i < volley; i++) {
                Vector2 dir = baseDir.RotatedBy((i - (volley - 1) * 0.5f) * 0.42f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 13f,
                    ModContent.ProjectileType<GsFlowerPowPetalProj>(),
                    Math.Max(1, (int)(Projectile.damage * PetalDamageMul)), 1.5f, Projectile.owner, target.whoAmI);
            }
            petalCount = 0;
            Projectile.ai[2] = 1f;
            Projectile.netUpdate = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //樱色质感补层：粉瓣碎屑带一点叶绿
            for (int i = 0; i < 4; i++) {
                Color c = Main.rand.NextBool(4) ? LeafGreen : PetalPink;
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        protected override void PostDrawHead(Color lightColor, float headRotation, Rectangle frame, Vector2 origin) {
            if (petalCount <= 0) {
                return;
            }
            Texture2D body = CWRAsset.Fog?.Value;
            Texture2D core = CWRAsset.StarGlow01?.Value;
            if (body == null || core == null) {
                return;
            }
            //花瓣确定性公转：GameUpdateCount 推相位、identity 播种，绘制不掷 Main.rand
            for (int i = 0; i < petalCount; i++) {
                float ang = Main.GameUpdateCount * 0.045f + Seed + MathHelper.TwoPi * i / PetalCap;
                float radius = 32f + 4f * MathF.Sin(Main.GameUpdateCount * 0.07f + Seed + i * 1.3f);
                Vector2 at = Projectile.Center + ang.ToRotationVector2() * radius - Main.screenPosition;
                float petalRot = ang + MathHelper.PiOver2;
                //真 alpha 粉色椭圆瓣身（Fog 非等比压扁成瓣形）
                Main.EntitySpriteDraw(body, at, null, PetalPink * 0.85f, petalRot,
                    body.Size() / 2f, new Vector2(0.085f, 0.045f), SpriteEffects.None, 0);
                //加色亮心
                Color glint = PetalCore * 0.65f;
                glint.A = 0;
                Main.EntitySpriteDraw(core, at, null, glint, petalRot, core.Size() / 2f, 0.10f, SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 追踪花刃：ai[0]=目标 whoAmI；初段快出、中段转向追踪、尾段收速淡出（减速曲线），全程自旋；
    /// 自绘花瓣形：真 alpha 粉瓣身+加色亮心+残影
    /// </summary>
    internal class GsFlowerPowPetalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 80;
        private const int FadeInFrames = 4;
        private const int FadeOutFrames = 14;

        /// <summary>identity 播种的绘制相位与自旋方向</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = LifeFrames;
        }

        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                if (Projectile.timeLeft < FadeOutFrames) {
                    return Projectile.timeLeft / (float)FadeOutFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            //减速曲线：速度上限从 15 收到 7，绝不匀速直飞
            float speedCap = MathHelper.Lerp(15f, 7f, LifeT * LifeT);
            int targetId = (int)Projectile.ai[0];
            NPC target = targetId >= 0 && targetId < Main.maxNPCs ? Main.npc[targetId] : null;
            if (target != null && target.active && target.CanBeChasedBy(Projectile)) {
                //中段追踪：朝目标缓转向
                Vector2 desired = Projectile.Center.To(target.Center).SafeNormalize(Vector2.UnitX) * speedCap;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.085f);
            }
            else {
                //丢失目标：顺势滑行减速淡出
                Projectile.velocity *= 0.94f;
            }
            if (Projectile.velocity.Length() > speedCap) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speedCap;
            }
            //自旋方向按 identity 定死，两端一致
            Projectile.rotation += 0.27f * (Projectile.identity % 2 == 0 ? 1f : -1f);
            Lighting.AddLight(Projectile.Center, GsFlowerPowHead.PetalPink.ToVector3() * 0.16f * Opacity);
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.45f }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                Color c = Main.rand.NextBool(3) ? GsFlowerPowHead.LeafGreen : GsFlowerPowHead.PetalPink;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f), c, Main.rand.NextFloat(0.3f, 0.45f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D body = CWRAsset.Fog?.Value;
            Texture2D core = CWRAsset.StarGlow01?.Value;
            if (body == null || core == null) {
                return false;
            }
            float alpha = Opacity;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 petalScale = new(0.11f, 0.055f);

            //残影：oldPos 缓存逐级淡出
            for (int g = 1; g < Projectile.oldPos.Length; g++) {
                Vector2 gp = Projectile.oldPos[g];
                if (gp == Vector2.Zero) {
                    continue;
                }
                float fade = (1f - g / (float)Projectile.oldPos.Length) * 0.30f * alpha;
                Main.EntitySpriteDraw(body, gp + Projectile.Size / 2f - Main.screenPosition, null,
                    GsFlowerPowHead.PetalPink * fade, Projectile.oldRot[g],
                    body.Size() / 2f, petalScale * 0.9f, SpriteEffects.None, 0);
            }

            //真 alpha 粉瓣身（Fog 压扁成椭圆）
            Main.EntitySpriteDraw(body, pos, null, GsFlowerPowHead.PetalPink * (0.9f * alpha),
                Projectile.rotation, body.Size() / 2f, petalScale, SpriteEffects.None, 0);
            //叶绿细边：转 90 度的窄椭圆衬一线
            Main.EntitySpriteDraw(body, pos, null, GsFlowerPowHead.LeafGreen * (0.35f * alpha),
                Projectile.rotation + MathHelper.PiOver2, body.Size() / 2f,
                petalScale * new Vector2(0.5f, 0.7f), SpriteEffects.None, 0);
            //加色亮心，呼吸用 identity 播种
            float breathe = 0.85f + 0.15f * MathF.Sin(Main.GameUpdateCount * 0.13f + Seed);
            Color glint = GsFlowerPowHead.PetalCore * (0.7f * alpha * breathe);
            glint.A = 0;
            Main.EntitySpriteDraw(core, pos, null, glint, Projectile.rotation,
                core.Size() / 2f, 0.12f * breathe, SpriteEffects.None, 0);
            return false;
        }
    }
}
