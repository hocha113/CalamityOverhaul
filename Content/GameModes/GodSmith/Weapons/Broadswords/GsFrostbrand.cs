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
    /// 【凛冬呼吸·霜痕剑】材质：冰霜巨人肺腑里淬出的霜痕剑，每一斩都是一次吐息。
    /// 签名：①每一斩呼出一枚霜弹（晶芯彗体+冰雾拖尾），飞行先滞后涌、绝不匀速
    /// ②命中叠霜火，终结拍霜弹化作三叉冰片扇 ③终结拍蓄力时寒雾倒吸入刃，命中碎冰迸溅无血尘
    /// </summary>
    internal class GsFrostbrand : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Frostbrand;

        protected override int HeldProjID => ModContent.ProjectileType<GsFrostbrandHeld>();

        protected override string GsDescFallback =>
            "Reforged: winter's breath; every slash exhales a frost bolt that stalls and surges in flight, " +
            "hits inflict Frostburn, and the finishing beat fans the bolt into three ice shards";

        //凛冬色板
        internal static readonly Color RimeBright = new(222, 244, 255); //霜白刃缘
        internal static readonly Color RimeMain = new(96, 144, 216);    //冰渊蓝体色
        internal static readonly Color RimeHot = new(146, 236, 226);    //极光青强调
        internal static readonly Color RimeDeep = new(14, 24, 44);      //深渊垫影

        //底伤不加成（原版 49/useAnim23 每挥一发全伤霜弹）：刀身拍均 1.03x + 霜弹 0.75x
        //（终结拍换 0.45x×3 三叉扇，散射难全中），按三拍循环约 66 帧摊算，
        //贴脸（刀+弹齐中）约原版 108%、纯刃外约 109%，霜火叠加是持续小补
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 凛冬呼吸手持：三拍。0 呼斩 / 1 吸斩（音调回升）/ 2 凛冬吐息
    /// （长举倒吸寒雾+前压+三叉冰扇）。每拍斩切爆发呼出霜弹。
    /// 魔法霜质：BleedOnFlesh 关闭。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsFrostbrandHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Frostbrand;
        protected override Color EdgeBright => GsFrostbrand.RimeBright;
        protected override Color BodyMain => GsFrostbrand.RimeMain;
        protected override Color HotAccent => GsFrostbrand.RimeHot;
        protected override Color DeepShadow => GsFrostbrand.RimeDeep;

        /// <summary>魔法霜刃不喷血，命中反馈全走碎冰</summary>
        protected override bool BleedOnFlesh => false;

        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsFrostbrand.RimeMain, 0.2f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsFrostbrand.RimeHot : GsFrostbrand.RimeBright;

        private bool boltFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 呼斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 吸斩：略快，音调回升
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.05f,
            },
            //拍2 凛冬吐息：长举倒吸、前压重斩
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = -0.25f,
            },
        };

        //==================== 吐息演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //吐息爆发：冰咒低鸣
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.5f, Pitch = -0.3f }, Owner.Center);
            }
        }

        /// <summary>斩切爆发呼出霜弹：普通拍单发，终结拍 ±0.22 弧度三叉扇</summary>
        protected override void OnSlashBegin() {
            if (boltFired) {
                return;
            }
            boltFired = true;
            int type = ModContent.ProjectileType<GsFrostbrandBoltProj>();
            Vector2 aim = baseAngle.ToRotationVector2();
            if (IsFinisher) {
                SetFlash(6);
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.45f));
                for (int i = -1; i <= 1; i++) {
                    SpawnOwnedProj(type, Hand + aim * 26f, aim.RotatedBy(i * 0.22f) * 13f, dmg, 1.5f, 1f);
                }
            }
            else {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.75f));
                SpawnOwnedProj(type, Hand + aim * 26f, aim * 13f, dmg, 1.5f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.5f, Pitch = 0.15f }, Owner.Center);
            }
        }

        /// <summary>近战命中叠霜火（原版霜弹只有弹体附伤，这里刀身也咬霜）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            target.AddBuff(BuffID.Frostburn, IsFinisher ? 240 : 150);
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //挥弧沿途飘散冰雾
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                    DustID.IceTorch, Vector2.Zero, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
                d.velocity = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2() * 1.5f;
            }
            //终结拍蓄力：寒雾自四周倒吸入刃，像剑在深吸一口凛冬
            if (IsFinisher && phase <= PhaseHold) {
                Vector2 at = Hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(42f, 74f);
                Vector2 toBlade = (Vector2.Lerp(Hand, mainTip, 0.6f) - at) * 0.16f;
                Dust d = Dust.NewDustPerfect(at, DustID.IceTorch, toBlade, 110, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_Light>(at, toBlade, GsFrostbrand.RimeHot,
                        Main.rand.NextFloat(0.06f, 0.1f))?.Configure(9, 0.6f);
                }
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //碎冰迸溅
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
    /// 霜弹：呼出的晶芯彗体。飞行走呼吸节律：出膛 13、先滞（14 帧减速到约 7）、
    /// 再涌（5 帧提速回约 10）、后稳，绝不匀速；冰雾拖尾，命中叠霜火，消亡碎冰。
    /// 自绘：软光芯+星贴图晶十字+涌相沿速度拉长。ai[0]=三叉扇成员（体型略小）
    /// </summary>
    internal class GsFrostbrandBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool FanShard => Projectile.ai[0] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>呼吸相：滞 14 帧、涌 5 帧、稳</summary>
        private const int StallEnd = 14;
        private const int SurgeEnd = 19;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 140;
        }

        public override void AI() {
            Life++;
            //呼吸节律：滞相减速，涌相回涌
            if (Life <= StallEnd) {
                Projectile.velocity *= 0.955f;
            }
            else if (Life <= SurgeEnd) {
                Projectile.velocity *= 1.075f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsFrostbrand.RimeMain.ToVector3() * 0.35f);

            //冰雾拖尾，涌相喷得更急
            if (!VaultUtils.isServer && Main.rand.NextBool(Life > StallEnd && Life <= SurgeEnd ? 1 : 2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                    -Projectile.velocity * 0.1f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 180);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.25f }, Projectile.Center);
            //碎冰与残雾
            for (int i = 0; i < 7; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f), 60, default,
                    Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsFrostbrand.RimeHot, 0.16f)
                ?.Configure(9, 0.7f);
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
            float scaleMul = FanShard ? 0.8f : 1f;
            //涌相：弹体沿速度拉长、亮度上探（运动的必须被运动拉伸）
            float surge = Life > StallEnd && Life <= SurgeEnd + 3 ? 1f : 0f;
            float stretch = 1f + 0.55f * surge;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + SegRand(1) * 6.28f);

            //雾迹：旧位置的淡青光点
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size * 0.5f) - Main.screenPosition;
                float t = 1f - (i / (float)Projectile.oldPos.Length);
                Color mist = GsFrostbrand.RimeHot * (0.16f * t);
                mist.A = 0;
                Main.EntitySpriteDraw(glow, at, null, mist, 0f, glow.Size() * 0.5f,
                    0.22f * t * scaleMul, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //软光芯
            Color core = GsFrostbrand.RimeHot * (0.55f * pulse);
            core.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() * 0.5f,
                0.42f * scaleMul, SpriteEffects.None, 0);

            //晶体彗身：顺速度长笔 + 横短笔叠出冰晶十字
            Color body = Color.Lerp(GsFrostbrand.RimeMain, GsFrostbrand.RimeBright, 0.5f) * pulse;
            body.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, body, Projectile.rotation,
                star.Size() * 0.5f, new Vector2(0.05f, 0.17f * stretch) * scaleMul, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, body * 0.75f, Projectile.rotation + MathHelper.PiOver2,
                star.Size() * 0.5f, new Vector2(0.04f, 0.09f) * scaleMul, SpriteEffects.None, 0);

            //霜白高光
            Color spec = GsFrostbrand.RimeBright * (0.9f * pulse);
            spec.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, spec, Projectile.rotation,
                star.Size() * 0.5f, new Vector2(0.025f, 0.07f * stretch) * scaleMul, SpriteEffects.None, 0);
            return false;
        }
    }
}
