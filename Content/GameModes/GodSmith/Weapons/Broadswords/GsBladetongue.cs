using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【腐蚀之吻·刃舌】材质：绯红教团养出的活体舌刃，刀身会渗灵液、命中会「亲吻」。
    /// 签名：①近战命中喷出双股灵液液束（重力下坠的金色液滴，命中降防）
    /// ②终结拍命中改在原地立起灵液间歇泉，驻场短喷柱逐跳蚀甲 ③挥舞全程渗灵液金珠，血肉命中带血尘
    /// </summary>
    internal class GsBladetongue : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Bladetongue;

        protected override int HeldProjID => ModContent.ProjectileType<GsBladetongueHeld>();

        protected override string GsDescFallback =>
            "Reforged: a living crimson blade; striking foes spits twin arcing streams of ichor that lower defense, " +
            "and the finishing beat's kiss leaves a lingering ichor geyser erupting on the spot";

        //血肉灵液色板
        internal static readonly Color FleshBright = new(255, 168, 160); //苍肉粉刃缘
        internal static readonly Color FleshMain = new(196, 60, 70);     //绯红活体色
        internal static readonly Color IchorGold = new(255, 212, 84);    //灵液金
        internal static readonly Color FleshDeep = new(40, 10, 16);      //腐肉暗红

        //底伤不加成（原版 55/28f 近战命中喷 0.5x 灵液流）：刀身拍均 1.02x；命中一拍一次喷
        //双股灵液束（0.25x×2 重力下坠溅射周边），终结拍改原地间歇泉（0.15x/跳、至多 3 跳）；
        //按三拍循环约 72 帧摊算，综合单体约原版 102%~118%，灵液降防是额外互惠收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 腐蚀之吻手持：三拍。0 舔斩 / 1 回舌斩 / 2 深吻重斩（前压+间歇泉）。
    /// 命中上灵液 buff，一拍一次喷灵液；活体刃全程渗金珠。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBladetongueHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Bladetongue;
        protected override Color EdgeBright => GsBladetongue.FleshBright;
        protected override Color BodyMain => GsBladetongue.FleshMain;
        protected override Color HotAccent => GsBladetongue.IchorGold;
        protected override Color DeepShadow => GsBladetongue.FleshDeep;

        //活体暗肉色吸光
        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, GsBladetongue.FleshDeep, 0.18f);
        protected override bool GlowAlways => IsFinisher;
        protected override Color GlowColor => GsBladetongue.IchorGold;

        /// <summary>一拍只喷一次灵液</summary>
        private bool spitFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 舔斩
            0 => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.12f,
            },
            //拍1 回舌斩
            1 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 1.95f, Follow = 1.1f, ReachScale = 1.02f, LeanAmp = 0.055f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍2 深吻重斩：前压
            _ => new GsBroadBeat {
                Raise = 9, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.25f, Follow = 1.25f, ReachScale = 1.12f, LeanAmp = 0.085f,
                DamageMult = 1.15f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.3f,
            },
        };

        //==================== 活体演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //深吻起手：湿滑的低啸
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = -0.4f }, Owner.Center);
            }
        }

        /// <summary>命中上灵液；一拍一次喷液：普通拍双股液束，终结拍原地间歇泉</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI == Main.myPlayer) {
                target.AddBuff(BuffID.Ichor, 240);
            }
            if (spitFired) {
                return;
            }
            spitFired = true;
            if (IsFinisher) {
                //腐蚀之吻：在目标脚下立起灵液间歇泉
                SpawnOwnedProj(ModContent.ProjectileType<GsBladetongueGeyserProj>(),
                    target.Bottom, Vector2.Zero, Math.Max(1, (int)(Projectile.damage * 0.15f)), 0.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
                }
            }
            else {
                //顺挥砍切线喷出双股液束，带着上抛的弧
                Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.25f));
                for (int i = -1; i <= 1; i += 2) {
                    SpawnOwnedProj(ModContent.ProjectileType<GsBladetongueSpitProj>(),
                        target.Center, (tangent.RotatedBy(i * 0.32f) * 6.5f) - (Vector2.UnitY * 2.2f), dmg, 0.8f);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.45f, Pitch = 0.25f }, target.Center);
                }
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //活体刃渗灵液：金珠自刃身滴落（斩切期甩得更急）
            if (Main.rand.NextBool(phase == PhaseSlash ? 2 : 7)) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f)),
                    DustID.Ichor, Vector2.Zero, 30, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.velocity = phase == PhaseSlash
                    ? (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f)
                    : new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f));
                d.noGravity = false;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //灵液溅射：金珠外迸后下坠
            int drops = IsFinisher ? 8 : 4;
            for (int i = 0; i < drops; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Ichor,
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3.5f, -1f)),
                    30, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
        }
    }

    /// <summary>
    /// 灵液液束：命中喷出的金色液滴，重力下坠划出液弧；弹体沿速度拉伸、金珠拖尾，
    /// 命中降防、落地溅开。自绘：软光芯+顺速度长滴形
    /// </summary>
    internal class GsBladetongueSpitProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Life++;
            //液滴弧线：重力下坠，横速微阻
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.32f, 12f);
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsBladetongue.IchorGold.ToVector3() * 0.25f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Ichor,
                    -Projectile.velocity * 0.05f, 60, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Ichor, 180);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //溅开：金珠四散落地
            SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.25f, Pitch = 0.45f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Ichor,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2.5f, -0.5f)),
                    30, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
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
            //金珠拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size * 0.5f) - Main.screenPosition;
                float t = 1f - (i / (float)Projectile.oldPos.Length);
                Color bead = GsBladetongue.IchorGold * (0.2f * t);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f, 0.14f * t, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float wobble = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + SegRand(1) * 6.28f);

            //软光芯
            Color core = GsBladetongue.IchorGold * (0.5f * wobble);
            core.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() * 0.5f, 0.3f, SpriteEffects.None, 0);

            //长滴形液体：顺速度拉长的亮滴（越快越长）
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() / 9f, 0.6f, 1.4f);
            Color body = Color.Lerp(GsBladetongue.IchorGold, GsBladetongue.FleshBright, 0.25f) * wobble;
            body.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, body, Projectile.rotation,
                star.Size() * 0.5f, new Vector2(0.045f, 0.1f * stretch), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 灵液间歇泉：深吻命中处立起的驻场喷柱。周期 16 帧一涌，柱高随涌势起伏；
    /// 逐跳蚀甲（命中冷却 14），金珠沿柱身上滚、顶端冠溅、基底积液。
    /// 绘制全走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsBladetongueGeyserProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 48;
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>涌势：每 16 帧一涌，涌后指数回落</summary>
        private float SpurtPulse {
            get {
                float phase = (Life % 16f) / 16f;
                return 0.78f + 0.28f * MathF.Exp(-phase * 3f);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f) {
                //生成点是目标脚底：柱体向上立起
                Projectile.Center += new Vector2(0f, -Projectile.height * 0.5f + 8f);
                Projectile.netUpdate = true;
            }
            //每次涌起的轻声喷溅
            if (!VaultUtils.isServer && Life % 16f == 2f) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.3f, Pitch = 0.1f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, GsBladetongue.IchorGold.ToVector3() * 0.4f);

            if (!VaultUtils.isServer) {
                //柱身金珠上涌 + 冠顶液滴回落
                if (Main.rand.NextBool(2)) {
                    Vector2 at = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-8f, 8f), -4f);
                    Dust d = Dust.NewDustPerfect(at, DustID.Ichor,
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-6f, -3f)),
                        30, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    Vector2 at = Projectile.Top + new Vector2(Main.rand.NextFloat(-12f, 12f), 6f);
                    Dust d = Dust.NewDustPerfect(at, DustID.Ichor,
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1f, 0.5f)),
                        30, default, Main.rand.NextFloat(0.7f, 1f));
                    d.noGravity = false;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                target.AddBuff(BuffID.Ichor, 180);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //泉息：最后一捧金珠塌落
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), -6f),
                    DustID.Ichor, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -0.5f)),
                    30, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D blot = CWRAsset.Extra_98?.Value;
            if (glow == null || star == null || blot == null) {
                return false;
            }
            Vector2 basePos = Projectile.Bottom - Main.screenPosition;
            float fadeIn = MathHelper.Clamp(Life / 5f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            float k = fadeIn * fadeOut;
            float pulse = SpurtPulse;
            float height = 88f * pulse * fadeIn;

            //基底湿痕：真 alpha 暗斑打湿地面
            Color stain = GsBladetongue.FleshDeep * (0.4f * k);
            Main.EntitySpriteDraw(blot, basePos + new Vector2(0f, 2f), null, stain, 0f,
                blot.Size() * 0.5f, new Vector2(0.2f, 0.05f), SpriteEffects.None, 0);
            //基底积液
            Color pool = GsBladetongue.IchorGold * (0.42f * k);
            pool.A = 0;
            Main.EntitySpriteDraw(glow, basePos, null, pool, 0f, glow.Size() * 0.5f,
                new Vector2(0.42f, 0.14f), SpriteEffects.None, 0);

            //柱心亮束：底粗顶细的立式光带
            Color column = GsBladetongue.IchorGold * (0.55f * k * pulse);
            column.A = 0;
            Main.EntitySpriteDraw(star, basePos - new Vector2(0f, height * 0.5f), null, column,
                MathHelper.PiOver2, star.Size() * 0.5f,
                new Vector2(0.075f, height / star.Height * 1.05f), SpriteEffects.None, 0);

            //金珠沿柱身上滚：相位滚动、底粗顶细、微微摆动
            for (int i = 0; i < 7; i++) {
                float slot = (i / 7f + Life * 0.028f + SegRand(i) * 0.13f) % 1f;
                float sway = MathF.Sin((slot * 9f) + (i * 1.7f)) * 5f * (1f - slot * 0.6f);
                Vector2 at = basePos + new Vector2(sway, -slot * height);
                float size = MathHelper.Lerp(0.24f, 0.1f, slot);
                Color bead = Color.Lerp(GsBladetongue.IchorGold, GsBladetongue.FleshBright, 0.2f * SegRand(i + 30)) * (0.5f * k);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f, size, SpriteEffects.None, 0);
            }

            //冠溅：顶端三粒液珠外抛
            for (int i = -1; i <= 1; i++) {
                float t = (Life * 0.06f + SegRand(i + 50) * 0.8f) % 1f;
                Vector2 at = basePos + new Vector2(i * (8f + 14f * t), -height - 6f + (t * t * 18f));
                Color drop = GsBladetongue.IchorGold * (0.45f * k * (1f - t));
                drop.A = 0;
                Main.EntitySpriteDraw(glow, at, null, drop, 0f, glow.Size() * 0.5f, 0.1f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
