using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 军士联合盾重铸（原版字段名 BouncingShield，显示名 Sergeant United Shield）。
    /// 材质：红白蓝军用圆盾。签名行为：①去程在敌人之间折跳至多四次，每跳击退递增
    /// ②回手接盾后短暂举盾，40 帧内格挡一次受击 ③折跳与格挡都有铛声
    /// </summary>
    internal class GsSergeantUnitedShield : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.BouncingShield;

        internal override int BoomerProjType => ModContent.ProjectileType<GsUnitedShieldProj>();

        internal override float DamageMul => 1.05f;

        protected override string GsDescFallback =>
            "Outbound it ricochets between foes up to four times, knocking harder with every bounce\nCatching it raises your guard: within 40 ticks you block one instance of damage";
    }

    /// <summary>军用盾体：盾阵折跳，接盾格挡</summary>
    internal class GsUnitedShieldProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.BouncingShield;

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
            }
            return true;
        }
    }
}
