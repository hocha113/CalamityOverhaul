using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// 弹幕扫描数据实现
    /// <br/>用于分析世界中的 Projectile 实体，不默认提供可上传协议
    /// </summary>
    internal class ProjectileScannable : IHackTarget
    {
        public int ProjectileIndex { get; }

        public ProjectileScannable(int projectileIndex) {
            ProjectileIndex = projectileIndex;
        }

        public Vector2 WorldCenter {
            get {
                if (!IsValid) return Vector2.Zero;
                return Main.projectile[ProjectileIndex].Center;
            }
        }

        public bool IsValid {
            get {
                if (ProjectileIndex < 0 || ProjectileIndex >= Main.maxProjectiles) return false;
                Projectile projectile = Main.projectile[ProjectileIndex];
                return projectile.active && projectile.type != ProjectileID.None;
            }
        }

        public bool IsHackable => false;

        public int ScanRowCount => 10;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            if (!IsValid) return;
            Projectile projectile = Main.projectile[ProjectileIndex];

            labels[0] = HackTime.ProjectileScanName.Value;
            values[0] = GetProjectileName(projectile);
            colors[0] = HackTheme.TextBright;

            labels[1] = HackTime.ProjectileScanClass.Value;
            values[1] = GetProjectileClass(projectile);
            colors[1] = GetProjectileClassColor(projectile);

            labels[2] = HackTime.DmgLabel.Value;
            values[2] = $"{projectile.damage}";
            colors[2] = projectile.damage > 0 ? HackTheme.Uploading : HackTheme.TextDim;

            labels[3] = HackTime.ProjectileScanSpeed.Value;
            Vector2 displayVel = TimeFreezes.WorldFreezeSystem.IsActive && TimeFreezes.WorldFreezeSystem.ProjSnapshotCaptured[ProjectileIndex]
                ? TimeFreezes.WorldFreezeSystem.ProjFrozenVelocities[ProjectileIndex]
                : projectile.velocity;
            values[3] = $"{displayVel.Length():F1} px/f";
            colors[3] = HackTheme.Accent;

            labels[4] = HackTime.ProjectileScanKnockback.Value;
            values[4] = $"{projectile.knockBack:F1}";
            colors[4] = HackTheme.TextBright;

            labels[5] = HackTime.ProjectileScanPenetrate.Value;
            values[5] = projectile.penetrate < 0 ? HackTime.ProjectileScanInfinite.Value : $"{projectile.penetrate}";
            colors[5] = projectile.penetrate < 0 ? HackTheme.AccentAlt : HackTheme.TextBright;

            labels[6] = HackTime.ProjectileScanTimeLeft.Value;
            values[6] = $"{projectile.timeLeft / 60f:F1}s";
            colors[6] = projectile.timeLeft <= 60 ? HackTheme.Uploading : HackTheme.TextBright;

            labels[7] = HackTime.ProjectileScanOwner.Value;
            values[7] = GetOwnerName(projectile);
            colors[7] = HackTheme.TextBright;

            labels[8] = HackTime.ProjectileScanAI.Value;
            values[8] = $"Style {projectile.aiStyle} / Type {projectile.type}";
            colors[8] = HackTheme.TextDim;

            labels[9] = HackTime.ProjectileScanPosition.Value;
            values[9] = $"{(int)projectile.Center.X}, {(int)projectile.Center.Y}";
            colors[9] = HackTheme.TextDim;
        }

        public HackTargetType TargetType => HackTargetType.Get<ProjectileTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                if (!IsValid) return Vector2.Zero;
                Projectile projectile = Main.projectile[ProjectileIndex];
                return new Vector2(
                    Math.Max(projectile.width, 16) * 0.6f + 24f,
                    Math.Max(projectile.height, 16) * 0.6f + 24f);
            }
        }

        public string LockFrameTitle => IsValid ? GetProjectileName(Main.projectile[ProjectileIndex]) : string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            if (!IsValid) return false;
            Projectile projectile = Main.projectile[ProjectileIndex];
            text = GetProjectileClass(projectile);
            color = GetProjectileClassColor(projectile);
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) => false;

        public bool TargetEquals(IHackTarget other) {
            return other is ProjectileScannable p && p.ProjectileIndex == ProjectileIndex;
        }

        private static string GetProjectileName(Projectile projectile) {
            string name = Lang.GetProjectileName(projectile.type).Value;
            if (!string.IsNullOrEmpty(name) && name != $"ProjectileName.{projectile.type}") return name;

            ModProjectile modProjectile = ProjectileLoader.GetProjectile(projectile.type);
            if (modProjectile != null) return modProjectile.Name;

            return $"Projectile #{projectile.type}";
        }

        private static string GetProjectileClass(Projectile projectile) {
            if (projectile.minion) return HackTime.ProjectileScanMinion.Value;
            if (projectile.sentry) return HackTime.ProjectileScanSentry.Value;
            if (projectile.trap) return HackTime.ProjectileScanTrap.Value;
            if (projectile.hostile) return HackTime.ProjectileScanHostile.Value;
            if (projectile.friendly) return HackTime.ProjectileScanFriendly.Value;
            return HackTime.ProjectileScanNeutral.Value;
        }

        private static Color GetProjectileClassColor(Projectile projectile) {
            if (projectile.hostile || projectile.trap) return HackTheme.Danger;
            if (projectile.minion || projectile.sentry) return HackTheme.AccentAlt;
            if (projectile.friendly) return HackTheme.Accent;
            return HackTheme.TextDim;
        }

        private static string GetOwnerName(Projectile projectile) {
            if (projectile.Alives()) {
                IEntitySource source = projectile.CWR().Source;
                if (source != null) {
                    if (source is EntitySource_ItemUse itemUseSource && itemUseSource.Item != null) {
                        string itemName = Lang.GetItemNameValue(itemUseSource.Item.type);
                        string playerName = itemUseSource.Player?.name ?? "?";
                        return $"{playerName} [{itemName}]";
                    }
                    if (source is EntitySource_Parent parentSource) {
                        switch (parentSource.Entity) {
                            case Player p when p.active:
                                return p.name;
                            case NPC n when n.active:
                                return n.TypeName;
                            case Projectile proj when proj.active:
                                return GetProjectileName(proj);
                        }
                    }
                    if (source is EntitySource_Misc miscSource && !string.IsNullOrEmpty(miscSource.Context)) {
                        return miscSource.Context;
                    }
                }
            }
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers) {
                Player owner = Main.player[projectile.owner];
                if (owner != null && owner.active && !string.IsNullOrEmpty(owner.name)) return owner.name;
            }
            return HackTime.ProjectileScanOwnerWorld.Value;
        }
    }
}
