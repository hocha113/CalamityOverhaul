using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 圣骑士之锤重铸（★A 档）。材质：圣金战锤。签名行为：①双手过顶蓄力掷
    /// ②去程沉重抛线，行至尽头先升后坠，一记审判坠锤砸出震荡波（65% 范围伤害+震屏）
    /// </summary>
    internal class GsPaladinsHammer : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.PaladinsHammer;

        internal override int BoomerProjType => ModContent.ProjectileType<GsPaladinsHammerProj>();

        internal override int MaxAirborne => int.MaxValue;   //原版无同场上限，掷速由手持接管节流

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "A two-handed overhead throw; the hammer arcs heavily, rises at the end of its flight,\nthen slams down in judgement: 65% area damage and a shockwave where it lands";
        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsPaladinsHammerHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsPaladinsHammerHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            return false;
        }
    }

    /// <summary>圣锤蓄力掷手持：双手过顶重起手</summary>
    internal class GsPaladinsHammerHeld : GsBoomerThrowHeldBase
    {
        protected override int SourceItemID => ItemID.PaladinsHammer;

        protected override int BoomerangType => ModContent.ProjectileType<GsPaladinsHammerProj>();

        protected override int RaiseDur => 12;

        protected override int ReleaseDur => 7;

        protected override float ThrowSpeedMul => 1.1f;

        protected override float LeanAmp => 0.09f;

        protected override float ForwardStep => 2.4f;

        protected override float HoldDist => 28f;

        protected override SoundStyle ThrowSound => SoundID.Item1 with { Volume = 0.95f, Pitch = -0.4f };
    }

    /// <summary>圣锤体：审判坠锤。ai[2]：0=普通 1=坠落 2=驻地</summary>
    internal class GsPaladinsHammerProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.PaladinsHammer;

        protected override int OutTime => 30;
        protected override float OutDrag => 0.955f;
        protected override int HoverTime => 16;           //升锤蓄势
        protected override int DashTime => 999;           //冲刺相自管理（坠落/驻地两态）
        protected override int PhaseAfterHover => PhaseDash;
        protected override int HitboxSize => 30;
        protected override bool HoverOnFirstHit => false;  //直击穿场，坠锤才是主菜
        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.7f, Pitch = -0.4f };

        private const int ModeNormal = 0;
        private const int ModeSlam = 1;
        private const int ModeEmbed = 2;

        private int SlamMode {
            get => (int)Projectile.ai[2];
            set => Projectile.ai[2] = value;
        }

        protected override void OnOutTick(Player owner) {
            Projectile.velocity.Y += 0.12f;   //沉重抛线
        }

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase == PhaseHover) {
                //升锤：先向上抬 3 像素级动势，蓄势读法
                Projectile.velocity = new Vector2(Projectile.velocity.X * 0.2f, -3.2f);
                Projectile.tileCollide = false;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
                }
                return;
            }
            if (phase == PhaseDash) {
                //审判坠锤：悬停期满借冲刺相竖直向下
                SlamMode = ModeSlam;
                Projectile.velocity = new Vector2(0f, 12f);
                Projectile.tileCollide = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = -0.6f }, Projectile.Center);
                }
            }
        }

        protected override void OnHoverTick(Player owner) {
            //升锤期锤头扶正朝下（贴图对角，视觉近似）
            Projectile.rotation = Projectile.rotation.AngleLerp(MathHelper.PiOver4, 0.22f);
        }

        protected override void OnDashTick(Player owner) {
            switch (SlamMode) {
                case ModeSlam:
                    //坠落：持续加速砸向地面
                    Projectile.velocity.X *= 0.85f;
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 1.4f, 26f);
                    Projectile.rotation = Projectile.rotation.AngleLerp(MathHelper.PiOver4, 0.25f);
                    if (PhaseTimer > 90) {
                        EnterPhase(PhaseReturn, owner);   //砸不到地就收锤
                    }
                    break;
                case ModeEmbed:
                    Projectile.velocity = Vector2.Zero;
                    if (PhaseTimer >= 10) {
                        SlamMode = ModeNormal;
                        EnterPhase(PhaseReturn, owner);
                    }
                    break;
            }
        }

        /// <summary>升锤与坠落停转，回程恢复快旋</summary>
        protected override float SpinTarget(int phase) {
            if (phase == PhaseHover || (phase == PhaseDash && SlamMode != ModeNormal)) {
                return 0f;
            }
            return phase == PhaseReturn ? 0.8f : base.SpinTarget(phase);
        }

        protected override bool HandleTileCollide(Vector2 oldVelocity) {
            if (Phase == PhaseDash && SlamMode == ModeSlam) {
                SlamMode = ModeEmbed;
                PhaseTimer = 0;
                Projectile.velocity = Vector2.Zero;
                JudgementQuake();
                return false;
            }
            return base.HandleTileCollide(oldVelocity);
        }

        /// <summary>审判落点：震荡波 + 震屏</summary>
        private void JudgementQuake() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.65f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center + new Vector2(0f, 4f), Vector2.Zero,
                    ModContent.ProjectileType<GsPaladinsHammerWaveProj>(), dmg, 9f, Owner.whoAmI);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.5f }, Projectile.Center);
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                Vector2.UnitY, 6f, 8f, 14, 1000f, "GsPaladinSlam"));
        }
    }

    /// <summary>圣金震荡波：贴地宽判定，存活 3 帧的无形判定箱，不画本体</summary>
    internal class GsPaladinsHammerWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 170;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3;
            Projectile.knockBack = 9f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
