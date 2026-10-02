// ---------------------------------------------------------------------------
// 一键搭建「移动 + 相机」测试场景（编辑器工具，不进构建产物）
//
// 【为什么用它】这一阶段要接的东西有十几处（玩家 4 个组件、边界、相机 3 件、调试面板、
//   测试地形），手点必漏，而漏了都不报错、只表现为"不动"或"不跟随"。
//   本工具按与 SampleScene 一致的接法一次建好，并顺手把地形摆出来让"碰撞阻挡"可当场验证。
//
// 【与 Assets/Editor/ 的关系】本文件要引用 PlayerController / MovementMotor 等运行时类型，
//   而 Assets/Editor/ 被 DeepseaOil.EditorTools.asmdef 覆盖（asmdef 引用不了 Assembly-CSharp）。
//   所以与其它测试工具一起放在本目录：无 asmdef → Assembly-CSharp-Editor，两边都够得着。
//
// 【安全性】只建对象、不改已有对象；可反复执行，会先删掉自己上次建的同名根对象。
//
// 跑法：菜单 Tools ▸ 深海石油 ▸ 搭建移动测试场景
// ---------------------------------------------------------------------------

using System;
using DeepseaOil.Data;
using DeepseaOil.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepseaOil.Tests
{
    public static class MovementSceneBuilder
    {
        private const string PlayerConfigPath = "Assets/ConfigAssets/玩家配置.asset";

        /// <summary>内置方块精灵（Built-in Extra）。GUID 从既有场景核实：SampleScene 的玩家与障碍物用的就是它。</summary>
        private const string BuiltinSquareSpriteGuid = "311925a002f4447b3a28927169b83ea6";

        // 相机参数：俯视角正交 + 极小黑框 + 轻阻尼（与文档给出的推荐值一致）
        private const float OrthoSize = 5f;
        private const float DeadZone = 0.1f;
        private const float SoftZoneWidth = 0.5f;
        private const float SoftZoneHeight = 0.35f;
        private const float Damping = 0.15f;

        private static readonly Vector2 MapSize = new Vector2(40f, 25f);

        [MenuItem("Tools/深海石油/搭建移动测试场景")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var config = AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerConfigPath);
            if (config == null)
            {
                Debug.LogError($"[搭建场景] 找不到玩家配置：{PlayerConfigPath}");
                return;
            }

            // 上一次搭建留下的根对象先清掉，保证可重复执行
            DestroyIfExists("MapBounds");
            DestroyIfExists("Player");
            DestroyIfExists("CameraRig");
            DestroyIfExists("DebugPanel");
            DestroyIfExists("TestGround");

            EnsureMainCamera();

            BuildMapBounds();
            BuildGround();
            GameObject player = BuildPlayer(config);
            BuildCameraRig(player);
            BuildDebugPanel(player);

            // 免得上次 Play 留下的暂停状态把新场景也冻住
            Time.timeScale = 1f;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[搭建场景] 完成。现在按 Play，用 WASD / 方向键移动，左 Shift 冲刺。");
            Debug.Log("[搭建场景] 建好后请执行 Tools ▸ 深海石油 ▸ 校验移动与相机接线 复核一遍。");
        }

        // ---------------------------------------------------------------- 玩家

        private static GameObject BuildPlayer(PlayerConfig config)
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            player.layer = 0;   // Default

            // 先后顺序有意义：Collider/Rigidbody 都先于 MovementMotor，避免依赖 RequireComponent 的补加时机
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;            // 俯视角无重力；逻辑层的 gravityScale 也是 0
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.None;   // 相机阻尼已充当插值器

            var collider = player.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.7f, 0.5f);   // 脚底碰撞体
            collider.offset = new Vector2(0f, -0.25f);
            collider.sharedMaterial = null;            // 摩擦 0，避免贴墙粘住

            // 视觉主体：一个方块先占位，将来换成精灵
            var sprite = player.AddComponent<SpriteRenderer>();
            sprite.sprite = BuiltinSquareSprite();
            sprite.color = new Color(0.95f, 0.85f, 0.35f);
            sprite.sortingOrder = 10;

            var motor = player.AddComponent<MovementMotor>();

            // 输入采样器单独一个物体（与 SampleScene 一致，便于将来挂更多输入相关件）
            var inputGo = new GameObject("InputProvider");
            var inputProvider = inputGo.AddComponent<InputProvider>();

            var controller = player.AddComponent<PlayerController>();
            SetRef(controller, "config", config);
            SetRef(controller, "motor", motor);
            SetRef(controller, "inputProvider", inputProvider);
            SetRef(controller, "boundsArea", FindComponent<BoxCollider2D>("MapBounds"));

            return player;
        }

        // ---------------------------------------------------------------- 地图

        private static void BuildMapBounds()
        {
            var go = new GameObject("MapBounds");
            var box = go.AddComponent<BoxCollider2D>();
            // 只作"区域标记"，不是实体墙：玩家位置由 PlayerController 的边界钳位兜底，
            // 让它可碰撞反而会让物理引擎与钳位两个写者互相打架。
            box.isTrigger = true;
            box.size = MapSize;
            box.offset = Vector2.zero;

            // Confiner2D 只接受 PolygonCollider2D / CompositeCollider2D（BoxCollider2D 会报红字），
            // 所以另挂一个子物体放多边形；BoxCollider2D 继续给 BoundsArea 用。
            var shapeGo = new GameObject("BoundsShape");
            shapeGo.transform.SetParent(go.transform, false);
            shapeGo.transform.position = Vector3.zero;

            var polygon = shapeGo.AddComponent<PolygonCollider2D>();
            polygon.isTrigger = true;   // 同样只是形状载体，不参与碰撞
            polygon.points = RectanglePath(MapSize);   // 单路径多边形，直接给点即可

            // 不挂渲染器：它不可见。
        }

        /// <summary>以原点为中心、尺寸为 <paramref name="size"/> 的矩形顶点（顺时针）。</summary>
        private static Vector2[] RectanglePath(Vector2 size)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;

            return new[]
            {
                new Vector2(-hx, -hy),
                new Vector2(-hx, hy),
                new Vector2(hx, hy),
                new Vector2(hx, -hy),
            };
        }

        private static void BuildGround()
        {
            var root = new GameObject("TestGround");

            AddBlock(root.transform, "Floor", Vector2.zero, MapSize, new Color(0.20f, 0.24f, 0.30f), -100);
            AddBlock(root.transform, "Obstacle_A", new Vector2(-6f, 2f), new Vector2(3f, 1.5f), new Color(0.55f, 0.35f, 0.25f), 0);
            AddBlock(root.transform, "Obstacle_B", new Vector2(5f, -3f), new Vector2(4f, 1f), new Color(0.55f, 0.35f, 0.25f), 0);
            AddBlock(root.transform, "Obstacle_C", new Vector2(0f, 6f), new Vector2(1.5f, 3f), new Color(0.55f, 0.35f, 0.25f), 0);
            AddBlock(root.transform, "Obstacle_D", new Vector2(12f, 8f), new Vector2(6f, 1f), new Color(0.55f, 0.35f, 0.25f), 0);
        }

        private static void AddBlock(
            Transform parent,
            string name,
            Vector2 center,
            Vector2 size,
            Color color,
            int sortingOrder
            )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = BuiltinSquareSprite();
            sprite.color = color;
            sprite.sortingOrder = sortingOrder;

            // 只有障碍物要挡人；地面不挂碰撞体（俯视角地面不是碰撞面）
            if (sortingOrder >= 0)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = Vector2.one;   // 缩放已由 transform.localScale 承担
            }
        }

        // ---------------------------------------------------------------- 相机

        private static void EnsureMainCamera()
        {
            Camera main = Camera.main;
            if (main == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                main = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            main.orthographic = true;
            main.orthographicSize = OrthoSize;
            main.transform.position = new Vector3(0f, 0f, -10f);
            main.clearFlags = CameraClearFlags.SolidColor;
            main.backgroundColor = new Color(0.10f, 0.12f, 0.15f);

            AddComponentByName(main.gameObject, "Cinemachine.CinemachineBrain", "CinemachineBrain");
        }

        private static void BuildCameraRig(GameObject player)
        {
            // 场景里可能留着手动加的 vcam（例如名为 cm 的对象）。两个 vcam 会争夺同一个 Brain，
            // 而校验菜单只看第一个——先报出来，别让它变成看不见的隐患。
            WarnAboutExtraVirtualCameras();

            var rig = new GameObject("CameraRig");

            // 必须是根对象：挂到别的 vcam 下面时，它的世界 z = 父级 z + 本机 z，
            // 相机就会被推到玩家背后（画面全空），而 Unity 不会报任何错。
            if (rig.transform.parent != null) rig.transform.SetParent(null, false);

            rig.transform.position = new Vector3(0f, 0f, -10f);

            Component vcam = AddComponentByName(rig, "Cinemachine.CinemachineVirtualCamera", "CinemachineVirtualCamera");
            if (vcam == null) return;   // AddComponentByName 已报错

            var serialized = new SerializedObject(vcam);

            SerializedProperty follow = serialized.FindProperty("m_Follow");
            if (follow != null) follow.objectReferenceValue = player.transform;
            else Debug.LogError("[搭建场景] vcam 上找不到 m_Follow 字段（Cinemachine 版本变了？）——请手动把 Follow 拖成 Player");

            // Lens：正交 + 尺寸。字段名与 SerializedProperty 的路径由 Cinemachine 决定，取不到就报出来。
            SerializedProperty lens = serialized.FindProperty("m_Lens");
            if (lens != null)
            {
                SerializedProperty ortho = lens.FindPropertyRelative("Orthographic");
                SerializedProperty size = lens.FindPropertyRelative("OrthographicSize");
                if (ortho != null) ortho.boolValue = true;
                if (size != null) size.floatValue = OrthoSize;
            }
            else
            {
                Debug.LogError("[搭建场景] vcam 上找不到 m_Lens——请在 Inspector 里手动勾 Orthographic 并把 Size 设为 " + OrthoSize);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Body = Framing Transposer（死区 / 软区 / 阻尼都在它身上）
            Component body = AddComponentByName(rig, "Cinemachine.CinemachineFramingTransposer", "CinemachineFramingTransposer");
            if (body != null)
            {
                var bodySerialized = new SerializedObject(body);
                SetIfExists(bodySerialized, "m_DeadZoneWidth", DeadZone);
                SetIfExists(bodySerialized, "m_DeadZoneHeight", DeadZone);
                SetIfExists(bodySerialized, "m_SoftZoneWidth", SoftZoneWidth);
                SetIfExists(bodySerialized, "m_SoftZoneHeight", SoftZoneHeight);
                SetIfExists(bodySerialized, "m_XDamping", Damping);
                SetIfExists(bodySerialized, "m_YDamping", Damping);
                SetIfExists(bodySerialized, "m_CameraDistance", 10f);
                bodySerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // Extensions：Confiner 2D 绑到 MapBounds 的多边形子物体（不是 BoxCollider2D——那会报红字）
            Component confiner = AddComponentByName(rig, "Cinemachine.CinemachineConfiner2D", "CinemachineConfiner2D");
            if (confiner != null)
            {
                var confinerSerialized = new SerializedObject(confiner);
                SerializedProperty shape = confinerSerialized.FindProperty("m_BoundingShape2D");
                PolygonCollider2D boundsShape = FindComponent<PolygonCollider2D>("BoundsShape");

                if (shape == null)
                {
                    Debug.LogError("[搭建场景] Confiner2D 上找不到 m_BoundingShape2D 字段——请手动把 Bounding Shape 2D 拖成 MapBounds 下的 BoundsShape");
                }
                else if (boundsShape == null)
                {
                    Debug.LogError("[搭建场景] 找不到 BoundsShape（PolygonCollider2D）——请手动在 MapBounds 下建一个多边形碰撞体再拖给 Confiner");
                }
                else
                {
                    shape.objectReferenceValue = boundsShape;
                }

                confinerSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log(
                "[搭建场景] 相机已配：Follow = " + player.name
                + "，正交 Size = " + OrthoSize
                + "，Confiner = " + (confiner == null ? "未加" : "已加到 BoundsShape")
                );
        }

        /// <summary>场景里除即将新建的之外还有别的 vcam 时报警：两个 vcam 会争同一个 Brain。</summary>
        private static void WarnAboutExtraVirtualCameras()
        {
            Type vcamType = FindType("Cinemachine.CinemachineVirtualCamera");
            if (vcamType == null) return;

            foreach (var component in UnityEngine.Object.FindObjectsOfType(vcamType))
            {
                var behaviour = component as Component;
                if (behaviour == null) continue;

                Debug.LogWarning(
                    $"[搭建场景] 场景里已有一个虚拟相机在 {behaviour.gameObject.name} 上——"
                    + "两个 vcam 会争夺同一个 Brain，请删掉多余的那个（保留工具建的 CameraRig）。"
                    );
            }
        }

        private static void BuildDebugPanel(GameObject player)
        {
            var go = new GameObject("DebugPanel");
            var panel = go.AddComponent<MovementDebugPanel>();
            SetRef(panel, "controller", player.GetComponent<PlayerController>());
        }

        // ---------------------------------------------------------------- 工具

        private static void DestroyIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        }

        /// <summary>取内置方块精灵；取不到返回 null（不报错——没有精灵也能验接线）。</summary>
        private static Sprite BuiltinSquareSprite()
        {
            string path = AssetDatabase.GUIDToAssetPath(BuiltinSquareSpriteGuid);
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite) return sprite;
            }

            return null;
        }

        private static T FindComponent<T>(string objectName) where T : Component
        {
            var go = GameObject.Find(objectName);
            return go == null ? null : go.GetComponent<T>();
        }

        /// <summary>用完整类型名反射添加组件：Cinemachine 未安装时给出人话报错而不是编译失败。</summary>
        private static Component AddComponentByName(GameObject go, string typeName, string friendlyName)
        {
            Type type = FindType(typeName);

            if (type == null)
            {
                Debug.LogError(
                    $"[搭建场景] 找不到 {friendlyName}（{typeName}）。"
                    + "请先在 Package Manager 里安装 Cinemachine（2.x），然后重跑本菜单。"
                    );
                return null;
            }

            Component existing = go.GetComponent(type);
            return existing != null ? existing : go.AddComponent(type);
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static void SetIfExists(SerializedObject owner, string propertyName, float value)
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
        }

        private static void SetRef(UnityEngine.Object owner, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[搭建场景] {owner.GetType().Name} 上没有字段 {fieldName}（字段被改名了？）");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}