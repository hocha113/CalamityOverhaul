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
    /// 【荆棘园圃】材质：世纪之花心木配阳绿汁液的园艺重剑。
    /// 签名：①原版种荚保留升级：每一斩掷出旋转木荚，弹跳带重力，
    /// 炸裂成扇形非匀速的荆棘弹雨（轻微追击）②终结拍种荚化作藤蔓爆发，
    /// 落点竖起驻留荆棘丛持续刺击
    /// </summary>
    internal class GsSeedler : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Seedler;

        protected override int HeldProjID => ModContent.ProjectileType<GsSeedlerHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash hurls a spinning seed pod that bounces and bursts into a fan of seeking thorns; the finisher's pod erupts into vines, raising a lingering bramble patch that keeps stabbing";
        internal static readonly Color SeedBright = new(216, 255, 160); //阳绿亮缘
        internal static readonly Color SeedMain = new(112, 192, 72);    //叶身翠绿
        internal static readonly Color SeedHot = new(250, 220, 96);     //汁液金绿

        //原版链按单体实战 斩1.0+荚1.0+落点刺0.5~1.0 ≈ 2.5~3.0x/23帧 估；
        //本方案三拍循环 ~65 帧：斩 1.0+1.0+1.3、荚 0.7x 三发（终结荚吃 1.3 拍倍率）、
        //刺 5×0.3x/荚（单体实战 2~3 中）、荆棘丛终结命中约 4 跳×0.2x →
        //单体约 8.5x/65 帧，对原版估算区间 ≈ 101%~120%；弹雨与棘丛是范围收益，底伤不动
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 荆棘园圃手持：三拍连段。0 横斩 / 1 返斩 / 2 播种重劈（前压终结）。
    /// 每拍斩切爆发掷出种荚，终结拍的荚携藤蔓爆发旗。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsSeedlerHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Seedler;
        protected override Color EdgeBright => GsSeedler.SeedBright;
        protected override Color BodyMain => GsSeedler.SeedMain;
        protected override Color HotAccent => GsSeedler.SeedHot;

        private bool podThrown;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.8f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.04f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.12f,
            },
            //拍2 播种：长举重劈，把荚砸进土里
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.14f, LeanAmp = 0.085f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.26f,
            },
        };

        /// <summary>每拍斩切爆发掷荚：沿出手向抛出，带一点上抛</summary>
        protected override void OnSlashBegin() {
            if (podThrown) {
                return;
            }
            podThrown = true;
            Vector2 dir = baseAngle.ToRotationVector2();
            int podDamage = Math.Max(1, (int)(Projectile.damage * 0.7f));
            SpawnOwnedProj(ModContent.ProjectileType<GsSeedlerPodProj>(),
                Hand + dir * (FullReach * 0.7f), dir * 9.5f + new Vector2(0f, -2.2f),
                podDamage, Projectile.knockBack * 0.4f, swingDir, IsFinisher ? 1f : 0f);
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.4f, Pitch = 0.1f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.4f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 种荚：每斩掷出的旋转木荚，用原版种荚贴图。重力弹跳（至多两弹），
    /// 命中或落定炸成荆棘弹雨；ai[0]=旋向 ai[1]=藤蔓爆发旗（终结荚，爆点竖起荆棘丛）
    /// </summary>
    internal class GsSeedlerPodProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SeedlerNut;

        private float SpinDir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private bool VineBurst => Projectile.ai[1] > 0.5f;
        private ref float Bounces => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 110;
        }

        public override void AI() {
            //重力抛体：荚是颗有分量的木果
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 14f) {
                Projectile.velocity.Y = 14f;
            }
            //荚旋：滚转随水平速度
            Projectile.rotation += (0.16f + 0.02f * MathF.Abs(Projectile.velocity.X)) * SpinDir;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Bounces++;
            if (Bounces >= 2f) {
                return true;
            }
            //弹跳：竖向反弹衰减、横向拖阻
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.62f;
            }
            Projectile.velocity.X *= 0.86f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.35f }, Projectile.Center);
            }
            return false;
        }

        /// <summary>爆荚：荆棘弹雨（owner 端生成），终结荚追加荆棘丛</summary>
        public override void OnKill(int timeLeft) {
            if (Projectile.owner == Main.myPlayer) {
                int thornDamage = Math.Max(1, (int)(Projectile.damage * 0.43f)); //0.7x 荚 × 0.43 ≈ 0.3x 物品伤
                for (int i = 0; i < 5; i++) {
                    //扇形非匀速：以上方为轴散开，速度参差
                    float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-1.15f, 1.15f, i / 4f)
                        + Main.rand.NextFloat(-0.18f, 0.18f);
                    float speed = Main.rand.NextFloat(4.5f, 9.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                        ang.ToRotationVector2() * speed,
                        ModContent.ProjectileType<GsSeedlerThornProj>(),
                        thornDamage, 0.5f, Projectile.owner);
                }
                if (VineBurst) {
                    int brambleDamage = Math.Max(1, (int)(Projectile.damage * 0.29f)); //≈0.2x 物品伤/跳
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsSeedlerBrambleProj>(),
                        brambleDamage, 0f, Projectile.owner);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //爆荚音
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.1f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 荆棘刺：爆荚散射的追击小刺，用原版荆棘贴图。速度参差、8 帧后轻微追击附近敌人，
    /// 触砖即碎
    /// </summary>
    internal class GsSeedlerThornProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SeedlerThorn;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
        }

        public override void AI() {
            Life++;
            //轻微追击：8 帧后向 320 内最近可追目标缓转（保留原版荆棘的追击身份）
            if (Life >= 8f) {
                NPC prey = null;
                float best = 320f * 320f;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    float d = npc.DistanceSQ(Projectile.Center);
                    if (d < best) {
                        best = d;
                        prey = npc;
                    }
                }
                if (prey != null) {
                    float speed = Projectile.velocity.Length();
                    Vector2 want = (prey.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * speed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.07f);
                }
                else {
                    //无目标时坠回抛体
                    Projectile.velocity.Y += 0.12f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }

    /// <summary>
    /// 荆棘丛：藤蔓爆发在落点竖起的驻留刺丛。160 帧寿命，18 帧一跳持续刺击；
    /// 用原版荨麻藤贴图按半径画一笔作范围提示
    /// </summary>
    internal class GsSeedlerBrambleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NettleBurstRight;

        private const int TotalLife = 160;
        private const float Radius = 84f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                //藤蔓爆发：破土闷响
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.35f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            }
        }

        public override bool? CanDamage() => Life >= 5f && Projectile.timeLeft > 12 ? null : false;

        /// <summary>横扁棘丛判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 delta = targetHitbox.Center.ToVector2() - Projectile.Center;
            delta.Y *= 1.5f;
            return delta.Length() <= Radius;
        }

        /// <summary>范围提示：原版荨麻藤贴图按棘丛半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Radius * 2f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * (1f - Life01),
                0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
