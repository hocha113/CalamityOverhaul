using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Budcrowns
{
    /// <summary>
    /// 蕾冠，荒花连枷。按住攻击键让锤头绕身悬旋，松开朝准星掷出，
    /// 到程或撞地后沿链收回。锤头命中抖落荒针，每第三次命中触发怒放。
    /// 实体在 <see cref="BudcrownBall"/>
    /// </summary>
    internal class Budcrown : BssModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Budcrown";

        public override void SetDefaults() {
            Item.width = 58;
            Item.height = 72;
            Item.damage = 22;
            Item.knockBack = 6.5f;
            Item.useTime = Item.useAnimation = 34;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.UseSound = null;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 20);
            Item.shoot = ModContent.ProjectileType<BudcrownBall>();
            Item.shootSpeed = 15.5f;
            Item.DamageType = DamageClass.Melee;
        }

        //场上已有锤头时不再出手
        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<BudcrownBall>()] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //锤头从悬旋态起手，初始角取掷出方向
            Projectile.NewProjectile(source, player.MountedCenter, velocity, type, damage, knockback,
                player.whoAmI, ai1: velocity.ToRotation());
            return false;
        }
    }

    /// <summary>
    /// 蕾冠锤头。ai0 状态（0 悬旋 1 掷出 2 收回），ai1 悬旋起始角。
    /// 悬旋期贴玩家绕圈，松开攻击键朝准星掷出；到程、撞地后收回。
    /// 锤头是一张有前后的花嘴（贴图开口朝下），不自转：
    /// 悬旋与收回时嘴朝外，掷出时嘴朝飞行方向。
    /// 命中抖落荒针，第三次命中怒放花瓣圈。链条用体节贴图铺
    /// </summary>
    internal class BudcrownBall : BssModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BudcrownBall";

        //客户端 PostSetupContent 加载，服务端为空，绘制侧判空
        [VaultLoaden(CWRConstant.Projectile_Melee + "BudcrownLink")]
        public static Asset<Texture2D> LinkTex = null;

        private const int StSpin = 0;
        private const int StLaunch = 1;
        private const int StRetract = 2;

        private const float SpinRadius = 62f;
        private const float MaxReach = 380f;
        /// <summary>攒满多少次命中触发一次怒放</summary>
        private const int HitsPerBloom = 3;

        private float State { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float SpinStart => Projectile.ai[1];

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>悬旋累计角</summary>
        private float spinAngle;
        /// <summary>掷出里程</summary>
        private float mileage;
        /// <summary>收回计时（提速用）</summary>
        private float retractTimer;
        /// <summary>已攒命中数，owner 侧决策</summary>
        private int hitCombo;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => State != StSpin;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            //换下武器后立即收回
            bool holdingItem = Owner.HeldItem != null && Owner.HeldItem.type == ModContent.ItemType<Budcrown>();
            if (!holdingItem && State != StRetract) {
                EnterRetract();
            }

            Projectile.tileCollide = State == StLaunch;

            switch ((int)State) {
                case StLaunch:
                    LaunchAI();
                    break;
                case StRetract:
                    RetractAI();
                    break;
                default:
                    SpinAI();
                    break;
            }

            if (!Main.dedServ && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    Main.rand.NextVector2Circular(0.6f, 0.6f), 150, default, 0.7f);
                d.noGravity = true;
            }
        }

        #region 状态
        /// <summary>贴图开口朝下（+Y），让嘴对准 dir</summary>
        private void FaceMouth(Vector2 dir) {
            Projectile.rotation = dir.ToRotation() - MathHelper.PiOver2;
        }

        /// <summary>悬旋：贴玩家绕圈提速，松开攻击键掷出</summary>
        private void SpinAI() {
            Projectile.timeLeft = 300;
            spinAngle += (0.2f + MathHelper.Clamp(spinAngle * 0.004f, 0f, 0.1f)) * Owner.direction;
            float ang = SpinStart + spinAngle;
            float radius = SpinRadius + MathF.Sin(spinAngle * 0.5f) * 5f;
            Projectile.Center = Owner.MountedCenter + ang.ToRotationVector2() * radius;
            Projectile.velocity = Vector2.Zero;
            //嘴朝外甩，链条自然指回手上
            FaceMouth(ang.ToRotationVector2());

            //维持使用动作与手臂朝向
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Owner.itemRotation = MathHelper.WrapAngle(ang * Owner.direction);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters,
                ang - MathHelper.PiOver2 * Owner.gravDir);

            //owner 侧决策掷出
            if (Projectile.IsOwnedByLocalPlayer() && !Owner.channel) {
                Vector2 aim = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = aim * 15.5f;
                Owner.ChangeDir(aim.X >= 0f ? 1 : -1);
                State = StLaunch;
                mileage = 0f;
                Projectile.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.2f }, Projectile.Center);
            }
        }

        /// <summary>掷出：直飞后段微坠，到程折返，嘴咬向飞行方向</summary>
        private void LaunchAI() {
            mileage += Projectile.velocity.Length();
            if (mileage > 130f) {
                Projectile.velocity.Y += 0.18f;
            }
            FaceMouth(Projectile.velocity);

            if (mileage >= MaxReach) {
                EnterRetract();
            }
        }

        /// <summary>收回：沿链渐加速归手，近身消失</summary>
        private void RetractAI() {
            retractTimer++;
            Vector2 home = Owner.MountedCenter;
            Vector2 to = home - Projectile.Center;
            float dist = to.Length();
            if (dist < 28f) {
                Projectile.Kill();
                return;
            }
            float speed = MathF.Min(15f + retractTimer * 0.6f, 28f);
            float steer = dist < 110f ? 0.32f : 0.16f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, to.SafeNormalize(Vector2.UnitX) * speed, steer);
            //被链拽回，嘴仍朝外
            FaceMouth(Projectile.Center - Owner.MountedCenter);
        }

        private void EnterRetract() {
            if (State == StRetract) {
                return;
            }
            State = StRetract;
            retractTimer = 0f;
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }
        #endregion

        #region 命中
        public override bool OnTileCollide(Vector2 oldVelocity) {
            Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.1f }, Projectile.position);
            //撞地反弹一口再收回，锤头有分量
            Projectile.velocity = -oldVelocity * 0.3f;
            EnterRetract();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中抖落荒针
            int needles = Main.rand.NextBool() ? 2 : 1;
            for (int i = 0; i < needles; i++) {
                Vector2 vel = new Vector2(0f, -5.5f).RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f));
                BloomArsenal.ShedNeedle(Projectile, Projectile.Center, vel,
                    (int)(Projectile.damage * 0.45f), 0f, gravity: true);
            }

            //第三次命中怒放
            hitCombo++;
            if (hitCombo >= HitsPerBloom) {
                hitCombo = 0;
                SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.1f, Volume = 0.95f }, Projectile.Center);
                BloomArsenal.PetalRing(Projectile, Projectile.Center, 8,
                    (int)(Projectile.damage * 0.7f), 0f, 6.5f);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.CWR().GetScreenShake(3f);
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 6; i++) {
                        BssVfx.PetalDrift(Projectile.Center, Main.rand.NextVector2Circular(1.5f, 1f), 0.75f);
                    }
                }
            }

            //掷出中命中略泄劲
            if (State == StLaunch) {
                Projectile.velocity *= 0.85f;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (State == StLaunch) {
                modifiers.Knockback += 2f;
                modifiers.HitDirectionOverride = Math.Sign(Projectile.velocity.X);
            }
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            Vector2 hand = Owner.MountedCenter + new Vector2(Owner.direction * 4f, -2f);
            Vector2 ball = Projectile.Center;

            //链条：素材自带的体节沿链铺开，节距略小于节高保证连续
            Texture2D link = LinkTex?.Value;
            Vector2 span = ball - hand;
            float len = span.Length();
            if (link != null && len > 12f) {
                Vector2 dir = span / len;
                float rot = span.ToRotation() + MathHelper.PiOver2;
                float step = link.Height * 0.8f;
                for (float d = 8f; d < len - 12f; d += step) {
                    Vector2 at = hand + dir * d;
                    Color c = Lighting.GetColor(at.ToTileCoordinates());
                    Main.EntitySpriteDraw(link, at - Main.screenPosition, null, c, rot,
                        link.Size() * 0.5f, 1f, SpriteEffects.None, 0);
                }
            }

            //锤体（嘴壳），方向由状态机喂
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, ball - Main.screenPosition, null, lightColor,
                Projectile.rotation, tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
        #endregion
    }
}
