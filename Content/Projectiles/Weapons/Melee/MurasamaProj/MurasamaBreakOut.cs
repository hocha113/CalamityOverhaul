using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using log4net.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaBreakOut : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Murasama";
        protected Player Owner => Main.player[Projectile.owner];
        protected Item murasama => Owner.ActiveItem();
        private Vector2 breakOutVector;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
        }

        public override void PostAI() => CWRUtils.ClockFrame(ref Projectile.frame, 5, 12);

        float getBrakSwingDamageSengsValue(int level) {
            float overValue = 0;
            if (level >= 5) {
                overValue = level * 0.3f;
            }
            return 3 + murasama.CWR().ai[0] * 0.2f + level * 0.2f + overValue;
        }

        public override void AI() {
            if (CWRServerConfig.Instance.ForceReplaceResetContent) {
                if (murasama.type != ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Murasama>()) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                if (murasama.type != ModContent.ItemType<Murasama>()) {
                    Projectile.Kill();
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, (Main.rand.NextBool(3) ? Color.Red : Color.IndianRed).ToVector3());

            int level = InWorldBossPhase.Instance.Level();

            if (Projectile.ai[0] == 0) {//在这一阶段，弹幕负责飞出
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.ai[1]++;
                if (Projectile.ai[1] > (60 + level * 15)) {//级别越高，弹幕的飞行时间便会越加的长
                    Projectile.ai[0] = 2;
                    Projectile.ai[1] = 0;
                    Projectile.netUpdate = true;
                }
                //不要在服务器上执行粒子的生成代码浪费性能
                if (!CWRUtils.isServer) {
                    AltSparkParticle spark = new AltSparkParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.5f, Projectile.height * 0.5f) + Projectile.velocity * 1.2f
                    , Projectile.velocity
                    , false, 13, Main.rand.NextFloat(1.3f), Main.rand.NextBool(3) ? Color.Red : Color.IndianRed);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
            }
            else if (Projectile.ai[0] == 1) {//在这阶段，刀会旋转着滞留在原地，在击中敌人后触发
                Projectile.rotation += 0.1f;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = 180;
                Projectile.ai[1]++;
                if (Projectile.ai[1] > 120) {
                    Projectile.ai[0] = 2;
                    Projectile.netUpdate = true;
                }
            }
            else if (Projectile.ai[0] == 2) {//飞回玩家身上完成一次攻击流程
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.ChasingBehavior(Owner.Center, 23);
                if (Projectile.Center.Distance(Owner.Center) < 23) {
                    Projectile.Kill();
                }
            }
            else if (Projectile.ai[0] == 3) {//如果是该阶段，说明玩家试图触发升龙斩
                Projectile.velocity *= 0.98f;

                //卸载掉玩家的所有钩爪
                Owner.RemoveAllGrapplingHooks();
                //卸载掉玩家的所有坐骑
                Owner.mount.Dismount(Owner);

                Vector2 toBreakV = Owner.Center.To(Projectile.Center);

                if (Projectile.IsOwnedByLocalPlayer()) {//发射衍生弹幕和进行位移的代码只能交由主人玩家执行
                    Owner.Center = Vector2.Lerp(Owner.Center, Projectile.Center, 0.1f);
                    Owner.velocity = breakOutVector;
                    if (CWRServerConfig.Instance.LensEasing) {
                        Main.SetCameraLerp(0.1f, 10);
                    }
                    float projToOwnerLeng = Projectile.Center.Distance(Owner.Center);
                    if (projToOwnerLeng < 233) {
                        Owner.GivePlayerImmuneState(5, true);
                    }
                    if (projToOwnerLeng < 33) {
                        //murasama.CWR().ai[0]表示充能值，这里让其充能值越高，升龙斩造成的伤害便越高
                        float sengs = getBrakSwingDamageSengsValue(level);

                        //如果此时爆发没有击中敌人，那么判断是否有Boss在场
                        foreach (NPC n in Main.npc) {//如果Boss在场，不进行升龙，而是飞回玩家身上
                            if (n.boss && n.active && n.position.To(Owner.position).LengthSquared() < 9000000) {
                                if (Projectile.numHits == 0) {
                                    sengs = 2;
                                }
                                else {
                                    sengs *= 1.2f;
                                }
                                break;//不管如何，执行一次伤害二次调整后都需要跳出
                            }
                        }
                        if (Murasama.NameIsVergil(Owner)) {
                            SoundEngine.PlaySound(CWRSound.V_Hooaaa with { Volume = 0.6f }, Projectile.Center);
                        }
                        murasama.initialize();

                        int sengsDmg = (int)(Murasama.ActualTrueMeleeDamage * sengs);
                        int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center + breakOutVector * (36 + level * 3), breakOutVector * 3
                        , ModContent.ProjectileType<MurasamaBreakSwing>(), sengsDmg, 0, Owner.whoAmI);
                        Main.projectile[proj].scale = 0.5f + level * 0.0f;

                        Owner.CWR().RisingDragonCoolDownTime += Murasama.GetOnRDCD / 2;//添加升龙冷却
                        Projectile.Kill();
                    }
                }

                for (int i = 0; i < 3; i++) {
                    SparkParticle spark = new SparkParticle(Owner.Center, toBreakV.UnitVector() * -0.1f, false, 9, 3.3f, Color.IndianRed * 0.1f);
                    GeneralParticleHandler.SpawnParticle(spark);
                }

                AltSparkParticle spark2 = new AltSparkParticle(
                Owner.Center + Main.rand.NextVector2Circular(13, 23) + toBreakV.UnitVector() * 1.2f
                , toBreakV.UnitVector() * 23
                , false, 13, Main.rand.NextFloat(1.3f), Main.rand.NextBool(3) ? Color.Red : Color.IndianRed);
                GeneralParticleHandler.SpawnParticle(spark2);
            }

            if (Projectile.ai[0] != 2 && Projectile.ai[0] != 3) {
                if (CWRKeySystem.Murasama_DownKey.JustPressed) {//触发下砸技能
                    if (!Murasama.UnlockSkill2) {//在击败史莱姆之神前不能使用这个技能
                        return;
                    }
                    murasama.initialize();
                    if (murasama.CWR().ai[0] >= 2) {
                        SoundEngine.PlaySound(Murasama.BigSwing with { Pitch = -0.1f }, Projectile.Center);

                        Projectile.NewProjectile(Owner.parent(), Projectile.Center, new Vector2(0, 5)
                            , ModContent.ProjectileType<MurasamaDownSkill>(), murasama.damage, 0, Owner.whoAmI);

                        murasama.CWR().ai[0] -= 2;
                        Projectile.Kill();
                        return;
                    }
                }
                if (Owner.PressKey()) {//如果按下的是左键，那么切换到3状态进行升龙斩的相关代码的执行
                    if (!Murasama.UnlockSkill1) {//在击败初期Boss之前不能使用这个技能
                        return;
                    }
                    if (Projectile.ai[1] > 0) {
                        SoundEngine.PlaySound(Murasama.Swing with { Pitch = -0.1f }, Projectile.Center);
                        Projectile.ai[0] = 3;
                    }
                    else {
                        SoundEngine.PlaySound(Murasama.Swing with { Pitch = -0.3f }, Projectile.Center);
                        Projectile.ai[0] = 2;
                    }
                    breakOutVector = Owner.Center.To(Projectile.Center).UnitVector();
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {//如果击中了敌人，那么进入旋转滞留阶段
                Projectile.ai[0] = 1;
                Projectile.ai[1] = 0;
                Projectile.netUpdate = true;

                if (target.boss && Murasama.UnlockSkill1) {//如果击中的目标是个Boss级生物，额外给予玩家无敌帧
                    //Owner.GivePlayerImmuneState(35 + InWorldBossPhase.Instance.Level() * 2, true);//这个部分存在争议，暂时取消
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits == 0) {//如果击中了敌人，那么进入旋转滞留阶段
                Projectile.ai[0] = 1;
                Projectile.ai[1] = 0;
                Projectile.netUpdate = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[0] == 3) {//如果此时玩家已经开始升龙冲刺，那么让刀刃简单的反弹回去而不是进行回归
                Projectile.velocity = oldVelocity * -1.1f;
                return false;
            }
            if (Projectile.ai[0] != 2) {//如果是碰到了物块，那么直接回到玩家身上
                Projectile.ai[0] = 2;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 290) {
                return false;
            }
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 13), lightColor
            , Projectile.rotation + (Projectile.velocity.X > 0 ? MathHelper.ToRadians(100) : MathHelper.ToRadians(80)) + MathHelper.Pi
                , CWRUtils.GetOrig(value, 13), Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
