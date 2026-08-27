using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 掷星预瞄线:蓄势期从主星伸出的星屑虚线,先追瞄后锁死(预告即承诺)<br/>
    /// ai[0]=主星whoAmI ai[1]/ai[2]=瞄准点世界坐标(权威端追瞄期每帧写,锁定后冻结)<br/>
    /// 寿命=Lifetime 定值,与投掷态出手拍对齐;末 LockFrames 帧为锁定窗,虚线转实线白热,
    /// 出手方向由投掷态直读本弹幕的锁定点,画的就是飞的
    /// </summary>
    internal class CultistPlanetAimLine : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>总寿命(帧),投掷态按 Windup-Lifetime 拍生成,归零帧即出手帧</summary>
        internal const int Lifetime = 48;
        /// <summary>锁定窗(帧):瞄准点冻结,线转实体白热</summary>
        internal const int LockFrames = 14;
        /// <summary>线长(px)</summary>
        private const float LineLength = 2400f;

        private int PlanetWho => (int)Projectile.ai[0];
        private Vector2 AimPoint => new(Projectile.ai[1], Projectile.ai[2]);
        private bool Locked => Projectile.timeLeft <= LockFrames;

        private bool lockBeatDone;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
            //配合 DrawBehind 设 hide,免得普通弹幕层重复画一遍预瞄线
            Projectile.hide = true;
        }

        private Projectile Planet {
            get {
                if (PlanetWho < 0 || PlanetWho >= Main.maxProjectiles) {
                    return null;
                }
                Projectile planet = Main.projectile[PlanetWho];
                return planet.active && planet.type == ModContent.ProjectileType<CultistPlanetProj>() ? planet : null;
            }
        }

        public override void AI() {
            Projectile planet = Planet;
            //主星没了或已出手(段离开收势待掷/聚阵)即散
            if (planet == null || (int)planet.ai[2] % 10 is not (3 or 7)) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = planet.Center;
            Projectile.velocity = Vector2.Zero;

            //追瞄期(权威端):瞄准点=与出手同参的线性预判,锁定窗后冻结
            if (!VaultUtils.isClient && !Locked) {
                NPC owner = (int)planet.ai[1] >= 0 && (int)planet.ai[1] < Main.maxNPCs
                    ? Main.npc[(int)planet.ai[1]] : null;
                Player target = owner != null && owner.active && owner.target >= 0 && owner.target < 255
                    ? Main.player[owner.target] : null;
                if (target.Alives()) {
                    Vector2 aim = CultistMotion.PredictTarget(target, planet.Center, 9f, 0.55f);
                    Projectile.ai[1] = aim.X;
                    Projectile.ai[2] = aim.Y;
                    if (Projectile.timeLeft % 4 == 0 || Projectile.timeLeft == LockFrames + 1) {
                        Projectile.netUpdate = true;
                    }
                }
            }

            //锁定拍(各端由 timeLeft 本地推,一次);幻星祭仪:锁定即揭示挂点星的真容(识真窗)
            if (Locked && !lockBeatDone) {
                lockBeatDone = true;
                int palette = PaletteOf(planet);
                CultistMotion.SigilCommitFX(planet.Center, CultistMotion.PhaseCore(palette), 1.1f);
                CultistMotion.Shake(planet.Center, 2.5f, 8);
                if (planet.ModProjectile is CultistPlanetProj planetBody) {
                    planetBody.RevealIdentity();
                }
            }
        }

        /// <summary>读指定星球预瞄线的瞄准点(权威端,幻星祭仪逐星出手);没找到返回 null</summary>
        internal static Vector2? GetLockedAimFor(int planetWho) {
            int type = ModContent.ProjectileType<CultistPlanetAimLine>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == planetWho) {
                    return new Vector2(proj.ai[1], proj.ai[2]);
                }
            }
            return null;
        }

        /// <summary>读锁定的瞄准点(权威端,投掷态出手时调用);没找到返回 null</summary>
        internal static Vector2? GetLockedAim(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetAimLine>();
            int planetType = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != type) {
                    continue;
                }
                int planetWho = (int)proj.ai[0];
                if (planetWho < 0 || planetWho >= Main.maxProjectiles) {
                    continue;
                }
                Projectile planet = Main.projectile[planetWho];
                if (planet.active && planet.type == planetType && (int)planet.ai[1] == ownerWho) {
                    return new Vector2(proj.ai[1], proj.ai[2]);
                }
            }
            return null;
        }

        private static int PaletteOf(Projectile planet) => (int)planet.ai[0];

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            //与主星同层:线是星的延伸,压在本体与弹幕之下
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            Projectile planet = Planet;
            if (planet == null) {
                return false;
            }
            int palette = PaletteOf(planet);
            Color mid = CultistMotion.PhaseCore(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Color deep = Color.Lerp(CultistMotion.PhaseEdge(palette), Color.Black, 0.45f);

            float planetR = planet.ModProjectile is CultistPlanetProj pp ? pp.VisRadius * planet.scale : 220f;
            Vector2 dir = (AimPoint - planet.Center).SafeNormalize(Vector2.UnitY);
            Vector2 root = planet.Center + dir * planetR * 0.92f - Main.screenPosition;
            Vector2 end = root + dir * LineLength;

            //出生淡入+锁定窗过载:虚线追瞄→实线白热(危险语调升级)
            float bornIn = MathHelper.Clamp((Lifetime - Projectile.timeLeft) / 8f, 0f, 1f);
            float lockT = Locked ? MathHelper.Clamp((LockFrames - Projectile.timeLeft) / (float)LockFrames + 0.4f, 0f, 1f) : 0f;
            float dash = Locked ? 0f : 16f;
            float alpha = (Locked ? 0.95f : 0.55f) * bornIn;
            float halfWidth = Locked ? 13f + lockT * 4f : 9f;

            Vector2[] pts = [root, end];
            float[] widths = [halfWidth, halfWidth];
            float[] alphas = [alpha, alpha * 0.55f];

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            Rendering.CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                deep, mid, bright, 1f, dash, lockT, Projectile.identity % 100 * 0.077f, alpha);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //瞄准点落标:追瞄期微星,锁定后亮起(玩家读"要打这里")
            Rendering.CultistOrreryRenderer.DrawStarBead(sb, AimPoint - Main.screenPosition, mid,
                CultistMotion.PhaseEdge(palette), (Locked ? 0.26f : 0.15f) * bornIn,
                (Locked ? 0.9f : 0.5f) * bornIn, Main.GlobalTimeWrappedHourly * 2.2f);
            return false;
        }
    }
}
