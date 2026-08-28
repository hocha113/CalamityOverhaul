using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 军士联合盾重铸（原版字段名 BouncingShield，显示名 Sergeant United Shield）。
    /// 材质：红白蓝军用圆盾。签名行为：①去程在敌人之间折跳至多四次，每跳击退递增
    /// ②回手接盾后短暂举盾，40 帧内格挡一次受击 ③折跳与格挡都有醒目的盾面闪光与铛声
    /// </summary>
    internal class GsSergeantUnitedShield : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.BouncingShield;

        internal override int BoomerProjType => ModContent.ProjectileType<GsUnitedShieldProj>();

        internal override float DamageMul => 1.05f;

        protected override string GsDescFallback =>
            "Outbound it ricochets between foes up to four times, knocking harder with every bounce\n" +
            "Catching it raises your guard: within 40 ticks you block one instance of damage\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>军用盾体：盾阵折跳，接盾格挡</summary>
    internal class GsUnitedShieldProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.BouncingShield;

        protected override Color GlowColor => new(160, 190, 240);

        /// <summary>折跳强调红</summary>
        private static readonly Color BounceRed = new(235, 80, 80);

        protected override bool HoverOnFirstHit => false;
        protected override int DashTime => 20;
        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.6f, Pitch = -0.1f };

        /// <summary>已折跳次数（owner 权威）</summary>
        private int bounces;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (bounces > 0) {
                modifiers.Knockback *= 1f + (0.15f * bounces);
            }
        }

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Projectile.IsOwnedByLocalPlayer() || Phase == PhaseReturn) {
                return;
            }
            //盾阵折跳
            if (bounces < 4) {
                NPC next = FindBounceTarget(target);
                if (next != null) {
                    bounces++;
                    Projectile.velocity = (next.Center - Projectile.Center)
                        .SafeNormalize(Vector2.UnitX * spinDir) * DashSpeed;
                    EnterPhase(PhaseDash, Owner);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.75f, Pitch = 0.3f }, target.Center);
                        PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, BounceRed, 0.35f)?.Configure(9, 0.9f);
                    }
                    return;
                }
            }
            EnterPhase(PhaseReturn, Owner);
        }

        private NPC FindBounceTarget(NPC exclude) {
            NPC best = null;
            float bestDist = 460f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == exclude.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = npc.Distance(Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        protected override void OnCatch(Player owner) {
            //接盾格挡：格挡窗只在 owner 自己客户端记账（玩家受击结算在本人客户端）
            if (Projectile.IsOwnedByLocalPlayer()) {
                owner.GetModPlayer<GsUnitedShieldParryPlayer>().parryWindow = 40;
            }
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(owner.Center, Vector2.Zero, GlowColor, 0.4f)?.Configure(12, 0.9f);
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            base.HitBurstFX(target, hit);
            //红白蓝三色迸溅
            PRTLoader.NewParticle<PRT_Spark>(target.Center,
                Main.rand.NextVector2Circular(4f, 4f), BounceRed,
                Main.rand.NextFloat(0.35f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            PRTLoader.NewParticle<PRT_Spark>(target.Center,
                Main.rand.NextVector2Circular(4f, 4f), Color.White,
                Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(10, 16));
        }
    }

    /// <summary>接盾格挡窗：40 帧内免除一次受击（每玩家状态放 ModPlayer，不放方案单例）</summary>
    internal class GsUnitedShieldParryPlayer : ModPlayer
    {
        /// <summary>格挡窗剩余帧</summary>
        public int parryWindow;

        public override void ResetEffects() {
            if (parryWindow > 0) {
                parryWindow--;
            }
        }

        public override bool FreeDodge(Player.HurtInfo info) {
            if (parryWindow <= 0) {
                return false;
            }
            parryWindow = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.9f, Pitch = 0.1f }, Player.Center);
                PRTLoader.NewParticle<PRT_Light>(Player.Center, Vector2.Zero, new Color(160, 190, 240), 0.55f)?.Configure(14, 1f);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Player.Center,
                        Main.rand.NextVector2CircularEdge(5f, 5f), Color.White,
                        Main.rand.NextFloat(0.4f, 0.6f))?.Configure(false, Main.rand.Next(10, 16));
                }
            }
            return true;
        }
    }
}
