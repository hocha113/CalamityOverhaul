using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 圣骑士之锤重铸（★A 档）。材质：圣金战锤。签名行为：①双手过顶蓄力掷，满蓄锤面亮起圣辉十字
    /// ②去程沉重抛线，行至尽头先升后坠，一记审判坠锤砸出圣金震荡环（65% 范围伤害+震屏）
    /// ③右键改向：先冲向光标再于该处起坠 ④回程金辉螺旋，圣尘余痕长过锤体
    /// </summary>
    internal class GsPaladinsHammer : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.PaladinsHammer;

        internal override int BoomerProjType => ModContent.ProjectileType<GsPaladinsHammerProj>();

        internal override int MaxAirborne => int.MaxValue;   //原版无同场上限，掷速由手持接管节流

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "A two-handed overhead throw; the hammer arcs heavily, rises at the end of its flight,\n" +
            "then slams down in judgement: 65% area damage and a shockwave where it lands\n" +
            "Right click while it flies: it dashes to your cursor and delivers the slam there";

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

    /// <summary>圣锤蓄力掷手持：双手过顶重起手，满蓄十字圣辉</summary>
    internal class GsPaladinsHammerHeld : GsBoomerThrowHeldBase
    {
        protected override int SourceItemID => ItemID.PaladinsHammer;

        protected override int BoomerangType => ModContent.ProjectileType<GsPaladinsHammerProj>();

        protected override Color GlowColor => GsPaladinsHammerProj.HolyGold;

        protected override int RaiseDur => 12;

        protected override int ReleaseDur => 7;

        protected override float ThrowSpeedMul => 1.1f;

        protected override float LeanAmp => 0.09f;

        protected override float ForwardStep => 2.4f;

        protected override float HoldDist => 28f;

        protected override SoundStyle ThrowSound => SoundID.Item1 with { Volume = 0.95f, Pitch = -0.4f };

        protected override void PostDrawHeld(SpriteBatch sb, Vector2 drawPos, float rot, float charge) {
            //满蓄圣辉十字：蓄势后半程从锤面亮起
            if (charge < 0.5f) {
                return;
            }
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return;
            }
            float k = (charge - 0.5f) * 2f;
            Color c = GsPaladinsHammerProj.HolyGold * (0.7f * k);
            c.A = 0;
            sb.Draw(star, drawPos, null, c, rot, star.Size() / 2f, 0.1f * k, SpriteEffects.None, 0);
            Color core = Color.White * (0.5f * k);
            core.A = 0;
            sb.Draw(star, drawPos, null, core, -rot * 0.5f, star.Size() / 2f, 0.05f * k, SpriteEffects.None, 0);
        }
    }

    /// <summary>圣锤体：审判坠锤。ai[2]：0=普通/冲刺 1=坠落 2=驻地</summary>
    internal class GsPaladinsHammerProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.PaladinsHammer;

        /// <summary>圣金</summary>
        internal static readonly Color HolyGold = new(255, 215, 120);

        protected override Color GlowColor => HolyGold;

        protected override Color TrailColor => new(255, 190, 90);

        protected override int OutTime => 30;
        protected override float OutDrag => 0.955f;
        protected override int HoverTime => 16;           //升锤蓄势
        protected override int DashTime => 999;           //冲刺相自管理（改向冲刺/坠落/驻地三态）
        protected override int PhaseAfterHover => PhaseDash;
        protected override int HitboxSize => 30;
        protected override int RedirectCharges => 1;
        protected override bool HoverOnFirstHit => false;  //直击穿场，坠锤才是主菜
        protected override bool AllowCommandInOut => true;
        protected override float GhostBaseAlpha => 0.3f;
        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.7f, Pitch = -0.4f };

        private const int ModeDash = 0;
        private const int ModeSlam = 1;
        private const int ModeEmbed = 2;

        private int SlamMode {
            get => (int)Projectile.ai[2];
            set => Projectile.ai[2] = value;
        }

        /// <summary>悬停期满即坠的确定性标记（各端同步推进悬停计时，标记同时立起）</summary>
        private bool slamPending;

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
                if (slamPending) {
                    //审判坠锤：竖直向下
                    slamPending = false;
                    SlamMode = ModeSlam;
                    Projectile.velocity = new Vector2(0f, 12f);
                    Projectile.tileCollide = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = -0.6f }, Projectile.Center);
                    }
                }
                else {
                    SlamMode = ModeDash;
                }
            }
        }

        protected override void OnHoverTick(Player owner) {
            //升锤期锤头扶正朝下（贴图对角，视觉近似）
            Projectile.rotation = Projectile.rotation.AngleLerp(MathHelper.PiOver4, 0.22f);
            if (PhaseTimer >= HoverTime - 1) {
                slamPending = true;
            }
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
                        SlamMode = ModeDash;
                        EnterPhase(PhaseReturn, owner);
                    }
                    break;
                default:
                    //改向冲刺：抵达冲程即在光标处起坠
                    if (PhaseTimer >= 14) {
                        slamPending = true;
                        EnterPhase(PhaseHover, owner);
                        PhaseTimer = HoverTime - 4;   //只留短促升锤直接接坠
                    }
                    break;
            }
        }

        /// <summary>升锤与坠落停转，回程恢复快旋</summary>
        protected override float SpinTarget(int phase) {
            if (phase == PhaseHover || (phase == PhaseDash && SlamMode != ModeDash)) {
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

        /// <summary>审判落点：震荡波 + 震屏 + 圣尘喷涌</summary>
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
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1.5f, 5f));
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 6f),
                    vel, HolyGold, Main.rand.NextFloat(0.45f, 0.7f))?.Configure(true, Main.rand.Next(18, 30));
            }
        }

        protected override void FlightFX(Player owner) {
            base.FlightFX(owner);
            //回程金辉螺旋余痕：闪尘寿命长过锤体
            if (Phase == PhaseReturn && PhaseTimer % 3 == 0) {
                Vector2 swirl = (PhaseTimer * 0.5f).ToRotationVector2() * 10f;
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + swirl,
                    -Projectile.velocity * 0.04f, HolyGold, 0.42f)
                    ?.Configure(HolyGold * 0.5f, 32, 0.12f, 0.9f);
            }
            //坠落拉出竖直金线
            if (Phase == PhaseDash && SlamMode == ModeSlam && PhaseTimer % 2 == 0) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center - new Vector2(0f, 18f),
                    new Vector2(0f, -0.5f), HolyGold, 0.2f)?.Configure(12, 0.7f);
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            base.HitBurstFX(target, hit);
            PRTLoader.NewParticle<PRT_Sparkle>(target.Center,
                Main.rand.NextVector2Circular(2.5f, 2.5f), Color.White, 0.4f)
                ?.Configure(HolyGold * 0.6f, 20, 0.1f);
        }

        protected override void PostDrawLayers(SpriteBatch sb, Vector2 drawPos, Color lightColor) {
            //升锤与坠落期的圣辉十字警示
            bool charging = Phase == PhaseHover || (Phase == PhaseDash && SlamMode == ModeSlam);
            if (!charging) {
                return;
            }
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return;
            }
            float k = Phase == PhaseHover ? MathHelper.Clamp(PhaseTimer / (float)HoverTime, 0f, 1f) : 1f;
            float tw = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 8f) + Projectile.whoAmI));
            Color c = HolyGold * (0.6f * k * tw);
            c.A = 0;
            sb.Draw(star, drawPos, null, c, 0f, star.Size() / 2f, 0.12f * k, SpriteEffects.None, 0);
        }
    }

    /// <summary>圣金震荡波：贴地宽判定，脉冲环即视觉本体</summary>
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

        public override void AI() {
            if (Projectile.localAI[0] != 0f || VaultUtils.isServer) {
                return;
            }
            Projectile.localAI[0] = 1f;
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                GsPaladinsHammerProj.HolyGold, 1.1f)?.Configure(0.3f, 2f, 20);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsPaladinsHammerProj.HolyGold, 0.6f)?.Configure(14, 1f);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
    }
}
