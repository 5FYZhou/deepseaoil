#if UNITY_EDITOR
using DeepseaOil.Presentation.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.EditorTools.Lighting
{
    /// <summary>网格光照场（新范式）一键装配：卸载旧点光池、挂新的 TileGridLightmap</summary>
    /// <remarks>旧点光组件的 Tick 由 CombatRoot 驱动，但残留组件会让人误以为点光源仍是生效配置，必须物理移除。</remarks>
    public static class TileLightmapSetupTool
    {
        private const string MenuPath = "DeepseaOil/光照与后处理/切换至网格光照场系统(新范式)";
        private const string DebugMenuPath = "DeepseaOil/光照与后处理/诊断/统计网格光照场";
        private const string EnvironmentRootName = "[Environment]";

        [MenuItem(MenuPath)]
        public static void SetupTileGridLightmap()
        {
            GameObject environment = FindEnvironmentRoot();

            if (environment == null)
            {
                Debug.LogError(
                    $"[Light] 找不到 {EnvironmentRootName}：先跑「DeepseaOil ▸ 光照与后处理 ▸ 一键装配当前场景环境基座」建出光照基座，" +
                    "再切网格光照场。这里不自动新建，免得造出一个没有 Global Light 2D 的空壳。");

                return;
            }

            RemoveLegacyPointLights(environment);

            TileGridLightmap lightmap = environment.GetComponent<TileGridLightmap>();

            if (lightmap == null)
            {
                lightmap = environment.AddComponent<TileGridLightmap>();

                Undo.RegisterCreatedObjectUndo(lightmap, "Add TileGridLightmap");
            }

            EditorUtility.SetDirty(environment);

            Debug.Log($"[Light] 新范式装配完成：旧点光源池已卸载，{environment.name} 上已挂载 TileGridLightmap（几何与网格边界由 CombatRoot 在运行期接线）。");
        }

        /// <summary>诊断：网格光照场规模与场景里还剩几盏 Light2D</summary>
        /// <remarks>剩 0 盏点光才算彻底告别 16 盏上限；只剩 Global 那一盏是期望值。</remarks>
        [MenuItem(DebugMenuPath)]
        public static void ReportTileGridLightmap()
        {
            TileGridLightmap[] lightmaps = Object.FindObjectsOfType<TileGridLightmap>();

            if (lightmaps.Length == 0)
            {
                Debug.Log("[Light] 当前场景没有 TileGridLightmap：新范式未装配（PlayMode 下 CombatRoot 会自建一个）。");

                return;
            }

            for (int i = 0; i < lightmaps.Length; i++)
            {
                TileGridLightmap lightmap = lightmaps[i];

                Debug.Log(
                    $"[Light] {lightmap.gameObject.name}：光照图 {lightmap.TexelWidth}×{lightmap.TexelHeight} 纹素，" +
                    $"发光格 {lightmap.EmitterCellCount} 个，光阻格 {lightmap.BlockedCellCount} 个（连片火海共用一个光区，无灯具数量上限）。", lightmap);
            }

            TileLightDirector[] legacy = Object.FindObjectsOfType<TileLightDirector>();

            if (legacy.Length > 0)
            {
                Debug.LogWarning($"[Light] 场景里还有 {legacy.Length} 个 TileLightDirector：点光池已无人驱动，但仍建议跑一次装配菜单物理移除。");
            }

            Light2D[] lights = Object.FindObjectsOfType<Light2D>();
            int globalLights = 0;
            int pointLights = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].lightType == Light2D.LightType.Global) globalLights++;
                else pointLights++;
            }

            Debug.Log($"[Light] 场景 Light2D 统计：全局 {globalLights} 盏（应为 1），点光/其它 {pointLights} 盏（应为 0）。");
        }

        /// <summary>清掉旧点光管理器与它残留的灯具子对象；全局灯保留</summary>
        private static void RemoveLegacyPointLights(GameObject environment)
        {
            TileLightDirector legacy = environment.GetComponent<TileLightDirector>();

            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy);

                Debug.Log("[Light] 已卸载旧 TileLightDirector（点光源池）。");
            }

            Light2D[] lights = environment.GetComponentsInChildren<Light2D>(true);

            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];

                if (light == null || light.lightType == Light2D.LightType.Global) continue;

                Undo.DestroyObjectImmediate(light.gameObject);
            }
        }

        private static GameObject FindEnvironmentRoot()
        {
            GameObject named = GameObject.Find(EnvironmentRootName);

            if (named != null) return named;

            EnvironmentDirector director = Object.FindObjectOfType<EnvironmentDirector>();

            return director != null ? director.gameObject : null;
        }
    }
}
#endif
