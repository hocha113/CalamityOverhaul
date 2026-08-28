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
    /// 【圣辉附魔钢】材质：注满圣辉的附魔秘银。签名：①每拍斩切放出自绘光刃波，
    /// 出生过冲、减速回稳地飞行 ②终结拍光刃加宽 1.5 倍且贯穿三敌
    /// ③举刀时刀身微微悬浮，泛着离手的淡青浮光
    /// </summary>
    internal class GsEnchantedSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.EnchantedSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsEnchantedSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash hurls a blade of holy light; " +
            "the finisher's wave widens and pierces through three foes";

        //圣辉色板
        internal static readonly Color HolyBright = new(214, 244, 255); //圣辉青白
        internal static readonly Color HolyMain = new(96, 158, 235);    //附魔青蓝
        internal static readonly Color HolyHot = new(255, 226, 150);    //鎏金强调
        internal static readonly Color HolyDeep = new(18, 28, 56);      //深海蓝影

        //底乘 1.0：原版附魔剑本就每挥一道全伤剑气；重铸剑气降为 60% 底伤但终结拍
        //加宽穿三 + 终结近战 1.2x，远程期望约原版 95%~105%、贴身约 108%~115%，
        //综合 DPS 落在原版 100%~112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1f;
    }

    /// <summary>
    /// 圣辉附魔钢手持：三拍轻灵快剑。0/1 短起手全弧快扫，2 拉满弧贯穿终结；
    /// 每拍斩切爆发射出光刃波（压掉原版 EnchantedBeam，光刃全自绘）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsEnchantedSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.EnchantedSword;
        protected override Color EdgeBright => GsEnchantedSword.HolyBright;
        protected override Color BodyMain => GsEnchantedSword.HolyMain;
        protected override Color HotAccent => GsEnchantedSword.HolyHot;
        protected override Color DeepShadow => GsEnchantedSword.HolyDeep;

        //魔法质感：命中不喷原版血尘
        protected override bool BleedOnFlesh => false;
        protected override bool GlowAlways => true;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //贯穿终结：弧线拉满，光刃加宽穿透
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                    RaiseBack = 2.0f, Follow = 1.35f, ReachScale = 1.08f, LeanAmp = 0.06f,
                    DamageMult = 1.2f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
                };
            }
            //轻灵快扫：短起手、快斩、快收，音高上扬（魔法剑的清脆）
            return new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.6f, Follow = 1.1f, ReachScale = 1f, LeanAmp = 0.035f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? 0.15f : 0.05f,
            };
        }

        protected override void OnSlashBegin() {
            //每拍斩切爆发放出光刃波：终结拍加宽 1.5x 且穿 3（除回 DamageMult 取底伤摊账）
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            int beamDamage = Math.Max(1, (int)(baseDamage * 0.6f));
            Vector2 vel = baseAngle.ToRotationVector2() * 15.5f;
            SpawnOwnedProj(ModContent.ProjectileType<GsEnchantedSwordBeamProj>(),
                Vector2.Lerp(Hand, mainTip, 0.7f), vel, beamDamage, 2f, IsFinisher ? 1f : 0f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.45f, Pitch = IsFinisher ? -0.1f : 0.3f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //举相刀身洒落淡青光屑：离手浮空的魔法钢在呼吸
            if (phase is PhaseRaise or PhaseHold && Main.rand.NextBool(3)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f));
                PRTLoader.NewParticle<PRT_Sparkle>(at, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)),
                    GsEnchantedSword.HolyBright, Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(GsEnchantedSword.HolyMain, Main.rand.Next(12, 20), 0.05f, 0.8f);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //圣辉光屑迸溅替代血腥反馈
            int motes = IsFinisher ? 5 : 3;
            for (int i = 0; i < motes; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    GsEnchantedSword.HolyBright, Main.rand.NextFloat(0.2f, 0.34f))
                    ?.Configure(GsEnchantedSword.HolyMain, Main.rand.Next(14, 22), 0.08f, 0.9f);
            }
        }

        /// <summary>举相刀身微悬浮：只在绘制层做正弦浮动的淡青浮光轮，不碰 mainTip 判定</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            int phase = CurrentPhase;
            if (phase > PhaseHold) {
                return;
            }
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            //浮动量纯绘制偏移：identity 定相的确定性正弦，各端一致
            float hover = MathF.Sin(timer * 0.45f + DrawRand01(7) * 6.28f) * 2.5f;
            Vector2 drawPos = Hand + (mainAngle.ToRotationVector2() * mainReach * BladePark)
                + new Vector2(0f, hover) - Main.screenPosition;
            Color aura = GsEnchantedSword.HolyMain * 0.34f;
            aura.A = 0;
            sb.Draw(tex, drawPos, null, aura, mainAngle + rotOffset, tex.Size() / 2f, scale * 1.06f, effect, 0);
            Color rim = GsEnchantedSword.HolyBright * 0.2f;
            rim.A = 0;
            sb.Draw(tex, drawPos, null, rim, mainAngle + rotOffset, tex.Size() / 2f, scale * 1.12f, effect, 0);
        }
    }

    /// <summary>
    /// 光刃波：斩切爆发甩出的剑形圣光。ai[0]=1 为终结拍宽刃（1.5x 宽、穿 3）。
    /// 出生 1.15x 过冲缩回；速度先衰减后回稳巡航，全程不匀速；
    /// 刃体加色青蓝双层拉伸 + 短拖尾，绘制抖动全部 identity 播种
    /// </summary>
    internal class GsEnchantedSwordBeamProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool Widened => Projectile.ai[0] >= 1f;
        private float WidthMult => Widened ? 1.5f : 1f;
        private int Age => 90 - Projectile.timeLeft;

        /// <summary>出生过冲：前 6 帧 1.15x 缩回 1.0</summary>
        private float BirthSwell => Age < 6 ? MathHelper.Lerp(1.15f, 1f, Age / 6f) : 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            //终结宽刃首帧对齐形态（ai[0] 随生成包过线，各端一致）
            if (Age == 0 && Widened) {
                Projectile.penetrate = 3;
                Projectile.Resize(38, 38);
            }

            //飞行相速度戏：出膛 15.5 衰减到约 9，回稳后绕 10.5 呼吸巡航并轻微摆动（全程不匀速）
            if (Age < 18) {
                Projectile.velocity *= 0.968f;
            }
            else {
                float wobble = MathF.Sin((Age - 18) * 0.24f + Projectile.identity * 0.9f);
                float targetSpeed = 10.5f + wobble * 1.1f;
                float speed = Projectile.velocity.Length();
                Projectile.velocity *= MathHelper.Lerp(1f, targetSpeed / MathF.Max(speed, 0.01f), 0.08f);
                Projectile.velocity = Projectile.velocity.RotatedBy(wobble * 0.004f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer) {
                //刃尾洒光屑
                if (Age % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                        -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                        GsEnchantedSword.HolyMain, Main.rand.NextFloat(0.22f, 0.36f))
                        ?.Configure(false, Main.rand.Next(8, 14));
                }
                Lighting.AddLight(Projectile.Center, GsEnchantedSword.HolyMain.ToVector3() * 0.45f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中光屑迸溅
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                    Main.rand.NextBool(3) ? GsEnchantedSword.HolyHot : GsEnchantedSword.HolyBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f), GsEnchantedSword.HolyBright,
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(GsEnchantedSword.HolyMain, Main.rand.Next(10, 16), 0.06f, 0.8f);
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种）</summary>
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
            float rot = Projectile.rotation;
            float swell = BirthSwell;
            float w = WidthMult;

            //短拖尾：旧位残刃逐节缩淡
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                pos += Projectile.Size / 2f - Main.screenPosition;
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Color tail = GsEnchantedSword.HolyMain * (0.22f * k);
                tail.A = 0;
                Main.EntitySpriteDraw(star, pos, null, tail, Projectile.oldRot[i],
                    star.Size() * 0.5f, new Vector2(0.55f, 0.13f * w) * (k * swell), SpriteEffects.None, 0);
            }

            //刃体双层拉伸：外层青蓝宽刃 + 内层圣白锐芯（加色 A=0），identity 定相微呼吸
            float breath = 1f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + SegRand(3) * 6.28f);
            Color outer = GsEnchantedSword.HolyMain * 0.85f;
            outer.A = 0;
            Main.EntitySpriteDraw(star, center, null, outer, rot,
                star.Size() * 0.5f, new Vector2(0.62f, 0.2f * w) * (swell * breath), SpriteEffects.None, 0);
            Color inner = GsEnchantedSword.HolyBright * 0.95f;
            inner.A = 0;
            Main.EntitySpriteDraw(star, center, null, inner, rot,
                star.Size() * 0.5f, new Vector2(0.5f, 0.1f * w) * swell, SpriteEffects.None, 0);

            //鎏金锋尖：终结宽刃才点出的金色刃尖光
            if (Widened) {
                Vector2 tip = center + rot.ToRotationVector2() * (star.Width * 0.26f * swell);
                Color gold = GsEnchantedSword.HolyHot * 0.7f;
                gold.A = 0;
                Main.EntitySpriteDraw(glow, tip, null, gold, 0f, glow.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
