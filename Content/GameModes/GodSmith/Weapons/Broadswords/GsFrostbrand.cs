using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【凛冬呼吸·霜痕剑】材质：冰霜巨人肺腑里淬出的霜痕剑，每一斩都是一次吐息。
    /// 签名：①每一斩呼出一枚霜弹，飞行先滞后涌、绝不匀速
    /// ②命中叠霜火，终结拍霜弹化作三叉冰片扇
    /// </summary>
    internal class GsFrostbrand : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Frostbrand;

        protected override int HeldProjID => ModContent.ProjectileType<GsFrostbrandHeld>();

        protected override string GsDescFallback =>
            "Reforged: winter's breath; every slash exhales a frost bolt that stalls and surges in flight, hits inflict Frostburn, and the finishing beat fans the bolt into three ice shards";
        internal static readonly Color RimeBright = new(222, 244, 255); //霜白刃缘
        internal static readonly Color RimeMain = new(96, 144, 216);    //冰渊蓝体色
        internal static readonly Color RimeHot = new(146, 236, 226);    //极光青强调

        //底伤不加成（原版 49/useAnim23 每挥一发全伤霜弹）：刀身拍均 1.03x + 霜弹 0.75x
        //（终结拍换 0.45x×3 三叉扇，散射难全中），按三拍循环约 66 帧摊算，
        //贴脸（刀+弹齐中）约原版 108%、纯刃外约 109%，霜火叠加是持续小补
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 凛冬呼吸手持：三拍。0 呼斩 / 1 吸斩（音调回升）/ 2 凛冬吐息
    /// （长举+前压+三叉冰扇）。每拍斩切爆发呼出霜弹。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsFrostbrandHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Frostbrand;
        protected override Color EdgeBright => GsFrostbrand.RimeBright;
        protected override Color BodyMain => GsFrostbrand.RimeMain;
        protected override Color HotAccent => GsFrostbrand.RimeHot;

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
    }

    /// <summary>
    /// 霜弹：用原版霜痕剑霜弹贴图。飞行走呼吸节律：出膛 13、先滞（14 帧减速到约 7）、
    /// 再涌（5 帧提速回约 10）、后稳，绝不匀速；命中叠霜火。ai[0]=三叉扇成员（体型略小）
    /// </summary>
    internal class GsFrostbrandBoltProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FrostBoltSword;

        private bool FanShard => Projectile.ai[0] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>呼吸相：滞 14 帧、涌 5 帧、稳</summary>
        private const int StallEnd = 14;
        private const int SurgeEnd = 19;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.FrostBoltSword];
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
            if (Life == 1f && FanShard) {
                //三叉扇成员体型略小
                Projectile.scale = 0.8f;
            }
            //呼吸节律：滞相减速，涌相回涌
            if (Life <= StallEnd) {
                Projectile.velocity *= 0.955f;
            }
            else if (Life <= SurgeEnd) {
                Projectile.velocity *= 1.075f;
            }
            //原版霜弹贴图为斜向弹形，补 45 度
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 180);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.25f }, Projectile.Center);
        }
    }
}
