using System;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    //RecipeData
    public class RecipeData
    {
        public int Target { get; set; }
        public string[] Values { get; set; }
        private int? _cachedHashCode;
        public int[] MaterialTypesCache;//缓存
        public static void Register(string[] recipes) {
            RecipeData recipeData = new RecipeData {
                Values = recipes,
                Target = VaultUtils.GetItemTypeFromFullName(recipes[^1])
            };
            recipeData.BuildMaterialTypesCache();
            SupertableUI.AllRecipes.Add(recipeData);
        }
        public void BuildMaterialTypesCache() {
            if (Values == null) {
                return;
            }
            if (MaterialTypesCache != null && MaterialTypesCache.Length == Values.Length) {
                return;
            }
            MaterialTypesCache = new int[Values.Length];
            for (int i = 0; i < Values.Length; i++) {
                MaterialTypesCache[i] = VaultUtils.GetItemTypeFromFullName(Values[i]);
            }
        }
        public static bool operator ==(RecipeData left, RecipeData right) {
            if (ReferenceEquals(left, right)) {
                return true;
            }
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) {
                return false;
            }
            return left.Equals(right);
        }
        public static bool operator !=(RecipeData left, RecipeData right) => !(left == right);
        public override bool Equals(object obj) {
            if (obj is not RecipeData other) {
                return false;
            }
            if (Target != other.Target || Values.Length != other.Values.Length) {
                return false;
            }
            for (int i = 0; i < Values.Length; i++) {
                if (!string.Equals(Values[i], other.Values[i])) {
                    return false;
                }
            }
            return true;
        }
        public override int GetHashCode() {
            if (_cachedHashCode.HasValue) {
                return _cachedHashCode.Value;
            }
            int hash = Target.GetHashCode();
            const int MaxHashElements = 5;
            for (int i = 0; i < Math.Min(Values.Length, MaxHashElements); i++) {
                hash = hash * 31 + (Values[i]?.GetHashCode() ?? 0);
            }
            _cachedHashCode = hash;
            return hash;
        }
    }
}
