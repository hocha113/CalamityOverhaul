using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【蓝冰月刃】材质：寒潭魔冰锻成的月牙镰。签名：①每拍掷出全额自旋冰镰（原版每挥必发的保真，
    /// 初速 12、减速自旋、无 autoReuse 手感不动）②凝停冰晶：旋镰转到尽头不消散，凝成冰晶停在原地，
    /// 用挥砍打碎则向前炸出 5 枚冰棱扇 ③越慢转得越急的晶化变脆观感 + 冰雾拖尾
    /// </summary>
    internal class GsIceSickle : GsOdditiesComboScheme
    {
        public override int TargetItemID => ItemID.IceSickle;

        protected override int HeldProjID => ModContent.ProjectileType<GsIceSickleHeld>();

        protected override int ComboBeats => 3;

        protected override string GsDescFallback =>
            "Reforged: the thrown sickle freezes into an ice prism where it stops;\n" +
            "shatter the prism with a slash to burst a fan of icicles forward";

        //蓝冰色板（近似 GsIceBlade 四色自建，偏钢青一分）
        internal static readonly Color FrostWhite = new(210, 242, 255);  //霜白刃缘
        internal static readonly Color GlacialBlue = new(118, 178, 232); //蓝冰体色
        internal static readonly Color CoreBlue = new(160, 224, 255);    //冰芯亮蓝
        internal static readonly Color DeepFjord = new(16, 28, 46);      //深湾暗蓝

        //×1.05：每拍全额旋镰是原版保真不算增益；净增收益=凝停冰晶的碎晶扇
        //（5×0.4 伤 + 霜火 60 帧，要专门用挥砍点碎的条件收益），计入包络后底伤只小幅让利
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        /// <summary>
        /// 压掉原版挥舞的物理尾巴：held 每帧强撑 itemAnimation&gt;0 而本器 noMelee=false，
        /// ItemCheck 近战尾巴（挥舞碰撞箱直击+切割）在 owner 端仍会逐帧执行，不压则与 held 双份直击
        /// </summary>
        public override void GsUseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
            => noHitbox = true;
    }

    /// <summary>
    /// 蓝冰月刃手持：三拍。0/1 交替快斩，2 凝晶重斩（前压）。每拍斩切爆发掷全额旋镰。
    /// 魔冰质感：BleedOnFlesh 关闭，命中反馈全走碎冰。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsIceSickleHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.IceSickle;
        protected override Color EdgeBright => GsIceSickle.FrostWhite;
        protected override Color BodyMain => GsIceSickle.GlacialBlue;
        protected override Color HotAccent => GsIceSickle.CoreBlue;
        protected override Color DeepShadow => GsIceSickle.DeepFjord;

        /// <summary>原版 scale 1.15 的大刃，触及略放</summary>
        protected override float BaseReach => 124f;

        /// <summary>魔冰法刃不喷血，命中反馈全走碎冰</summary>
        protected override bool BleedOnFlesh => false;

        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsIceSickle.GlacialBlue, 0.22f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsIceSickle.CoreBlue : GsIceSickle.FrostWhite;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //凝晶重斩：重弧前压
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 5, Recover = 12,
                    RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.15f, LeanAmp = 0.08f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = -0.18f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Raise = stage == 0 ? 6 : 5;
            b.Recover = 10;
            b.DamageMult = 0.95f;
            b.SwingPitch = stage == 0 ? 0.08f : 0.16f; //冰刃音色偏亮
            return b;
        }

        /// <summary>斩切爆发：掷全额旋镰（原版保真）+ 扫描点碎前方晶化旋镰</summary>
        protected override void OnSlashBegin() {
            Vector2 aim = baseAngle.ToRotationVector2();
            int spinType = ModContent.ProjectileType<GsIceSickleSpinProj>();
            //原版每挥必发的保真：全额伤害、初速 12
            SpawnOwnedProj(spinType, Hand + aim * 24f, aim * 12f, Projectile.damage, Projectile.knockBack);

            //碎晶触发：owner 记账，斩击落点（手前 90px）170px 内的晶化旋镰标 ai[1]=1 过线，下一帧自炸冰棱扇
            if (Projectile.owner == Main.myPlayer) {
                Vector2 focus = Hand + aim * 90f;
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.owner == Projectile.owner && p.type == spinType
                        && p.ai[0] == 1f && p.ai[1] == 0f && p.Distance(focus) <= 170f) {
                        p.ai[1] = 1f;
                        p.netUpdate = true;
                    }
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.45f, Pitch = 0.25f }, Owner.Center);
            }
        }

        /// <summary>挥弧沿途飘散冰雾（已在非服务器端调用）</summary>
        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase != PhaseSlash || !Main.rand.NextBool(2)) {
                return;
            }
            Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.45f, 1f)),
                DustID.IceTorch, Vector2.Zero, 110, default, Main.rand.NextFloat(0.8f, 1.2f));
            d.noGravity = true;
            d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 1.4f;
        }

        /// <summary>命中碎冰迸溅（血尘已由 BleedOnFlesh=false 关掉）</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int shards = IsFinisher ? 9 : 5;
            for (int i = 0; i < shards; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Ice,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 60, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 自旋冰镰：原版 263 保真（34px、穿透 4、撞墙亡、冰伤、timeLeft 180、idStatic 8、×0.95 减速），
    /// 自旋越慢转得越急（晶化变脆观感）。签名「凝停冰晶」：速度低于 0.4 时定格为冰晶
    /// （ai[0]=1，各端同式判定 + netUpdate 对齐；ai[2] 存晶化朝向），晶化态不判伤、90 帧自然碎裂只演出；
    /// 被挥砍标记 ai[1]=1 则 owner 端向最近敌或存向炸 5 枚冰棱扇。
    /// 自绘：原版贴图垫底 + 3 拍旋转残影 + 结霜缘，晶化加结霜脉动与棱面闪点（identity 播种）
    /// </summary>
    internal class GsIceSickleSpinProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.IceSickle");

        /// <summary>自旋方向（首帧按横速符号定，各端同式）</summary>
        private int spinDir;

        private bool IsCrystal => Projectile.ai[0] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            //镜像原版 263：Projectile.cs SetDefaults
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 180;
            Projectile.coldDamage = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 8;
        }

        /// <summary>原版 aiStyle 106（含 263）不切物块，照封</summary>
        public override bool? CanCutTiles() => false;

        /// <summary>晶化态不判伤</summary>
        public override bool? CanDamage() => IsCrystal ? false : null;

        public override void AI() {
            if (spinDir == 0) {
                spinDir = Projectile.velocity.X >= 0f ? 1 : -1;
            }
            if (IsCrystal) {
                CrystalAI();
                return;
            }

            //飞行：×0.95 减速（原版保真）；自旋越慢转得越急=晶化变脆观感
            Projectile.rotation += spinDir * (0.15f + 0.55f * (1f - Projectile.timeLeft / 180f));
            Projectile.velocity *= 0.95f;
            Lighting.AddLight(Projectile.Center, GsIceSickle.GlacialBlue.ToVector3() * 0.4f);

            //冰雾拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                    -Projectile.velocity * 0.1f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }

            //凝停：慢到临界即定格成冰晶。速度衰减各端确定同步，同式就地翻转，netUpdate 对齐掉队者
            if (Projectile.velocity.Length() < 0.4f) {
                Projectile.ai[0] = 1f;
                Projectile.ai[2] = Projectile.velocity.ToRotation(); //存晶化朝向，碎晶无敌可寻时用
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = 90;
                Projectile.netUpdate = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.55f }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                            Main.rand.NextVector2Circular(1.2f, 1.2f), 120, default, Main.rand.NextFloat(0.7f, 1f));
                        d.noGravity = true;
                    }
                }
            }
        }

        private void CrystalAI() {
            //被斩击点碎：owner 端炸冰棱扇，各端随后走 Kill 的碎冰演出
            if (Projectile.ai[1] == 1f) {
                if (Projectile.owner == Main.myPlayer) {
                    ShatterIntoShards();
                }
                Projectile.Kill();
                return;
            }
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, GsIceSickle.CoreBlue.ToVector3() * 0.32f);
            //表面霜气缓升
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 12f),
                    DustID.IceTorch, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f), 130, default,
                    Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        /// <summary>碎晶扇：朝最近敌（600px 内）否则存下的晶化朝向，扇形 5 枚各 0.4 伤（只在 owner 端被调）</summary>
        private void ShatterIntoShards() {
            float aim = Projectile.ai[2];
            NPC target = null;
            float best = 600f;
            foreach (NPC n in Main.ActiveNPCs) {
                if (!n.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Distance(n.Center);
                if (dist < best) {
                    best = dist;
                    target = n;
                }
            }
            if (target != null) {
                aim = (target.Center - Projectile.Center).ToRotation();
            }
            int dmg = Math.Max(1, (int)(Projectile.damage * 0.4f));
            int type = ModContent.ProjectileType<GsIceSickleCrystalProj>();
            for (int i = -2; i <= 2; i++) {
                Vector2 vel = (aim + i * 0.21f).ToRotationVector2() * 9f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    type, dmg, 1f, Projectile.owner);
            }
        }

        /// <summary>碎冰演出：打碎与 90 帧自然碎裂共用（自然碎裂只演出无弹幕）</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.1f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5.5f), 60, default,
                    Main.rand.NextFloat(0.8f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsIceSickle.CoreBlue, 0.2f)
                ?.Configure(9, 0.75f);
        }

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种，禁 Main.rand）</summary>
        private float SeedRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.IceSickle);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.IceSickle].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 at = Projectile.Center - Main.screenPosition;

            if (!IsCrystal) {
                //3 拍旋转残影（oldRot/oldPos 渐淡）
                for (int i = 5; i >= 1; i -= 2) {
                    Color ghost = GsIceSickle.FrostWhite * (0.26f * (1f - i / 6f));
                    ghost.A = 0;
                    Main.EntitySpriteDraw(tex, Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition,
                        null, ghost, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
                //本体：镜像原版 GetAlpha(263) 的 timeLeft 渐隐
                float bodyAlpha = MathHelper.Clamp(Projectile.timeLeft / 255f, 0f, 1f);
                Main.EntitySpriteDraw(tex, at, null, lightColor * bodyAlpha, Projectile.rotation,
                    origin, Projectile.scale, SpriteEffects.None, 0);
                //结霜缘（加色）
                Color rim = GsIceSickle.GlacialBlue * (0.22f * bodyAlpha);
                rim.A = 0;
                Main.EntitySpriteDraw(tex, at, null, rim, Projectile.rotation,
                    origin, Projectile.scale * 1.06f, SpriteEffects.None, 0);
                return false;
            }

            //晶化态：软光芯 + 定格本体 + 结霜层脉动 + 棱面闪点（identity 播种错相）
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + SeedRand01(2) * MathHelper.TwoPi);
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                Color halo = GsIceSickle.GlacialBlue * (0.30f * pulse);
                halo.A = 0;
                Main.EntitySpriteDraw(glowTex, at, null, halo, 0f, glowTex.Size() / 2f, 0.55f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, at, null, Color.Lerp(lightColor, GsIceSickle.FrostWhite, 0.35f),
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            Color frost = GsIceSickle.FrostWhite * (0.30f + 0.18f * pulse);
            frost.A = 0;
            Main.EntitySpriteDraw(tex, at, null, frost, Projectile.rotation,
                origin, Projectile.scale * 1.05f, SpriteEffects.None, 0);

            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                for (int k = 0; k < 2; k++) {
                    float glint = MathF.Sin((Main.GlobalTimeWrappedHourly * 5f) + (SeedRand01(k + 4) * MathHelper.TwoPi));
                    if (glint <= 0.2f) {
                        continue;
                    }
                    Vector2 facet = at + ((SeedRand01(k + 7) * MathHelper.TwoPi) + Projectile.rotation).ToRotationVector2() * 9f;
                    Color spec = GsIceSickle.FrostWhite * (0.7f * glint);
                    spec.A = 0;
                    Main.EntitySpriteDraw(star, facet, null, spec, 0f, star.Size() / 2f,
                        new Vector2(0.05f, 0.11f) * glint, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 碎晶冰棱：晶化旋镰被斩碎时的扇形弹（0.4 伤、初速 9、轻重力、穿透 1、命中挂霜火 60）。
    /// 自绘小冰棱=StarTexture 纵笔+微光+残影链（画法对标 GsIceBladeShardProj），加色批全 A=0
    /// </summary>
    internal class GsIceSickleCrystalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.IceSickle");

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
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
            Projectile.coldDamage = true;
        }

        public override void AI() {
            //轻重力，尖头顺速度
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.12f, 10f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, GsIceSickle.GlacialBlue.ToVector3() * 0.22f);
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                    -Projectile.velocity * 0.06f, 120, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 60);

        /// <summary>碎晶主响已在旋镰 OnKill，冰棱只出屑不叠音</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f), 60, default,
                    Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SeedRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (star == null || glow == null) {
                return false;
            }
            Vector2 origin = star.Size() / 2f;

            //残影链：旧位置渐淡小晶
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 at2 = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trail = GsIceSickle.FrostWhite * (0.20f * (1f - i / (float)Projectile.oldPos.Length));
                trail.A = 0;
                Main.EntitySpriteDraw(star, at2, null, trail, Projectile.oldRot[i],
                    origin, new Vector2(0.04f, 0.12f), SpriteEffects.None, 0);
            }

            Vector2 at = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 8f) + (SeedRand01(1) * 6.28f));

            //软光芯
            Color core = GsIceSickle.CoreBlue * (0.5f * pulse);
            core.A = 0;
            Main.EntitySpriteDraw(glow, at, null, core, 0f, glow.Size() / 2f, 0.4f, SpriteEffects.None, 0);
            //冰棱纵笔 + 横短笔
            Color body = Color.Lerp(GsIceSickle.GlacialBlue, GsIceSickle.FrostWhite, 0.5f) * pulse;
            body.A = 0;
            Main.EntitySpriteDraw(star, at, null, body, Projectile.rotation,
                origin, new Vector2(0.055f, 0.17f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, at, null, body * 0.75f, Projectile.rotation + MathHelper.PiOver2,
                origin, new Vector2(0.04f, 0.09f), SpriteEffects.None, 0);
            //霜白高光点
            Color spec = GsIceSickle.FrostWhite * (0.85f * pulse);
            spec.A = 0;
            Main.EntitySpriteDraw(star, at, null, spec, Projectile.rotation,
                origin, new Vector2(0.025f, 0.07f), SpriteEffects.None, 0);
            return false;
        }
    }
}
