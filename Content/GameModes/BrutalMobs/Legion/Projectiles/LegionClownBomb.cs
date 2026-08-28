using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 小丑滚地炸弹：原版炸弹贴图 + 可见引线火花，落地弹跳滚行，引信
    /// <see cref="FuseFrames"/> 帧后引爆；爆炸前最后 <see cref="RingFrames"/> 帧
    /// 地面警示环渐亮=伤害窗预告，判定半径与可见环共用 <see cref="BlastRadius"/>。
    /// 引信期被玩家弹幕命中即提前哑火（权威端判定，ai[0]=哑火旗随包同步，
    /// 各端读旗后本地钳短寿命——timeLeft 不进同步包，不能只改权威端副本）。
    /// 引爆帧之外 CanDamage 恒假：滚动的炸弹本体无接触伤害
    /// </summary>
    internal class LegionClownBomb : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bomb;

        /// <summary>引信帧数（掷出到起爆）</summary>
        internal const int FuseFrames = 120;
        /// <summary>爆炸判定窗帧数（伤害窗=警示环烧满的瞬间）</summary>
        private const int BlastFrames = 8;
        /// <summary>爆炸前警示环渐亮时长（伤害窗预告，档位一律不缩短）</summary>
        private const int RingFrames = 30;
        /// <summary>爆炸半径：判定与可见环共用同一常量（判定=可见环）</summary>
        internal const float BlastRadius = 110f;
        /// <summary>哑火后的消散帧</summary>
        private const int DefuseFadeFrames = 16;
        /// <summary>滚行重力（每帧），NPC 侧掷点抛物解算引用同一常量</summary>
        internal const float BombGravity = 0.25f;
        /// <summary>下落终速上限</summary>
        private const float MaxFallSpeed = 14f;

        private bool Defused {
            get => Projectile.ai[0] == 1f;
            set => Projectile.ai[0] = value ? 1f : 0f;
        }
        private bool InBlast => !Defused && Projectile.timeLeft <= BlastFrames;
        private float RingCharge => MathHelper.Clamp(
            1f - (Projectile.timeLeft - BlastFrames) / (float)RingFrames, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FuseFrames + BlastFrames;
            Projectile.netImportant = true;
        }

        /// <summary>伤害窗=引爆帧：环烧满之前滚动本体无判定；哑火后永不引爆（公平阀门）</summary>
        public override bool? CanDamage() => InBlast ? null : false;

        /// <summary>引爆帧按圆判定：判定半径与可见环同一常量</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!InBlast) {
                return false;
            }
            Vector2 nearest = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) <= BlastRadius * BlastRadius;
        }

        public override void AI() {
            //哑火旗随同步包到达后，各端本地钳短寿命（timeLeft 本身不过网）
            if (Defused && Projectile.timeLeft > DefuseFadeFrames) {
                Projectile.timeLeft = DefuseFadeFrames;
            }

            //滚行运动：重力 + 触地摩擦，转角读横速（滚动感）
            Projectile.velocity.Y += BombGravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            if (Projectile.velocity.Y == 0f) {
                Projectile.velocity.X *= 0.97f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.06f;

            //哑火判定（权威端，低频）：引信期被玩家侧弹幕碰到即提前失效。
            //末段留同步余量：引爆前最后几帧不再受理哑火，防止哑火包迟到时
            //受击端拿着过期旗短暂开出伤害窗
            if (!VaultUtils.isClient && !Defused && Projectile.timeLeft > BlastFrames + 8
                && Projectile.timeLeft % 4 == 0) {
                Rectangle box = Projectile.Hitbox;
                box.Inflate(6, 6);
                foreach (Projectile other in Main.ActiveProjectiles) {
                    if (other.friendly && !other.hostile && other.damage > 0
                        && other.owner >= 0 && other.owner < Main.maxPlayers
                        && other.Hitbox.Intersects(box)) {
                        Defused = true;
                        Projectile.timeLeft = DefuseFadeFrames;
                        Projectile.velocity.X *= 0.5f;
                        Projectile.netUpdate = true;
                        break;
                    }
                }
            }

            if (Main.dedServ) {
                return;
            }
            if (Defused) {
                //哑火余烟
                if (Main.rand.NextBool(2)) {
                    Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                        DustID.Smoke, 0f, -0.6f, 130, default, 1f);
                    smoke.velocity *= 0.4f;
                }
                return;
            }

            //引线火花：随引信烧短，火花越贴近弹体
            float fuseLeft = MathHelper.Clamp(
                (Projectile.timeLeft - BlastFrames) / (float)FuseFrames, 0f, 1f);
            Vector2 fuseTip = Projectile.Center
                + new Vector2(0f, -14f - 6f * fuseLeft).RotatedBy(Projectile.rotation);
            if (Main.rand.NextBool(2)) {
                Dust spark = Dust.NewDustPerfect(fuseTip, DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.4f)),
                    80, default, Main.rand.NextFloat(0.8f, 1.2f));
                spark.noGravity = true;
            }
            Lighting.AddLight(fuseTip, 0.25f, 0.14f, 0.04f);

            float ring = RingCharge;
            if (ring > 0f && Projectile.timeLeft > BlastFrames) {
                //警示环：尘粒标出精确判定半径，随倒数渐密（判定=可见环）
                int puffs = 1 + (int)(ring * 3f);
                for (int i = 0; i < puffs; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust edge = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * BlastRadius,
                        DustID.RedTorch, ang.ToRotationVector2() * 0.3f, 90, default, 0.9f + 0.5f * ring);
                    edge.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.5f * ring, 0.15f * ring, 0.05f * ring);
            }

            if (Projectile.timeLeft == BlastFrames) {
                //引爆瞬间：各端本地按同一确定性倒数播放（爆响 + 焰浪）
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f }, Projectile.Center);
                for (int i = 0; i < 26; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float reach = Main.rand.NextFloat(0.2f, 1f);
                    Dust fire = Dust.NewDustPerfect(
                        Projectile.Center + ang.ToRotationVector2() * (BlastRadius * reach * 0.6f),
                        DustID.Torch, ang.ToRotationVector2() * (3f + 5f * reach), 60, default,
                        Main.rand.NextFloat(1.2f, 2f));
                    fire.noGravity = true;
                }
                for (int i = 0; i < 10; i++) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center,
                        DustID.Smoke, Main.rand.NextVector2Circular(3f, 3f) - new Vector2(0f, 1.5f),
                        110, default, Main.rand.NextFloat(1.2f, 1.8f));
                    smoke.velocity *= 0.7f;
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //落地弹跳 + 撞墙反弹，滚行不销毁
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.55f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y && oldVelocity.Y > 1.2f) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.42f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 5 },
                        Projectile.Center);
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !Defused) {
                return;
            }
            //哑火收场：一撮闷烟，无爆炸（爆炸表现挂在引爆帧，不在死亡帧）
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.45f, Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Smoke, 0f, -0.8f, 140, default, 1.2f);
                smoke.velocity *= 0.5f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //空中飞行段的同材质残影（滚地后速度低自然消失）
            if (Projectile.velocity.Length() > 3f) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, ghostPos, null, new Color(120, 110, 110, 90) * (0.35f * t),
                        Projectile.rotation, orig, 0.95f * t + 0.05f, SpriteEffects.None, 0);
                }
            }

            //警示环渐亮：黑底 SoftGlow 加色盘，半径对齐判定圆（尘环标边、光盘填面）
            float ring = RingCharge;
            if (!Defused && ring > 0f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 20f + Projectile.identity);
                float scale = BlastRadius * 2f / glow.Width;
                Main.EntitySpriteDraw(glow, drawPos, null,
                    new Color(255, 80, 40, 0) * (0.10f + 0.30f * ring * pulse),
                    0f, glow.Size() / 2f, scale, SpriteEffects.None, 0);
            }

            //本体：真 alpha 原版炸弹贴图 + 引信侧微光
            Main.EntitySpriteDraw(tex, drawPos, null, Projectile.GetAlpha(lightColor),
                Projectile.rotation, orig, 1f, SpriteEffects.None, 0);
            if (!Defused) {
                Main.EntitySpriteDraw(tex, drawPos, null, new Color(255, 140, 60, 0) * (0.18f + 0.3f * ring),
                    Projectile.rotation, orig, 1.06f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
