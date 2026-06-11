#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClawMachine.EditorTools
{
    /// <summary>
    /// สร้าง MVP scene ตู้คีบ 2 ขาทั้งหมดด้วยโค้ด แล้ว wire component ให้ถูกต้องผ่าน Unity API
    /// เมนู: Claw Machine > Build MVP Scene
    ///
    /// ใช้ primitive (cube) แทนโมเดลจริงเพื่อให้กด Play เล่นได้ทันที — เปลี่ยนเป็นโมเดลสวยทีหลังได้
    /// </summary>
    public static class ClawSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PhysMatPath = "Assets/Physics/LowFriction.physicMaterial";
        private const string PrizeLayerName = "Prize";

        [MenuItem("Claw Machine/Build MVP Scene")]
        public static void BuildScene()
        {
            EnsurePrizeLayer();
            ApplyPhysicsProjectSettings();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lowFriction = CreateLowFrictionMaterial();
            int prizeLayer = LayerMask.NameToLayer(PrizeLayerName);

            BuildLighting();
            BuildCabinet(lowFriction);
            BuildBars(lowFriction);
            var chute = BuildWinZone();
            var (claw, grip) = BuildClaw(lowFriction);
            var payout = BuildSystems();
            var (frontCam, sideCam) = BuildCameras();
            SpawnBoxesOnBars(lowFriction, prizeLayer);

            WireClawController(claw, grip, payout);
            WireSystems(claw, grip, payout, chute, frontCam, sideCam);

            // save
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ClawSceneBuilder] สร้าง scene hashi-watashi เสร็จ: " + ScenePath);
            EditorUtility.DisplayDialog("Claw Machine — Hashi-watashi",
                "สร้าง scene คานขนานเสร็จแล้ว!\n\n" +
                "เป้าหมาย: ดันกล่องให้หมุนจนร่วงลงระหว่างคาน\n\n" +
                "Arrows = เลื่อน  Space = ดิ่งคีบ\n" +
                "C = สลับกล้อง  P = บังคับ payout\n\n" +
                "เคล็ดลับ: เล็งที่มุมกล่อง (กล่องขวาเอียงเกือบขนาน = ง่ายสุด)",
                "OK");
        }

        // ---------- Project settings ----------

        private static void EnsurePrizeLayer()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            // ถ้ามีอยู่แล้วข้าม
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == PrizeLayerName) return;

            // ใส่ที่ user layer ว่างตัวแรก (>= 8)
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = PrizeLayerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
            Debug.LogWarning("[ClawSceneBuilder] ไม่มี user layer ว่างสำหรับ 'Prize'");
        }

        private static void ApplyPhysicsProjectSettings()
        {
            // ชนแม่นยำ ขาไม่ทะลุของ (ดู PRD หัวข้อ 4)
            Time.fixedDeltaTime = 0.01f;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
        }

        private static PhysicsMaterial CreateLowFrictionMaterial()
        {
            var mat = new PhysicsMaterial("LowFriction")
            {
                dynamicFriction = 0.15f,
                staticFriction = 0.20f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            Directory.CreateDirectory("Assets/Physics");
            AssetDatabase.CreateAsset(mat, PhysMatPath);
            return mat;
        }

        // ---------- Scene pieces ----------

        private static void BuildLighting()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void BuildCabinet(PhysicsMaterial mat)
        {
            var root = new GameObject("Cabinet").transform;

            // พื้นตู้ (ผิวบนที่ y=0)
            MakeBox("Floor", new Vector3(0f, -0.025f, 0f),
                new Vector3(0.6f, 0.05f, 0.6f), root, mat, new Color(0.2f, 0.2f, 0.22f));

            // ผนัง 4 ด้าน = กระจกใส (โปร่งแสง มองเห็นของข้างใน)
            var glass = CreateGlassMaterial();
            const float h = 0.35f, t = 0.012f, half = 0.3f;
            var walls = new[]
            {
                MakeBox("Glass_Back", new Vector3(0f, h / 2f, half), new Vector3(0.6f, h, t), root, mat, Color.white),
                MakeBox("Glass_Front", new Vector3(0f, h / 2f, -half), new Vector3(0.6f, h, t), root, mat, Color.white),
                MakeBox("Glass_Left", new Vector3(-half, h / 2f, 0f), new Vector3(t, h, 0.6f), root, mat, Color.white),
                MakeBox("Glass_Right", new Vector3(half, h / 2f, 0f), new Vector3(t, h, 0.6f), root, mat, Color.white),
            };
            foreach (var w in walls)
                w.GetComponent<MeshRenderer>().sharedMaterial = glass;
        }

        // กระจกใสสำหรับผนังตู้ — รองรับทั้ง built-in Standard และ URP
        private static Material CreateGlassMaterial()
        {
            var glassColor = new Color(0.55f, 0.78f, 0.95f, 0.14f);
            var std = Shader.Find("Standard");
            if (std != null)
            {
                var m = new Material(std);
                m.SetFloat("_Mode", 3f); // Transparent
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
                m.color = glassColor;
                return m;
            }

            // URP fallback
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            var mm = new Material(urp);
            mm.SetFloat("_Surface", 1f); // Transparent
            mm.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mm.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mm.SetInt("_ZWrite", 0);
            mm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mm.renderQueue = 3000;
            mm.color = glassColor;
            return mm;
        }

        // ===== Hashi-watashi (橋渡し) =====
        // คาน 2 อันวางขนานตามแกน X เว้นช่องตรงกลางตามแกน Z
        private const float BarZ = 0.07f;       // ตำแหน่ง z ของคานแต่ละอัน (±)
        private const float BarThick = 0.04f;   // ความหนาคาน
        private const float BarTopY = 0.16f;    // ผิวบนคาน
        private const float GapHalfZ = 0.05f;   // ครึ่งความกว้างช่อง (ขอบในคาน)

        private static void BuildBars(PhysicsMaterial mat)
        {
            var root = new GameObject("Bars").transform;
            float cy = BarTopY - BarThick / 2f;
            var barColor = new Color(0.85f, 0.2f, 0.2f); // คานสีแดงเด่น
            MakeBar(root, "Bar_Front", new Vector3(0f, cy, -BarZ), mat, barColor);
            MakeBar(root, "Bar_Back", new Vector3(0f, cy, BarZ), mat, barColor);
        }

        private static void MakeBar(Transform parent, string name, Vector3 pos, PhysicsMaterial mat, Color c)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = pos;
            bar.transform.localScale = new Vector3(0.5f, BarThick, BarThick);
            Paint(bar, c);
            if (mat != null) bar.GetComponent<BoxCollider>().sharedMaterial = mat;
        }

        // ช่องรับของ = โซนใต้คานตรงช่องว่าง (กล่องตกผ่านคานลงมาโดน = ได้รางวัล)
        private static PrizeCatchZone BuildWinZone()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "WinZone";
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>()); // trigger มองไม่เห็น
            go.transform.position = new Vector3(0f, 0.05f, 0f);
            // คลุมเฉพาะใต้ช่องระหว่างคาน (z ±0.08) ไม่คลุมพื้นด้านนอก
            go.transform.localScale = new Vector3(0.55f, 0.09f, 2f * GapHalfZ + 0.06f);
            go.GetComponent<BoxCollider>().isTrigger = true;

            // marker พื้นสีเข้มใต้ช่อง ให้เห็นว่าเป็นช่องรับของ
            var hole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hole.name = "HoleMarker";
            hole.transform.SetParent(go.transform, false);
            hole.transform.localScale = new Vector3(1f, 0.04f, 1f);
            hole.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            Object.DestroyImmediate(hole.GetComponent<Collider>());
            Paint(hole, new Color(0.04f, 0.04f, 0.05f));

            return go.AddComponent<PrizeCatchZone>();
        }

        private static (ClawController, ClawGripSystem) BuildClaw(PhysicsMaterial mat)
        {
            var machine = new GameObject("ClawMachine").transform;

            var gantry = new GameObject("Gantry").transform;
            gantry.SetParent(machine, false);
            // เริ่มเหนือพื้นที่คาน (hashi-watashi คีบตรงไหนก็ปล่อยตรงนั้น)
            gantry.localPosition = new Vector3(0f, 0.55f, -0.12f);
            var claw = gantry.gameObject.AddComponent<ClawController>();

            var clawHead = new GameObject("ClawHead").transform;
            clawHead.SetParent(gantry, false);
            clawHead.localPosition = Vector3.zero;

            // ลำตัวหัวคีบ = จานครอบ (saucer) แบบ UFO catcher
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "HeadBody";
            body.transform.SetParent(clawHead, false);
            body.transform.localScale = new Vector3(0.09f, 0.025f, 0.09f); // แบนเป็นจาน
            body.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            Paint(body, new Color(0.85f, 0.7f, 0.2f));
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // แกนกลางเชื่อมขา
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hub.name = "Hub";
            hub.transform.SetParent(clawHead, false);
            hub.transform.localScale = new Vector3(0.03f, 0.04f, 0.03f);
            hub.transform.localPosition = new Vector3(0f, -0.025f, 0f);
            Paint(hub, new Color(0.3f, 0.3f, 0.33f));
            Object.DestroyImmediate(hub.GetComponent<Collider>());

            var grip = clawHead.gameObject.AddComponent<ClawGripSystem>();

            // ขา 2 ข้าง — ง่ามมีปลายงอเข้า (hooked prong) pivot ที่แกนกลาง
            // inwardSign: ทิศที่ปลายขางอเข้าหากึ่งกลาง (ซ้าย -1 / ขวา +1)
            var leftArm = BuildArm("LeftArm", clawHead, mat, -1f);
            var rightArm = BuildArm("RightArm", clawHead, mat, 1f);

            // GrabPoint กึ่งกลางปลายขา
            var grabPoint = new GameObject("GrabPoint").transform;
            grabPoint.SetParent(clawHead, false);
            grabPoint.localPosition = new Vector3(0f, -0.16f, 0f);

            // ขาดิ่ง: ยิง ray จาก GrabPoint หา prize เท่านั้น (กัน ray ชนขาตัวเอง)
            var cso = new SerializedObject(claw);
            cso.FindProperty("dropProbe").objectReferenceValue = grabPoint;
            cso.FindProperty("groundCheckDistance").floatValue = 0.04f;
            cso.FindProperty("dropBlockingLayers").intValue = 1 << LayerMask.NameToLayer(PrizeLayerName);
            cso.ApplyModifiedPropertiesWithoutUndo();

            // wire grip ผ่าน SerializedObject (field เป็น private [SerializeField])
            var so = new SerializedObject(grip);
            so.FindProperty("leftArm").objectReferenceValue = leftArm;
            so.FindProperty("rightArm").objectReferenceValue = rightArm;
            so.FindProperty("grabPoint").objectReferenceValue = grabPoint;
            so.FindProperty("prizeLayer").intValue = 1 << LayerMask.NameToLayer(PrizeLayerName);
            so.ApplyModifiedPropertiesWithoutUndo();

            return (claw, grip);
        }

        private static Transform BuildArm(string name, Transform parent, PhysicsMaterial mat, float inwardSign)
        {
            var metal = new Color(0.78f, 0.78f, 0.82f);

            // pivot ที่แกนกลางหัวคีบ — หมุนรอบ Z เพื่อหุบ/กาง
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0f, -0.035f, 0f);

            // ท่อนบน: แท่งยาวยื่นลง
            var upper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            upper.name = name + "_Upper";
            upper.transform.SetParent(pivot, false);
            upper.transform.localScale = new Vector3(0.018f, 0.11f, 0.022f);
            upper.transform.localPosition = new Vector3(0f, -0.055f, 0f);
            Paint(upper, metal);
            var ucol = upper.GetComponent<BoxCollider>();
            if (mat != null) ucol.sharedMaterial = mat;

            // ปลายงอเข้า (hook tip): เอียงเข้าหากึ่งกลาง
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = name + "_Tip";
            tip.transform.SetParent(pivot, false);
            tip.transform.localScale = new Vector3(0.018f, 0.055f, 0.022f);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, inwardSign * 38f);
            tip.transform.localPosition = new Vector3(inwardSign * 0.013f, -0.125f, 0f);
            Paint(tip, metal);
            var tcol = tip.GetComponent<BoxCollider>();
            if (mat != null) tcol.sharedMaterial = mat;

            return pivot;
        }

        private static PayoutManager BuildSystems()
        {
            var systems = new GameObject("Systems");
            return systems.AddComponent<PayoutManager>();
        }

        private static (Camera front, Camera side) BuildCameras()
        {
            var front = MakeCamera("FrontCamera", new Vector3(0f, 0.55f, -0.95f), true);
            front.gameObject.AddComponent<AudioListener>();

            var side = MakeCamera("SideCamera", new Vector3(0.95f, 0.55f, 0f), false);
            return (front, side);
        }

        private static Camera MakeCamera(string name, Vector3 pos, bool enabled)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.LookAt(new Vector3(0f, 0.1f, 0f));
            var cam = go.AddComponent<Camera>();
            cam.enabled = enabled;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 45f;
            return cam;
        }

        // กล่อง figure วางพาดขวางคาน 2 อัน (ปลายเกยคาน กลางลอยเหนือช่อง)
        // ด้านยาว Z = 0.14 (พาดคาน) / ด้านแคบ X = 0.07 < ช่อง 0.10 → พอหมุนขนานคานก็ร่วง (tatehame)
        private static void SpawnBoxesOnBars(PhysicsMaterial mat, int layer)
        {
            var root = new GameObject("Figures").transform;
            var boxSize = new Vector3(0.07f, 0.06f, 0.14f);
            float restY = BarTopY + boxSize.y / 2f + 0.001f;

            // (x, มุมหมุนรอบ Y) — ไล่ความยากจากตั้งฉาก(ยาก) ไปเกือบขนาน(ง่าย ใกล้ร่วง)
            var setups = new[]
            {
                new Vector2(-0.14f, 0f),   // ตั้งฉากคาน — ยาก
                new Vector2(0.00f, 38f),   // เฉียง — ปานกลาง
                new Vector2(0.15f, 66f),   // เกือบขนาน — ง่าย ดันนิดเดียวร่วง
            };

            for (int i = 0; i < setups.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Figure_" + (i + 1);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(setups[i].x, restY, 0f);
                go.transform.rotation = Quaternion.Euler(0f, setups[i].y, 0f);
                go.transform.localScale = boxSize;
                go.layer = layer;
                Paint(go, Color.HSVToRGB((0.08f + i * 0.2f) % 1f, 0.75f, 0.95f));

                var col = go.GetComponent<BoxCollider>();
                if (mat != null) col.sharedMaterial = mat;

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var prize = go.AddComponent<Prize>();
                prize.prizeId = "figure_" + (i + 1);
                prize.displayName = "Figure " + (i + 1);
            }
        }

        // ---------- Wiring ----------

        private static void WireClawController(ClawController claw, ClawGripSystem grip, PayoutManager payout)
        {
            var so = new SerializedObject(claw);
            so.FindProperty("gantry").objectReferenceValue = claw.transform;
            so.FindProperty("clawHead").objectReferenceValue = grip.transform; // ClawHead = ที่ติด grip
            so.FindProperty("gripSystem").objectReferenceValue = grip;
            so.FindProperty("payoutManager").objectReferenceValue = payout;
            so.FindProperty("returnToChute").boolValue = false; // hashi-watashi: ปล่อยตรงจุดที่คีบ
            so.FindProperty("yTop").floatValue = 0f;
            so.FindProperty("yBottom").floatValue = -0.40f;
            so.FindProperty("xLimits").vector2Value = new Vector2(-0.22f, 0.22f);
            so.FindProperty("zLimits").vector2Value = new Vector2(-0.22f, 0.22f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireSystems(ClawController claw, ClawGripSystem grip, PayoutManager payout,
            PrizeCatchZone chute, Camera front, Camera side)
        {
            var systems = payout.gameObject;

            var switcher = systems.AddComponent<CameraSwitcher>();
            var sso = new SerializedObject(switcher);
            sso.FindProperty("frontCamera").objectReferenceValue = front;
            sso.FindProperty("sideCamera").objectReferenceValue = side;
            sso.ApplyModifiedPropertiesWithoutUndo();

            var hud = systems.AddComponent<DebugHUD>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("claw").objectReferenceValue = claw;
            hso.FindProperty("grip").objectReferenceValue = grip;
            hso.FindProperty("payout").objectReferenceValue = payout;
            hso.FindProperty("chute").objectReferenceValue = chute;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Primitive helpers ----------

        private static GameObject MakeBox(string name, Vector3 pos, Vector3 scale,
            Transform parent, PhysicsMaterial mat, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Paint(go, color);
            if (mat != null)
            {
                var col = go.GetComponent<BoxCollider>();
                if (col != null) col.sharedMaterial = mat;
            }
            return go;
        }

        private static void Paint(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { color = color };
            mr.sharedMaterial = m;
        }
    }
}
#endif
