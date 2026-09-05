using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 夺命枪「收殓人」：黑铁殓棺左轮·血锈铭纹。<br/>
    /// ①殓魂：击杀或重创濒死之敌收下一盏魂灯，魂灯飞回入怀补血（每匣一次）；
    /// ②末发「葬钟」：血色重弹鸣钟出膛，命中哀鸣、击退翻倍；
    /// ③逐膛装填可打断，膛声如钉棺，钉一颗少一声。<br/>
    /// 后坐 1.2px（末发 2.4px）+ 角度踢。<br/>
    /// 账目：周期 150t 打 6 发对原版 5.8 发（×1.03），末发均值 1.1、魂灯为续航收益，
    /// 伤害行 ×1.2（原版公认偏弱）→ 约 118%（待游戏内标定）
    /// </summary>
    internal class GsTheUndertaker : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.TheUndertaker;

        protected override string GsDescFallback =>
            "Reforged: fell a foe, or wound one near death, and the gun collects a soul lantern\nthat drifts back to mend you once per cylinder.\nThe final chamber tolls the burial bell: a heavy blood round with doubled knockback";
        public override int MagSize => 6;
        public override int ReloadTicks => 44;
        public override GsReloadStyle Style => GsReloadStyle.Cylinder;
        protected override float GetRecoil(bool lastRound) => lastRound ? 2.4f : 1.2f;

        /// <summary>伤害行 ×1.2：原版夺命枪公认偏弱，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.2f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            if (lastRound) {
                damage = (int)(damage * 1.5f);  //葬钟重弹
                knockback *= 2f;
                velocity *= 1.2f;
            }
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = 1f;
            if (!VaultUtils.isServer) {
                //钟鸣出膛：低哑钟声
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.8f, Pitch = -0.55f }, position);
            }
            return null;
        }

        //==================== 殓魂（owner 端权威） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsGunsEarlyPlayer mp = State(player);

            //葬钟命中：哀鸣（个人反馈层）
            if (router.MarkData >= 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.55f, Pitch = -0.2f }, target.Center);
            }

            //收殓：击杀或打至 20% 血线以下，各匣一盏
            if (!mp.healUsedThisMag && (target.life <= 0 || target.life < target.lifeMax / 5)) {
                mp.healUsedThisMag = true;
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, -Vector2.UnitY * 2f,
                    ModContent.ProjectileType<GsUndertakerSoulProj>(), 0, 0f, proj.owner);
            }
        }

        //==================== 钉棺装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //甩轮
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = -0.4f }, player.Center);
            }
        }

        protected override void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (!VaultUtils.isServer) {
                //钉棺闷响：低音定死、不上行，钉一颗少一声
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.75f, Pitch = -0.5f }, player.Center);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.15f, Pitch = -0.6f }, player.Center);
            }
        }

        //==================== 后坐姿态（差分，见 GsGunRecoil） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 1.6f, 0.07f);
    }

    /// <summary>
    /// 殓魂灯：从收殓处升起、绕行一拍后归主的魂灯，归怀补 10 血。
    /// 无伤害判定，纯 owner 端结算；借原版幽魂治疗球贴图默认绘制
    /// </summary>
    internal class GsUndertakerSoulProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpiritHeal;

        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //起灯一拍上飘，随后渐加速归主
            if (Projectile.localAI[0] < 22f) {
                Projectile.localAI[0]++;
                Projectile.velocity *= 0.94f;
                Projectile.velocity.Y -= 0.06f;
            }
            else {
                Vector2 toOwner = owner.MountedCenter - Projectile.Center;
                float dist = toOwner.Length();
                float speed = MathHelper.Clamp(4f + Projectile.localAI[0] * 0.16f, 4f, 16f);
                Projectile.localAI[0]++;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    toOwner.SafeNormalize(Vector2.UnitY) * speed, 0.12f);
                //侧摆游魂步
                Vector2 side = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
                Projectile.Center += side * MathF.Sin(Projectile.localAI[0] * 0.25f + Seed * 6f) * 1.2f;

                if (dist < 26f) {
                    //归怀：owner 端结算补血（HealEffect 自带广播）
                    if (Projectile.owner == Main.myPlayer) {
                        owner.Heal(10);
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.3f }, owner.Center);
                    }
                    Projectile.Kill();
                    return;
                }
            }
        }
    }
}
