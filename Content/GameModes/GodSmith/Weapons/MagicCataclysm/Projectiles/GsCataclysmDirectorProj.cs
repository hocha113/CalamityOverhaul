using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 灾变演出主控基类：蓄势/爆发/余韵三段编排。<br/>
    /// 相位计时器放 ai[0]（各端 AI 同步自增，随生成包与晚加入快照过线，远端确定性重建）；
    /// ai[1]/ai[2] 是子类的演出参数槽。timeLeft 在 SetDefaults 定死（各端一致），此后禁改。<br/>
    /// damage 承载武器基伤：自身持续判定的倍率走 <see cref="TickDamageMul"/>，
    /// 子波弹幕在生成时按倍率折算。子弹幕生成守 owner 端，buff/位移守权威端，粒子音效守非服务器
    /// </summary>
    internal abstract class GsCataclysmDirectorProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicCataclysm";

        /// <summary>蓄势段时长</summary>
        public abstract int OmenTicks { get; }
        /// <summary>爆发段时长</summary>
        public abstract int MainTicks { get; }
        /// <summary>余韵段时长</summary>
        public abstract int AftermathTicks { get; }

        public int TotalTicks => OmenTicks + MainTicks + AftermathTicks;

        /// <summary>相位计时器（各端同步自增）</summary>
        protected ref float Timer => ref Projectile.ai[0];

        protected int Elapsed => (int)Timer;

        /// <summary>0 蓄势 / 1 爆发 / 2 余韵</summary>
        protected int Phase => Elapsed < OmenTicks ? 0 : Elapsed < OmenTicks + MainTicks ? 1 : 2;

        protected Player Owner => Main.player[Projectile.owner];

        /// <summary>子弹幕生成守门：本弹幕属于本地玩家</summary>
        protected bool OwnerSide => Projectile.IsOwnedByLocalPlayer();

        /// <summary>buff 施加与 NPC 位移的权威端（服务器或单机）</summary>
        protected static bool Authoritative => !VaultUtils.isClient;

        /// <summary>演出跟随玩家（锚定类返回 false）</summary>
        protected virtual bool FollowOwner => false;

        /// <summary>自身持续判定的 tick 间隔（localNPCHitCooldown）</summary>
        protected virtual int HitTickRate => 12;

        /// <summary>自身持续判定的伤害倍率</summary>
        protected virtual float TickDamageMul => 1f;

        /// <summary>大幅面演出防屏缘剔除</summary>
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public sealed override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = HitTickRate;
            Projectile.netImportant = true;
            //驻场寿命生成时定死（各端 SetDefaults 一致），演出内不许再改
            Projectile.timeLeft = TotalTicks + 30;
            SetCataclysmDefaults();
        }

        /// <summary>子类补充默认值（禁改 timeLeft）</summary>
        protected virtual void SetCataclysmDefaults() { }

        public sealed override void AI() {
            UpdateAnchor();
            if (!Projectile.active) {
                //锚点更新中演出被散场（如跟随类玩家亡）
                return;
            }
            Projectile.velocity = Vector2.Zero;
            int elapsed = Elapsed;
            if (elapsed >= TotalTicks) {
                Projectile.Kill();
                return;
            }
            if (elapsed < OmenTicks) {
                OmenUpdate(elapsed);
            }
            else if (elapsed < OmenTicks + MainTicks) {
                MainUpdate(elapsed - OmenTicks);
            }
            else {
                AftermathUpdate(elapsed - OmenTicks - MainTicks);
            }
            Timer += 1f;
        }

        /// <summary>锚点更新：跟随类贴玩家，玩家亡则演出散场；锚定类不动</summary>
        protected virtual void UpdateAnchor() {
            if (!FollowOwner) {
                return;
            }
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = Owner.Center;
        }

        /// <summary>蓄势段（t 为相内帧，无伤 telegraph）</summary>
        protected abstract void OmenUpdate(int t);

        /// <summary>爆发段（t 为相内帧，主要伤害）</summary>
        protected abstract void MainUpdate(int t);

        /// <summary>余韵段（t 为相内帧，低频残留）</summary>
        protected abstract void AftermathUpdate(int t);

        /// <summary>蓄势相一律无伤；爆发/余韵相位交给子类判定形状</summary>
        public override bool? CanDamage() => Phase == 0 ? false : null;

        /// <summary>director 默认无矩形盒命中：子类必须给出自定义命中形状</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.FinalDamage *= TickDamageMul;

        /// <summary>按倍率折算的子弹幕伤害</summary>
        protected int ScaledDamage(float mul) => System.Math.Max(1, (int)(Projectile.damage * mul));

        /// <summary>自 worldPos 向下探地，返回地表世界 y（tile 各端一致，结果确定）</summary>
        protected static float FindGroundY(Vector2 worldPos, int maxTiles = 42) {
            int tx = (int)(worldPos.X / 16f);
            int ty = System.Math.Max(1, (int)(worldPos.Y / 16f));
            for (int i = 0; i < maxTiles; i++) {
                int y = ty + i;
                if (!WorldGen.InWorld(tx, y, 10)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return y * 16f;
                }
            }
            return worldPos.Y + maxTiles * 16f;
        }

        /// <summary>identity 定相的伪随机（绘制路径专用，禁 Main.rand）</summary>
        protected float Hash01(int salt) {
            uint h = (uint)(Projectile.identity * 747796405 + salt * 2891336453);
            h = (h >> 13) ^ h;
            h = h * 1274126177u + 247464247u;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
