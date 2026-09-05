using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【陨星蓝钢】材质：坠地陨星淬成的召星蓝钢。
    /// 签名「星辰共鸣」：①每拍斩切从天顶呼落一枚弧线加速的星辰 ②第三拍「引星」，
    /// 举刀锁定扇形三处预落点，爆发时三星齐落 ③命中鸣响星音
    /// </summary>
    internal class GsStarfury : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Starfury;

        protected override int HeldProjID => ModContent.ProjectileType<GsStarfuryHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash calls a star down from the zenith; the third strike locks three fated points and brings three stars crashing down together";
        internal static readonly Color StarBright = new(255, 238, 190); //星辉淡金
        internal static readonly Color StarMain = new(88, 112, 205);    //陨星蓝钢
        internal static readonly Color StarHot = new(255, 210, 110);    //星金强调

        //底伤 +2%：原版星怒的星辰本就是全伤主力，重铸星辰降为 70% 底伤；
        //每循环星数 1+1+3=5 星（原版 3 挥 3 星），近战终结 1.2x，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 108%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 陨星蓝钢手持：三拍召星连击。0/1 交替斩各唤一星，2 引星终结
    /// （长举锁定扇形三点，爆发三星齐落）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsStarfuryHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Starfury;
        protected override Color EdgeBright => GsStarfury.StarBright;
        protected override Color BodyMain => GsStarfury.StarMain;
        protected override Color HotAccent => GsStarfury.StarHot;

        /// <summary>星辰落点锚：手心沿出手向前推的固定距离（全由同步量算出，各端一致）</summary>
        private Vector2 StarAnchor => Hand + baseAngle.ToRotationVector2() * 300f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //引星终结：长举锁星、滞帧读标记、爆发三星齐落
                return new GsBroadBeat {
                    Raise = 10, Hold = 4, Slash = 4, Recover = 12,
                    RaiseBack = 2.2f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.08f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.3f,
                };
            }
            return new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? -0.1f : -0.16f,
            };
        }

        /// <summary>引星扇形第 i 个预落点（identity 播种微散布，各端一致且逐帧稳定）</summary>
        private Vector2 MarkPoint(int i) {
            Vector2 anchor = StarAnchor;
            Vector2 lateral = (baseAngle + MathHelper.PiOver2).ToRotationVector2();
            float spread = (i - 1) * 92f + (DrawRand01(i * 11 + 5) - 0.5f) * 26f;
            float forward = (i == 1 ? 24f : 0f) + (DrawRand01(i * 17 + 9) - 0.5f) * 20f;
            return anchor + lateral * spread + baseAngle.ToRotationVector2() * forward;
        }

        protected override void OnSlashBegin() {
            //召星：普通拍落锚点一星，引星终结三点齐落（除回 DamageMult 取底伤摊账）
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            int starDamage = Math.Max(1, (int)(baseDamage * 0.7f));
            int starType = ModContent.ProjectileType<GsStarfuryStarProj>();
            if (IsFinisher) {
                for (int i = 0; i < 3; i++) {
                    Vector2 mark = MarkPoint(i);
                    SpawnOwnedProj(starType, new Vector2(mark.X, MathF.Max(mark.Y - 760f, 60f)),
                        Vector2.Zero, starDamage, 3f, mark.X, mark.Y, i * 4);
                }
            }
            else {
                Vector2 mark = StarAnchor;
                SpawnOwnedProj(starType, new Vector2(mark.X, MathF.Max(mark.Y - 760f, 60f)),
                    Vector2.Zero, starDamage, 3f, mark.X, mark.Y);
            }
            if (!VaultUtils.isServer) {
                //星鸣：召引的清越星音
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = IsFinisher ? -0.2f : 0.25f }, Owner.Center);
            }
        }

        /// <summary>命中：变调星鸣</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.35f, Pitch = Main.rand.NextFloat(0.3f, 0.6f), MaxInstances = 3 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 呼落星辰：天顶坠向预落点的星，用原版星怒星星贴图。ai[0]/ai[1]=落点坐标 ai[2]=起落延迟帧
    /// （延迟期 alpha 全透隐身）。下落带横向弧线与纵向加速度（禁匀速）；抵达落点即消；穿 1 敌
    /// </summary>
    internal class GsStarfuryStarProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Starfury;

        private Vector2 TargetPoint => new(Projectile.ai[0], Projectile.ai[1]);
        private int SpawnDelay => (int)Projectile.ai[2];
        private ref float AgeTimer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;//穿 1 敌
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 150;
        }

        /// <summary>确定性伪随机（identity+salt 播种，各端一致）：给弧线初速与自旋方向用</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool? CanDamage() => AgeTimer > SpawnDelay ? null : false;

        public override void AI() {
            AgeTimer++;
            //齐落错帧：延迟期隐身不动，营造三星鱼贯而落
            if (AgeTimer <= SpawnDelay) {
                Projectile.velocity = Vector2.Zero;
                Projectile.alpha = 255;
                return;
            }
            Projectile.alpha = 0;
            float age = AgeTimer - SpawnDelay;

            //首帧甩出横向弧线初速（identity 播种，各端一致）
            if (age == 1f) {
                float lean = (SegRand(1) - 0.5f) * 9f;
                Projectile.velocity = new Vector2(lean, 4f);
            }

            //下落相：纵向加速度 + 横向朝落点比例修正，弧线收拢（全程不匀速）
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.42f, 23f);
            float dx = TargetPoint.X - Projectile.Center.X;
            Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, dx * 0.04f, 0.08f);
            Projectile.rotation += 0.22f * (SegRand(2) > 0.5f ? 1f : -1f);

            //抵达落点即消（越过落点高度收星）
            if (Projectile.Center.Y >= TargetPoint.Y) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = Main.rand.NextFloat(0.1f, 0.5f), MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //落点星音
            SoundEngine.PlaySound(SoundID.Item88 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
        }
    }
}
