using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【骨剑】材质：干燥髓骨磨成的大骨刃。签名：①终结拍收势时甩出一根旋骨，
    /// 抛物线飞行且自旋越转越快 ②终结劈砍带前压
    /// </summary>
    internal class GsBoneSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BoneSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsBoneSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: the third strike hurls a spinning bone that arcs through the air, splintering on whatever it strikes";
        internal static readonly Color BoneBright = new(246, 240, 224); //骨白
        internal static readonly Color BoneMain = new(216, 198, 160);   //米黄骨身
        internal static readonly Color BoneHot = new(255, 216, 138);    //髓芯暖黄

        //底伤 -5% 摊账：每三拍 = 1 + 1 + 1.3(终结) + 0.45(掷骨) ≈ 单拍均值 1.25x，
        //乘 0.95 后综合 DPS 约为原版 112%~119%，落在包络内
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 0.95f;
    }

    /// <summary>
    /// 骨剑手持：三拍。0/1 交替劈骨，2 终结重劈+前压；终结拍进收势的首帧
    /// 向瞄准向甩出旋骨（约 45% 底伤）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBoneSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BoneSword;
        protected override Color EdgeBright => GsBoneSword.BoneBright;
        protected override Color BodyMain => GsBoneSword.BoneMain;
        protected override Color HotAccent => GsBoneSword.BoneHot;

        private bool boneTossed;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //甩骨终结：大后摆重劈，收势稍长给掷骨动作留戏
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 5, Recover = 13,
                    RaiseBack = 2.3f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.085f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3f, SwingPitch = -0.22f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.SwingPitch = stage == 0 ? 0.06f : -0.04f;//干骨劈砍偏干偏脆
            return b;
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //终结拍进收势的首帧甩骨：顺挥砍余势掷出，带上抬弧线
            if (IsFinisher && !boneTossed && phase == PhaseRecover) {
                boneTossed = true;
                Vector2 tossVel = baseAngle.ToRotationVector2() * 11.5f + new Vector2(0f, -2.6f);
                //底伤 45%：先除回终结拍乘数再摊
                int tossDamage = Math.Max(1, (int)(Projectile.damage * 0.45f / Beat.DamageMult));
                SpawnOwnedProj(ModContent.ProjectileType<GsBoneSwordTossProj>(), Hand, tossVel,
                    tossDamage, Projectile.knockBack * 0.6f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = 0.32f }, Owner.Center);
                }
            }
        }
    }

    /// <summary>
    /// 掷骨：抛物线飞行的旋骨。自旋带角加速度（越飞越快转）；用原版骨头手套骨块贴图
    /// </summary>
    internal class GsBoneSwordTossProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneGloveProj;

        /// <summary>当前自旋角速度（各端由同一初速确定性推进）</summary>
        private float spinSpeed = 0.16f;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.BoneGloveProj];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            //抛物线：重力与轻微空气阻尼
            Projectile.velocity.Y += 0.34f;
            Projectile.velocity.X *= 0.995f;

            //角加速度：自旋逐帧加快，不匀速
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;
            spinSpeed = Math.Min(spinSpeed + 0.014f, 0.52f);
            Projectile.rotation += spinSpeed * dir;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);
        }
    }
}
