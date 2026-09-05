using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【圣辉附魔钢】材质：注满圣辉的附魔秘银。签名：①每拍斩切放出光刃波，
    /// 减速回稳地飞行 ②终结拍光刃加宽 1.5 倍且贯穿三敌
    /// </summary>
    internal class GsEnchantedSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.EnchantedSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsEnchantedSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash hurls a blade of holy light; the finisher's wave widens and pierces through three foes";
        internal static readonly Color HolyBright = new(214, 244, 255); //圣辉青白
        internal static readonly Color HolyMain = new(96, 158, 235);    //附魔青蓝
        internal static readonly Color HolyHot = new(255, 226, 150);    //鎏金强调

        //底乘 1.0：原版附魔剑本就每挥一道全伤剑气；重铸剑气降为 60% 底伤但终结拍
        //加宽穿三 + 终结近战 1.2x，远程期望约原版 95%~105%、贴身约 108%~115%，
        //综合 DPS 落在原版 100%~112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1f;
    }

    /// <summary>
    /// 圣辉附魔钢手持：三拍轻灵快剑。0/1 短起手全弧快扫，2 拉满弧贯穿终结；
    /// 每拍斩切爆发射出光刃波（用原版附魔剑气贴图）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsEnchantedSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.EnchantedSword;
        protected override Color EdgeBright => GsEnchantedSword.HolyBright;
        protected override Color BodyMain => GsEnchantedSword.HolyMain;
        protected override Color HotAccent => GsEnchantedSword.HolyHot;

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
    }

    /// <summary>
    /// 光刃波：斩切爆发甩出的剑气，用原版附魔剑气贴图。ai[0]=1 为终结拍宽刃（1.5x 宽、穿 3）。
    /// 速度先衰减后回稳巡航，全程不匀速
    /// </summary>
    internal class GsEnchantedSwordBeamProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EnchantedBeam;

        private bool Widened => Projectile.ai[0] >= 1f;
        private int Age => 90 - Projectile.timeLeft;

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
            //终结宽刃首帧对齐形态（ai[0] 随生成包过线，各端一致）：判定与贴图同步放大 1.5x
            if (Age == 0 && Widened) {
                Projectile.penetrate = 3;
                Projectile.Resize(38, 38);
                Projectile.scale = 1.5f;
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
            //原版剑气贴图为斜向刀形，补 45 度
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
        }
    }
}
