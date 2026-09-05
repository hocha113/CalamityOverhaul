using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 集结场：集结指令下立于旗点的驻场判定弹幕，四形态共用一类。<br/>
    /// ai[0] = 形态（0 胶垛 / 1 盘旋鸟群 / 2 雪障 / 3 万剑门），
    /// ai[1] = 空闲，ai[2] = 绑定的仆从弹幕类型（续命条件）。全部初值经 NewProjectile 形参传入。<br/>
    /// 续命各端确定性判定：集结态 + 绑定仆从在场 + 模式开启，任一不满足即全端同步过期；
    /// 剑门（形态 3）为 60 帧一次性，不续命
    /// </summary>
    internal class GsRallyFieldProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SlimeGun;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        internal const int StanceGelMound = 0;
        internal const int StanceFlock = 1;
        internal const int StanceSnowDrift = 2;
        internal const int StanceBladeGate = 3;

        private int Stance => (int)Projectile.ai[0];

        private int BoundMinionType => (int)Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //首帧：节拍参数按形态落位（ai 随生成包，各端一致）+ 立场音（AI 各端都跑，远端也可闻）
            if (Life == 1f) {
                if (Stance is StanceBladeGate or StanceFlock) {
                    Projectile.localNPCHitCooldown = 20;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item46 with { Volume = 0.4f, Pitch = 0.1f },
                        Projectile.Center);
                }
            }

            //剑门为 60 帧一次性：owner 端到时收门（Kill 广播兜底远端），不走续命
            if (Stance == StanceBladeGate) {
                if (Life >= 60f && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                    return;
                }
            }
            //续命：各端按同一条件判定
            else if (Projectile.timeLeft < 90
                && GameModeSystem.GodSmithActive
                && MinionDoctrine.GetCommand(Projectile.owner) == MinionDoctrine.CommandRally
                && OwnerKeepsMinion()) {
                Projectile.timeLeft = 150;
            }
        }

        /// <summary>绑定武器的仆从仍在场（ownedProjectileCounts 各端一致维护）</summary>
        private bool OwnerKeepsMinion() {
            Player owner = Main.player[Projectile.owner];
            return owner.active && BoundMinionType > 0
                && owner.ownedProjectileCounts[BoundMinionType] > 0;
        }

        /// <summary>形态判定矩形</summary>
        private Rectangle Zone => Stance switch {
            //胶垛：旗点贴地的弹性胶堆
            StanceGelMound => CenteredRect(96, 52, 14),
            //鸟群：盘旋圈用大方形近似（半径 120）
            StanceFlock => CenteredRect(220, 200, 0),
            //雪障：横向雪堆
            StanceSnowDrift => CenteredRect(110, 66, 6),
            //剑门：竖立门框
            _ => CenteredRect(34, 116, 0),
        };

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Zone.Intersects(targetHitbox);

        private Rectangle CenteredRect(int width, int height, int sink)
            => new((int)(Projectile.Center.X - width / 2f),
                (int)(Projectile.Center.Y - height / 2f + sink), width, height);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            switch (Stance) {
                case StanceGelMound:
                    target.AddBuff(BuffID.Slimed, 120);
                    break;
                case StanceSnowDrift:
                    target.AddBuff(BuffID.Frostburn, 180);
                    break;
            }
        }

        /// <summary>区域尺寸提示：按形态取一张原版贴图拉伸到判定矩形画一笔（lightColor 着色，进出场渐隐）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Life / 10f, 0f, 1f);
            //剑门以本地 Life 收口（timeLeft 只作续命载体），持续场以 timeLeft 收口
            float fadeOut = Stance == StanceBladeGate
                ? MathHelper.Clamp((60f - Life) / 12f, 0f, 1f)
                : MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            if (fade <= 0.01f) {
                return false;
            }
            int texType = Stance switch {
                StanceFlock => ProjectileID.HarpyFeather,
                StanceSnowDrift => ProjectileID.SnowBallFriendly,
                StanceBladeGate => ProjectileID.Smolstar,
                _ => ProjectileID.SlimeGun,
            };
            Main.instance.LoadProjectile(texType);
            Texture2D tex = TextureAssets.Projectile[texType].Value;
            Rectangle zone = Zone;
            Main.EntitySpriteDraw(tex, zone.Center.ToVector2() - Main.screenPosition, null,
                lightColor * fade, 0f, tex.Size() / 2f,
                new Vector2(zone.Width / (float)tex.Width, zone.Height / (float)tex.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
