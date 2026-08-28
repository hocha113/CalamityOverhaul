using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·帕武道】阴阳玉太极锤：墨黑皓白双相、玉青点缀。签名行为：①逐掷交替阴阳两相，锤头与链条随相冷暖
    /// ②阴击挂阴印并迟缓目标 ③阳击命中带印目标清印引爆阴阳环
    /// </summary>
    internal class GsDaoofPow : GsFlailScheme
    {
        public override int TargetItemID => ItemID.DaoofPow;

        protected override int FlailProjType => ModContent.ProjectileType<GsDaoofPowHead>();

        protected override string GsDescFallback =>
            "Reforged: throws alternate between Yin and Yang; a Yin strike brands and slows the target" +
            "\nA Yang strike on a branded target detonates the brand into a burst of light and ink";

        //签名机制（印记引爆 75% 小 AOE）占预算大头，底伤只补半成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        /// <summary>阴印持续帧数（6 秒）</summary>
        private const int MarkFrames = 360;

        /// <summary>下一掷是否为阳相；方案跨玩家共享单例，只在 myPlayer 守门路径翻转</summary>
        private bool yangNext;

        /// <summary>阴印计时表 npc.whoAmI→剩余帧；只在 myPlayer 守门路径读写</summary>
        private readonly Dictionary<int, int> yinMarks = [];

        protected override float LaunchAi2(Player player, int index) {
            if (player.whoAmI != Main.myPlayer) {
                return 0f;//GsShoot 只在 owner 端跑，此分支纯防御
            }
            bool yang = yangNext;
            yangNext = !yangNext;
            return yang ? 1f : 0f;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer || yinMarks.Count == 0) {
                return;
            }
            //阴印衰减：过期或目标失效即除名
            foreach (int key in new List<int>(yinMarks.Keys)) {
                if (--yinMarks[key] <= 0 || !Main.npc[key].active) {
                    yinMarks.Remove(key);
                }
            }
        }

        /// <summary>挂阴印（owner 端调用）</summary>
        internal void MarkYin(NPC npc) => yinMarks[npc.whoAmI] = MarkFrames;

        /// <summary>目标带印则清印返回 true（owner 端调用）</summary>
        internal bool TryConsumeMark(NPC npc) => yinMarks.Remove(npc.whoAmI);
    }

    /// <summary>
    /// 帕武道锤头。ai[2]：0=阴 1=阳（随生成包过线，全端可读）；
    /// 阴相罩墨影加玉青缘光，阳相罩皓白炽层；命中逻辑全在 owner 端 OnHeadHit
    /// </summary>
    internal class GsDaoofPowHead : GsFlailHeadProj
    {
        /// <summary>墨黑</summary>
        internal static readonly Color InkBlack = new(38, 34, 46);
        /// <summary>皓白</summary>
        internal static readonly Color PureWhite = new(240, 244, 240);
        /// <summary>玉青</summary>
        internal static readonly Color JadeGreen = new(112, 205, 168);

        public override int SourceItemID => ItemID.DaoofPow;
        public override int VanillaProjID => ProjectileID.TheDaoofPow;
        //跟随原版弹幕 63 的链贴图 Chain7（brief 写的 154/Chain13 是肉丸的，查 TML 源纠正）
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain7;
        public override Color GlowColor => JadeGreen;

        public override float MaxChainLength => 340f;

        /// <summary>本掷是否阳相</summary>
        private bool IsYang => WeaponAi2 >= 0.5f;

        /// <summary>identity 播种的绘制相位，抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        protected override void OnSpinTick(float charge) {
            //高转速时按相位甩出碎屑：阴掷墨烟、阳掷白尘
            if (VaultUtils.isServer || charge <= 0.55f || spinTimer % 7 != 0) {
                return;
            }
            if (IsYang) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch,
                    Main.rand.NextVector2Circular(1.4f, 1.4f), 100, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
            else {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    Main.rand.NextVector2Circular(1.4f, 1.4f), 170, InkBlack, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
            }
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || Owner.whoAmI != Main.myPlayer
                || !GodSmithScheme.TryGetScheme(SourceItemID, out var s) || s is not GsDaoofPow scheme) {
                return;
            }
            if (!IsYang) {
                //阴击：挂印+迟缓，墨黑命中雾
                scheme.MarkYin(target);
                target.AddBuff(BuffID.Slow, 120);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustPerfect(target.Center, DustID.Smoke,
                            Main.rand.NextVector2Circular(2.6f, 2.6f), 170, InkBlack, Main.rand.NextFloat(1.2f, 1.8f));
                        d.noGravity = true;
                    }
                }
                return;
            }
            //阳击带印：清印引爆阴阳环（75% 小 AOE）
            if (scheme.TryConsumeMark(target)) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsDaoofPowBurstProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.75f)), 3f, Projectile.owner);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.5f }, target.Center);
                }
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //相色补层：阴击玉青、阳击皓白
            Color c = IsYang ? PureWhite : JadeGreen;
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(4f, 4f), c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override Color ChainLinkColor(int linkIndex, float t, Color light)
            //链条随相冷暖：阳相近头处泛暖白、阴相压墨青
            => IsYang
                ? Color.Lerp(light, PureWhite, 0.30f * t)
                : Color.Lerp(light, InkBlack, 0.30f * t);

        protected override void PostDrawHead(Color lightColor, float headRotation, Rectangle frame, Vector2 origin) {
            Texture2D veil = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (veil == null || glow == null) {
                return;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float pulse = 0.9f + 0.1f * MathF.Sin(Main.GameUpdateCount * 0.09f + Seed);
            if (IsYang) {
                //阳相：皓白炽层（加色 A=0）+ 玉青光晕 + 太极异色眼（墨点真 alpha）
                Texture2D head = TextureAssets.Projectile[VanillaProjID].Value;
                Color blaze = PureWhite * (0.55f * pulse);
                blaze.A = 0;
                Main.EntitySpriteDraw(head, pos, frame, blaze, headRotation, origin,
                    Projectile.scale * 1.05f, SpriteEffects.None, 0);
                Color halo = JadeGreen * 0.25f;
                halo.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, halo, 0f, glow.Size() / 2f, 0.5f * pulse, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(veil, pos, null, InkBlack * 0.85f, 0f, veil.Size() / 2f, 0.05f, SpriteEffects.None, 0);
            }
            else {
                //阴相：墨影罩层（真 alpha 压暗）+ 玉青缘光 + 太极异色眼（白点加色）
                Main.EntitySpriteDraw(veil, pos, null, InkBlack * (0.62f * pulse), 0f,
                    veil.Size() / 2f, 0.30f, SpriteEffects.None, 0);
                Color rim = JadeGreen * 0.35f;
                rim.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, rim, 0f, glow.Size() / 2f, 0.34f * pulse, SpriteEffects.None, 0);
                Color eye = PureWhite * 0.8f;
                eye.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, eye, 0f, glow.Size() / 2f, 0.08f, SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 阴阳环爆：白炽外环与墨圈内层双相扩张，早窗结伤后余辉淡出；
    /// 全程自绘，绘制相位用 identity 播种
    /// </summary>
    internal class GsDaoofPowBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 26;
        /// <summary>只在扩张早窗结伤</summary>
        private const int DamageWindow = 10;

        private float Seed => Projectile.identity * 0.917f;
        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;
        /// <summary>环半径：先快后慢的扩张曲线</summary>
        private float RingRadius => MathHelper.Lerp(14f, 78f, 1f - (1f - LifeT) * (1f - LifeT));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;//一环对同一目标只结一次
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI()
            => Lighting.AddLight(Projectile.Center, GsDaoofPowHead.JadeGreen.ToVector3() * (0.5f * (1f - LifeT)));

        public override bool? CanDamage() => Projectile.timeLeft > LifeFrames - DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= RingRadius;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D veil = CWRAsset.Extra_98?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (ring == null || veil == null || star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = 1f - LifeT;
            float diameter = RingRadius * 2f;

            //白炽外环（加色 A=0）
            Color white = GsDaoofPowHead.PureWhite * (0.85f * fade);
            white.A = 0;
            Main.EntitySpriteDraw(ring, pos, null, white, Seed, ring.Size() / 2f,
                diameter / ring.Width, SpriteEffects.None, 0);
            //玉青余环，略大略淡反向旋
            Color jade = GsDaoofPowHead.JadeGreen * (0.40f * fade);
            jade.A = 0;
            Main.EntitySpriteDraw(ring, pos, null, jade, -Seed, ring.Size() / 2f,
                diameter * 1.15f / ring.Width, SpriteEffects.None, 0);
            //墨圈内层（真 alpha 压暗，滞后半拍）
            Main.EntitySpriteDraw(veil, pos, null, GsDaoofPowHead.InkBlack * (0.55f * fade), Seed * 0.5f,
                veil.Size() / 2f, diameter * 0.62f / veil.Width, SpriteEffects.None, 0);
            //中心四芒闪（加色，只在前段）
            if (LifeT < 0.35f) {
                Color flash = Color.White * (0.7f * (1f - LifeT / 0.35f));
                flash.A = 0;
                Main.EntitySpriteDraw(star, pos, null, flash, Seed, star.Size() / 2f,
                    0.26f * (1f - LifeT * 0.5f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
