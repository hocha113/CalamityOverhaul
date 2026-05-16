using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 沙之飞匕 —— 战士的沙岩投掷匕首
    /// 直线掷出后下坠，撞地后短暂停留；若插入沙地则蓄势喷沙刺，并喷射黄沙地脉冲击波
    /// </summary>
    internal class SandDagger : ModItem
    {
        public override string Texture => CWRConstant.Item + "Melee/SandDagger";

        public override void SetDefaults() {
            Item.width = 48;
            Item.height = 48;
            Item.damage = 16;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item1 with { Pitch = -0.05f };
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 50, 15);
            Item.shoot = ModContent.ProjectileType<SandDaggerThrow>();
            Item.shootSpeed = 17.5f;
            Item.DamageType = DamageClass.Melee;
        }
    }

    /// <summary>
    /// 沙之飞匕实体
    /// 直线投掷 + 重力下坠，撞地嵌入；插入沙地时延时喷射成"地脉冲击波"
    /// </summary>
    internal class SandDaggerThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Melee/SandDaggerProj";

        private static readonly int[] SandTileIDs = new int[] {
            TileID.Sand, TileID.Ebonsand, TileID.Pearlsand, TileID.Crimsand,
            TileID.HardenedSand, TileID.CorruptHardenedSand, TileID.CrimsonHardenedSand
        };

        //是否已撞到地形（嵌入态）
        private ref float OnTile => ref Projectile.ai[0];
        //是否插入沙地（达到则进入"地脉冲击波"模式）
        private ref float OnSand => ref Projectile.ai[1];
        //嵌入计时
        private ref float StuckTimer => ref Projectile.ai[2];

        //插入瞬间锁定的旋转角
        private float tileRot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //标记"已经飞起"用于绘制拖尾
            Projectile.localAI[0] = 1f;

            if (OnTile == 0f) {
                //飞行: 朝向速度方向，20 帧后开始重力下坠
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (++StuckTimer > 20) {
                    Projectile.velocity.Y += 0.3f;
                    Projectile.velocity.X *= 0.99f;
                }
            }
            else {
                //嵌入: 锁定旋转 + 短寿命
                Projectile.timeLeft = 2;
                Projectile.rotation = tileRot;

                if (StuckTimer <= 40) {
                    Projectile.velocity *= 0.6f;
                }

                //插入沙地后蓄势, 满 40 帧后向上冲射并触发地脉冲击波
                if (OnSand > 0f && StuckTimer > 40) {
                    Projectile.velocity = new Vector2(0, -13);
                    Projectile.rotation = Projectile.velocity.ToRotation();
                }

                if (++StuckTimer >= 60) {
                    Projectile.Kill();
                }
            }
        }

        public override bool? CanDamage() {
            //嵌入静止阶段不再造成伤害（喷射上升时仍可伤敌）
            if (OnTile > 0f && OnSand <= 0f) {
                return false;
            }
            return null;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (OnTile == 0f) {
                Projectile.Center += Projectile.velocity;
                Projectile.velocity = Vector2.Zero;
                tileRot = Projectile.rotation;

                //检测是否插入沙地（覆盖周围 5 格）
                Vector2 tilePos = Projectile.Bottom;
                if (SandTileIDs.Contains(Framing.GetTileSafely(tilePos).TileType)
                    || SandTileIDs.Contains(Framing.GetTileSafely(tilePos + new Vector2(1, 0)).TileType)
                    || SandTileIDs.Contains(Framing.GetTileSafely(tilePos + new Vector2(-1, 0)).TileType)
                    || SandTileIDs.Contains(Framing.GetTileSafely(tilePos + new Vector2(0, 1)).TileType)
                    || SandTileIDs.Contains(Framing.GetTileSafely(tilePos + new Vector2(0, -1)).TileType)) {
                    OnSand = 1f;
                }

                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
                OnTile = 1f;
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            //插入沙地: 战士地脉冲击波 (爆炸 + 屏震 + 三发沙刺)
            if (OnSand > 0f) {
                Projectile.Explode();

                if (CWRServerConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Projectile.Center, Vector2.UnitX, 2.5f, 3.5f, 5, 350f, FullName));
                }

                if (Projectile.IsOwnedByLocalPlayer()) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 velocity = new Vector2(0, -6).RotatedByRandom(0.6f);
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity
                            , CWRID.Proj_DesertScourgeSpit, Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
                        if (proj >= 0 && proj < Main.maxProjectiles) {
                            Main.projectile[proj].hostile = false;
                            Main.projectile[proj].friendly = true;
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, proj);
                        }
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            SpriteEffects effects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + MathHelper.PiOver2 * (Projectile.velocity.X > 0 ? 1 : -1);

            //飞行残影
            if (Projectile.localAI[0] == 1f) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2f;
                    Color color = lightColor * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2f);
                    Main.EntitySpriteDraw(texture, drawPos, null, color, drawRot,
                        texture.Size() / 2f, Projectile.scale, effects, 0);
                }
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                drawRot, texture.Size() / 2f, Projectile.scale, effects, 0);
            return false;
        }
    }
}
