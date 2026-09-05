using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【魔法冰晶刃】材质：雪原魔冰凝成的法刃。签名：每拍斩切爆发凝出一枚自旋冰晶，
    /// 飞行 20 帧后失衡下坠，终结拍双发小扇形
    /// </summary>
    internal class GsIceBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.IceBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsIceBladeHeld>();

        protected override string GsDescFallback =>
            "Reforged: each slash condenses a spinning ice shard that arcs down in flight; the finisher looses a twin fan of shards";
        internal static readonly Color IceBright = new(206, 240, 255); //霜白刃缘
        internal static readonly Color IceMain = new(112, 172, 228);   //魔冰蓝
        internal static readonly Color IceHot = new(152, 220, 255);    //冰芯亮蓝

        //底伤 +2%：冰晶每拍 1 枚 50% 底伤（终结拍双发 40%×2）+ 冰晶命中附 60 帧霜火，
        //对比原版每挥一发全伤冰弹，投射物总量反而更收敛，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 105%~115%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 魔法冰晶刃手持：三拍。0/1 交替斩各凝 1 枚冰晶，2 终结重斩双发小扇形。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsIceBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.IceBlade;
        protected override Color EdgeBright => GsIceBlade.IceBright;
        protected override Color BodyMain => GsIceBlade.IceMain;
        protected override Color HotAccent => GsIceBlade.IceHot;

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
    }

    /// <summary>
    /// 凝晶冰锥：冰晶自旋飞行，20 帧后失衡下坠（不匀速直飞）；用原版冰刃冰弹贴图
    /// </summary>
    internal class GsIceBladeShardProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        /// <summary>匀速直飞的帧数，超过开始下坠</summary>
        private const int StraightFrames = 20;

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
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 60);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
        }
    }
}
