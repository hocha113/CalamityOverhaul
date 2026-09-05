using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 飞刀重铸。材质：受缚怨钢刀。签名行为：①按住攻击键去程持续受控转向追随光标，呼吸式脉动推进
    /// ②松手原地旋舞，刀刃高速回旋连斩 ③回程直线切割
    /// </summary>
    internal class GsFlyingKnife : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.FlyingKnife;

        internal override int BoomerProjType => ModContent.ProjectileType<GsFlyingKnifeProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Hold the attack button to steer the knife after your cursor; release it to let the blade\nwhirl in place, slashing everything around, then carve a straight line home";
    }

    /// <summary>怨钢刀体：操刀引导，去程受控、松手旋舞</summary>
    internal class GsFlyingKnifeProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.FlyingKnife;

        protected override int OutTime => 95;
        protected override float OutDrag => 1f;         //推进改走呼吸脉动，不用整体衰减
        protected override int HoverTime => 26;
        protected override bool HoverOnFirstHit => false;

        /// <summary>操刀基准速度</summary>
        private const float SteerSpeed = 13.5f;

        protected override bool OutFinished(Player owner)
            => PhaseTimer >= OutTime || !owner.channel;   //channel 各端同步，松手即入旋舞

        protected override void OnOutTick(Player owner) {
            //owner 端持续追光标转向；每 6 帧发一次校正包
            if (Projectile.IsOwnedByLocalPlayer() && owner.channel) {
                Vector2 desired = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX * spinDir);
                Vector2 cur = Projectile.velocity.SafeNormalize(Vector2.UnitX * spinDir);
                Projectile.velocity = Vector2.Lerp(cur, desired, 0.14f).SafeNormalize(cur) * Projectile.velocity.Length();
                if (PhaseTimer % 6 == 0) {
                    Projectile.netUpdate = true;
                }
            }
            //呼吸式脉动推进：速度沿正弦起伏，禁匀速直飞（各端确定性）
            float pulse = 1f + (0.22f * MathF.Sin(PhaseTimer * 0.24f));
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX * spinDir) * (SteerSpeed * pulse);
        }

        protected override float SpinTarget(int phase) => phase switch {
            PhaseHover => 1.15f,   //旋舞：远超家族默认的转速
            PhaseDash => 1f,
            PhaseReturn => 0.7f,
            _ => 0.55f,
        };

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase == PhaseHover && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            }
        }
    }
}
