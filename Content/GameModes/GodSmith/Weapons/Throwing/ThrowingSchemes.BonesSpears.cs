using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 骨头:回力骨。飞至最远点走回旋弧返程(返程也判伤),接住整根收回;
    /// 没接住落地 40% 回收体;暴击时分裂两片小骨(分裂片走原版轨迹不回力)
    /// </summary>
    internal class GsBone : GsThrowScheme
    {
        public override int TargetItemID => ItemID.Bone;
        protected override string GsDescFallback =>
            "Reforged: the bone arcs back to your hand and deals damage both ways; catch it to reclaim it outright\nMisses can be picked back up; crits split off two half-damage shards";

        protected override float NoConsumeChance => 0.10f;
        protected override float RecoverOnTileChance => 0.40f;
        protected override float RecoverOnFadeChance => 0.40f;
        protected override float DamageMul => 1.10f;

        /// <summary>MarkData2 分裂片码:不回力、不再分裂、缩小绘制</summary>
        private const float FragmentCode = 1f;

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            //回程需要多段命中:穿透带 >0 守卫,本地免疫防止同目标连帧刷伤
            if (proj.penetrate > 0) {
                proj.penetrate += 2;
            }
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = 20;
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            //分裂片与非本武器弹幕走原版重力旋转
            if (proj.type != ProjectileID.Bone || !router.IsMarked || router.MarkData2 == FragmentCode) {
                return true;
            }
            GsThrowProjState st = router.GetOrCreateState<GsThrowProjState>();
            Player owner = Main.player[proj.owner];
            //全接管:自清出生透明度,自转
            proj.alpha = 0;
            proj.rotation += 0.38f * (proj.velocity.X >= 0f ? 1f : -1f);
            st.Custom++;
            if (proj.ai[0] == 0f) {
                //去程:轻重力,38 帧或 560px 后由 owner 裁定转返程(ai[0] 过线)
                proj.velocity.Y += 0.12f;
                if (proj.IsOwnedByLocalPlayer() && (st.Custom >= 38 || proj.Distance(owner.Center) > 560f)) {
                    proj.ai[0] = 1f;
                    proj.netUpdate = true;
                }
            }
            else {
                //返程:穿墙磁吸回手,速度渐升
                proj.tileCollide = false;
                float speed = MathHelper.Clamp(8f + st.Custom * 0.12f, 8f, 17f);
                Vector2 want = (owner.Center - proj.Center).SafeNormalize(Vector2.UnitX) * speed;
                proj.velocity = Vector2.Lerp(proj.velocity, want, 0.10f);
                if (proj.owner == Main.myPlayer && owner.active && !owner.dead
                    && proj.Hitbox.Intersects(owner.Hitbox)) {
                    //接住:整根收回;借免耗闩封死 OnKill 的二次回收
                    st.FreeThrow = true;
                    RefundOne(owner, owner.Center);
                    proj.Kill();
                    return false;
                }
                //返程兜底:超时自杀,走常规回收判定
                if (proj.IsOwnedByLocalPlayer() && st.Custom > 340) {
                    proj.Kill();
                    return false;
                }
            }
            //飞行尾迹:速度拉伸淡痕
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.05f, new Color(226, 220, 200),
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(false, 10);
            }
            return false;
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            //暴击分裂:两片半伤小骨(承签打分裂片码,走原版轨迹)
            if (proj.owner != Main.myPlayer || !st.IsPrimary || !hit.Crit) {
                return;
            }
            for (int i = -1; i <= 1; i += 2) {
                Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center,
                    proj.velocity.RotatedBy(0.5 * i) * 0.8f, ProjectileID.Bone,
                    (int)(proj.damage * 0.5f), proj.knockBack * 0.5f, proj.owner);
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (proj.type == ProjectileID.Bone) {
                //分裂片承签:打码防递归回力(仍先于生成包发出,远端一致)
                router.MarkData2 = FragmentCode;
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.Bone || router.MarkData2 != FragmentCode) {
                return null;
            }
            //分裂片:七成大小
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null, lightColor,
                proj.rotation, tex.Size() / 2f, 0.7f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>标枪:助跑掷更狠更快;同目标连击叠伤;嵌墙的枪大多能捡回</summary>
    internal class GsJavelin : GsThrowScheme
    {
        public override int TargetItemID => ItemID.Javelin;
        protected override string GsDescFallback =>
            "Reforged: crits refund one; javelins left in walls are usually reclaimable\nThrowing while sprinting adds 25% damage and 30% velocity; consecutive hits on one target stack +12% up to 3";

        protected override float NoConsumeChance => 0.10f;
        protected override float RecoverOnTileChance => 0.25f;
        protected override float RecoverOnFadeChance => 0.60f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.05f;

        protected override void GsThrowModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //助跑掷:掷出瞬间自身水平速度足够快
            if (System.Math.Abs(player.velocity.X) >= 4f) {
                damage = (int)(damage * 1.25f);
                velocity *= 1.3f;
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_Spark>(position, velocity * 0.1f,
                        GsGold, 0.32f)?.Configure(false, 12);
                }
            }
        }

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            GsThrowGlobalNPC gn = target.GetGlobalNPC<GsThrowGlobalNPC>();
            if (gn.JavelinStacks > 0 && Main.GameUpdateCount <= gn.JavelinWindowUntil) {
                modifiers.FinalDamage *= 1f + 0.12f * gn.JavelinStacks;
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || target.friendly) {
                return;
            }
            GsThrowGlobalNPC gn = target.GetGlobalNPC<GsThrowGlobalNPC>();
            if (Main.GameUpdateCount > gn.JavelinWindowUntil) {
                gn.JavelinStacks = 0;
            }
            gn.JavelinStacks = System.Math.Min(3, gn.JavelinStacks + 1);
            gn.JavelinWindowUntil = Main.GameUpdateCount + 90;
        }
    }

    /// <summary>骨标枪:同目标嵌满 3 支时该目标受你的枪 +15%;嵌着的枪在目标倒下时六成掉回收体</summary>
    internal class GsBoneJavelin : GsThrowScheme
    {
        public override int TargetItemID => ItemID.BoneJavelin;
        protected override string GsDescFallback =>
            "Reforged: crits refund one\nWith 3 javelins embedded in one foe your javelins hit 15% harder; each embedded javelin has a 60% chance to drop a recovery pickup when it dies";

        protected override float NoConsumeChance => 0.10f;
        protected override float RecoverOnTileChance => 0.25f;
        protected override float RecoverOnFadeChance => 0.40f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.05f;

        protected override float RecoverChanceOnKill(Projectile proj, int timeLeft, GsThrowProjState st, bool diedOnHit)
            //嵌入态消亡(目标倒下或穿刺期满):六成掉回收体
            => IsStuck(proj) ? 0.60f : base.RecoverChanceOnKill(proj, timeLeft, st, diedOnHit);

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (CountStuckOn(target, proj.owner, proj.type) >= 3) {
                modifiers.FinalDamage *= 1.15f;
            }
        }
    }

    /// <summary>刺球:撒菱经济。存在期满自动回库;敌同时踩到三颗以上触发连锁刺爆(每颗一生一次)</summary>
    internal class GsSpikyBall : GsThrowScheme
    {
        public override int TargetItemID => ItemID.SpikyBall;
        protected override string GsDescFallback =>
            "Reforged: balls that expire on the field return straight to your bag\nA foe standing on 3 or more of your balls sets off a chain spike-burst, 50% each, once per ball";

        protected override float NoConsumeChance => 0f;
        protected override float RecoverOnFadeChance => 0.70f;
        protected override bool DirectRefundOnFade => true;
        protected override float DamageMul => 1.08f;

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || target.friendly) {
                return;
            }
            //数与目标碰撞箱相交的本人刺球(几何判定,含本支)
            int touching = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == proj.type && p.owner == proj.owner
                    && p.Hitbox.Intersects(target.Hitbox)) {
                    touching++;
                }
            }
            if (touching < 3) {
                return;
            }
            //连锁刺爆:每颗未闩的刺球各起一记半伤脉冲,一生只参与一次
            int burstType = ModContent.ProjectileType<GsBurstProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != proj.type || p.owner != proj.owner
                    || !p.Hitbox.Intersects(target.Hitbox)
                    || !p.TryGetGlobalProjectile(out GodSmithProjRouter pr)) {
                    continue;
                }
                GsThrowProjState ps = pr.GetOrCreateState<GsThrowProjState>();
                if (ps.Latch) {
                    continue;
                }
                ps.Latch = true;
                Projectile.NewProjectile(p.GetSource_FromThis(), p.Center, Vector2.Zero,
                    burstType, (int)(proj.damage * 0.5f), 2f, proj.owner, 60f, GsBurstProj.FxNone);
                if (!VaultUtils.isServer) {
                    for (int k = 0; k < 4; k++) {
                        PRTLoader.NewParticle<PRT_Spark>(p.Center,
                            Main.rand.NextVector2Circular(3f, 3f),
                            new Color(200, 205, 215), Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, 16);
                    }
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f }, target.Center);
            }
        }
    }
}
