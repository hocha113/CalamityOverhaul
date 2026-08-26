using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 钉枪重铸：木匠的「嵌钉-起钉」处决节奏。钉命中不再消耗而是嵌进目标
    /// （每目标至多 5 枚），嵌钉期间该目标受到的一切伤害 +3%/钉（封顶 15%）；
    /// 右键「起钉」把全部嵌钉爆出：每钉结算 65% 武器伤的小爆并外溅 3 枚二次钉片。<br/>
    /// 钉 = 弹幕状态载体，天然同步：MarkData2 0 普通 / 1 嵌入 / 2 起爆令 / 3 钉片
    /// </summary>
    internal class GsNailGun : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.NailGun;

        protected override string GsDescFallback =>
            "Reforged: nails embed into flesh (up to 5 per victim), each making the victim take +3% damage from all sources; right click rips every nail out in a burst of shrapnel";

        /// <summary>铁锈钢色</summary>
        internal static readonly Color NailSteel = new(215, 190, 150);

        /// <summary>每目标嵌钉上限</summary>
        internal const int EmbedCap = 5;

        private LocalizedText tipExtract;

        /// <summary>数一个目标身上（该玩家的）嵌钉数</summary>
        internal static int CountEmbedded(int owner, int npcIndex) {
            int n = 0;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != ProjectileID.NailFriendly || p.owner != owner
                    || !p.TryGetGlobalProjectile(out GodSmithProjRouter r)
                    || r.MarkData2 != 1f || (int)p.ai[0] != npcIndex) {
                    continue;
                }
                if (++n >= EmbedCap) {
                    break;
                }
            }
            return n;
        }

        public override void GsSetStaticDefaults()
            => tipExtract = this.GetLocalization("TipExtract", () => "Rip them out!");

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            //起钉：给嵌钉打起爆令再走死亡路径（钉不是爆炸物，直接 Kill 进 OnKill）
            int n = DetonateMarked(player,
                filter: (p, r) => p.type == ProjectileID.NailFriendly && r.MarkData2 == 1f,
                before: (p, r) => r.MarkData2 = 2f);
            if (n <= 0) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = 0.45f }, player.Center);
            LocalTip(player, tipExtract, NailSteel);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 0.6f, NailSteel);
            return null;
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.NailFriendly || router.MarkData2 != 1f) {
                return true;
            }
            //嵌入态：钉是插在宿主身上的状态载体
            NPC host = Main.npc[(int)proj.ai[0]];
            if (!host.active || host.life <= 0) {
                proj.Kill();
                return false;
            }
            float ang = IdentityHash01(proj.identity) * MathHelper.TwoPi;
            Vector2 offset = ang.ToRotationVector2() * (host.width * 0.28f);
            proj.Center = host.Center + offset;
            proj.rotation = ang - MathHelper.PiOver2;
            proj.velocity = Vector2.Zero;
            proj.friendly = false;
            proj.tileCollide = false;
            if (proj.alpha > 0) {
                proj.alpha = Math.Max(0, proj.alpha - 25);
            }
            //嵌钉微光：低频钢芒提示层数存在
            if (!VaultUtils.isServer && proj.timeLeft % 30 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center, new Vector2(0f, -0.4f),
                    NailSteel, 0.18f)?.Configure(false, 10);
            }
            return false;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只有主射钉转嵌入；钉片与已嵌钉不转
            if (proj.type != ProjectileID.NailFriendly || router.MarkData2 != 0f
                || !target.active || target.life <= 0 || target.type == NPCID.TargetDummy
                || CountEmbedded(proj.owner, target.whoAmI) >= EmbedCap) {
                return;
            }
            //抵掉本次命中的穿透消耗让钉存活（>0 守卫防无限穿被写坏）
            if (proj.penetrate > 0) {
                proj.penetrate++;
            }
            proj.ai[0] = target.whoAmI;
            router.MarkData2 = 1f;
            proj.timeLeft = 600;
            proj.friendly = false;
            proj.tileCollide = false;
            proj.netUpdate = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 5 }, target.Center);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.NailFriendly) {
                return;
            }
            //起爆令：钉被拔出的殉爆
            if (router.MarkData2 == 2f) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 6 }, proj.Center);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_ProcSpark>(proj.Center,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                            NailSteel, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
                    }
                    for (int i = 0; i < 2; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(proj.Center,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4f),
                            NailSteel, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(16, 26));
                    }
                }
                if (proj.IsOwnedByLocalPlayer()) {
                    //拔钉小爆 65% + 三向外溅钉片 30%
                    Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsNailBurstProj>(),
                        Math.Max(1, (int)(proj.damage * 0.65f)), 2f, proj.owner);
                    int shardDamage = Math.Max(1, (int)(proj.damage * 0.30f));
                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(7f, 10f);
                        Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, vel,
                            ProjectileID.NailFriendly, shardDamage, 1f, proj.owner);
                    }
                }
                return;
            }
            //普通消亡（含嵌入期满脱落）：一点钢屑
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f),
                        NailSteel, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            base.GsProjOnSpawnInherited(proj, router, parent, parentRouter);
            //起钉外溅的二次钉片：标记为钉片身份，不再嵌入
            if (proj.type == ProjectileID.NailFriendly) {
                router.MarkData2 = 3f;
            }
        }
    }

    /// <summary>
    /// 起钉小爆：拔钉瞬间的一发短命判定箱，承载 65% 武器伤的殉爆
    /// </summary>
    internal class GsNailBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] != 0f || VaultUtils.isServer) {
                return;
            }
            Projectile.localAI[0] = 1f;
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsNailGun.NailSteel, 0.2f)?.Configure(8, 0.8f);
        }
    }

    /// <summary>
    /// 嵌钉承伤层：目标身上每枚嵌钉让它受到的一切来源伤害 +3%（封顶 15%）。
    /// 层数从场上弹幕即时清点（弹幕表各端同步，命中裁决端读数一致），
    /// NPC 身上不落任何字段。自建钩子自查模式旗
    /// </summary>
    internal class GsNailGunGlobalNPC : GlobalNPC
    {
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            int nails = 0;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != ProjectileID.NailFriendly
                    || !p.TryGetGlobalProjectile(out GodSmithProjRouter r)
                    || r.MarkData2 != 1f || (int)p.ai[0] != npc.whoAmI) {
                    continue;
                }
                if (++nails >= GsNailGun.EmbedCap) {
                    break;
                }
            }
            if (nails > 0) {
                modifiers.FinalDamage *= 1f + 0.03f * nails;
            }
        }
    }
}
