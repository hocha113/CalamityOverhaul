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
    /// 【魔法冰晶刃】材质：雪原魔冰凝成的法刃。签名：①每拍斩切爆发凝出一枚自旋菱形冰晶，
    /// 飞行 20 帧后失衡下坠，终结拍双发小扇形 ②命中碎冰迸溅、无血尘（魔法质感）
    /// ③挥弧沿途飘散冰雾
    /// </summary>
    internal class GsIceBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.IceBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsIceBladeHeld>();

        protected override string GsDescFallback =>
            "Reforged: each slash condenses a spinning ice shard that arcs down in flight; " +
            "the finisher looses a twin fan of shards";

        //冰蓝白色板
        internal static readonly Color IceBright = new(206, 240, 255); //霜白刃缘
        internal static readonly Color IceMain = new(112, 172, 228);   //魔冰蓝
        internal static readonly Color IceHot = new(152, 220, 255);    //冰芯亮蓝
        internal static readonly Color IceDeep = new(18, 30, 48);      //深潭暗蓝

        //底伤 +2%：冰晶每拍 1 枚 50% 底伤（终结拍双发 40%×2）+ 冰晶命中附 60 帧霜火，
        //对比原版每挥一发全伤冰弹，投射物总量反而更收敛，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 105%~115%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 魔法冰晶刃手持：三拍。0/1 交替斩各凝 1 枚冰晶，2 终结重斩双发小扇形。
    /// 魔法质感：BleedOnFlesh 关闭，命中只出碎冰。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsIceBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.IceBlade;
        protected override Color EdgeBright => GsIceBlade.IceBright;
        protected override Color BodyMain => GsIceBlade.IceMain;
        protected override Color HotAccent => GsIceBlade.IceHot;
        protected override Color DeepShadow => GsIceBlade.IceDeep;

        /// <summary>魔冰法刃不喷血，命中反馈全走碎冰</summary>
        protected override bool BleedOnFlesh => false;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //凝晶终结：重斩双发
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 5, Recover = 12,
                    RaiseBack = 2.15f, Follow = 1.2f, ReachScale = 1.15f, LeanAmp = 0.08f,
                    DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 2.5f, SwingPitch = -0.2f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Raise = stage == 0 ? 6 : 5;
            b.DamageMult = 0.95f;
            b.SwingPitch = stage == 0 ? 0.05f : 0.14f;
            return b;
        }

        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsIceBlade.IceMain, 0.22f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsIceBlade.IceHot : GsIceBlade.IceBright;

        /// <summary>斩切爆发凝晶出手：普通拍单发，终结拍 ±0.13 弧度小扇形双发</summary>
        protected override void OnSlashBegin() {
            int type = ModContent.ProjectileType<GsIceBladeShardProj>();
            Vector2 aim = baseAngle.ToRotationVector2();
            if (IsFinisher) {
                //双发各 40%，与终结拍 1.25x 本体合计摊入包络
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.4f));
                SpawnOwnedProj(type, Hand + aim * 30f, aim.RotatedBy(0.13f) * 11f, dmg, 1.5f);
                SpawnOwnedProj(type, Hand + aim * 30f, aim.RotatedBy(-0.13f) * 11f, dmg, 1.5f);
            }
            else {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.5f));
                SpawnOwnedProj(type, Hand + aim * 30f, aim * 11f, dmg, 1.5f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.5f, Pitch = 0.2f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //挥弧沿途飘散冰雾
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.45f, 1f)),
                    DustID.IceTorch, Vector2.Zero, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
                d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 1.4f;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //碎冰迸溅
            int shards = IsFinisher ? 10 : 6;
            for (int i = 0; i < shards; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Ice,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 60, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 凝晶冰锥：菱形冰晶自旋飞行，20 帧后失衡下坠（不匀速直飞）；
    /// 冰雾拖尾，命中/落地碎冰迸溅。自绘：四芒星贴图纵横两笔叠出菱晶 + 软光芯 + 残影链，
    /// 加色批全 A=0，闪烁抖动 identity 播种
    /// </summary>
    internal class GsIceBladeShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>匀速直飞的帧数，超过开始下坠</summary>
        private const int StraightFrames = 20;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            //自旋方向跟横向速度
            Projectile.rotation += 0.34f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            //20 帧后失衡：重力渐显、横速微衰，弧线下坠
            if (Projectile.localAI[0] > StraightFrames) {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.24f, 13f);
                Projectile.velocity.X *= 0.995f;
            }

            Lighting.AddLight(Projectile.Center, GsIceBlade.IceMain.ToVector3() * 0.35f);

            //冰雾拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                    -Projectile.velocity * 0.08f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 60);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f), 60, default,
                    Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsIceBlade.IceHot, 0.18f)
                ?.Configure(9, 0.7f);
        }

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SeedRand01(int salt) {
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
            Vector2 origin = star.Size() / 2f;

            //残影链：旧位置画渐淡小晶
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 at = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trail = GsIceBlade.IceBright * (0.22f * (1f - i / (float)Projectile.oldPos.Length));
                trail.A = 0;
                Main.EntitySpriteDraw(star, at, null, trail, Projectile.oldRot[i],
                    origin, new Vector2(0.05f, 0.14f), SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //冰晶明灭呼吸：identity 播种错相，不掷绘制 rand
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + SeedRand01(1) * 6.28f);

            //软光芯
            Color core = GsIceBlade.IceHot * (0.55f * pulse);
            core.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);

            //菱形晶体：四芒星纵长一笔 + 横短一笔，随 rotation 自旋
            Color body = Color.Lerp(GsIceBlade.IceMain, GsIceBlade.IceBright, 0.5f) * pulse;
            body.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, body, Projectile.rotation,
                origin, new Vector2(0.07f, 0.2f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, body * 0.8f, Projectile.rotation + MathHelper.PiOver2,
                origin, new Vector2(0.05f, 0.11f), SpriteEffects.None, 0);
            //霜白高光点
            Color spec = GsIceBlade.IceBright * (0.9f * pulse);
            spec.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, spec, Projectile.rotation,
                origin, new Vector2(0.03f, 0.08f), SpriteEffects.None, 0);
            return false;
        }
    }
}
