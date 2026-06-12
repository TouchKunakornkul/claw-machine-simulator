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

        // สนามเล่นสเกลจริง (manual: ตู้กว้าง 1683mm = 2 สถานี -> ต่อคน ~84cm, ลึก 875mm)
        private const float FieldHalfX = 0.40f; // กว้าง 80cm
        private const float FieldHalfZ = 0.30f; // ลึก 60cm
        private const float GlassH = 0.60f;     // กระจกสูง 60cm

        private static void BuildCabinet(PhysicsMaterial mat)
        {
            var root = new GameObject("Cabinet").transform;

            // พื้นตู้ = ก้นหลุมรับของ (ผิวบนที่ y=0, สีเข้มให้รู้ว่าเป็นหลุม)
            MakeBox("Floor", new Vector3(0f, -0.025f, 0f),
                new Vector3(FieldHalfX * 2f, 0.05f, FieldHalfZ * 2f), root, mat, new Color(0.06f, 0.06f, 0.08f));

            // ผนัง 4 ด้าน = กระจกใส (โปร่งแสง มองเห็นของข้างใน)
            var glass = CreateGlassMaterial();
            const float t = 0.012f;
            var walls = new[]
            {
                MakeBox("Glass_Back", new Vector3(0f, GlassH / 2f, FieldHalfZ), new Vector3(FieldHalfX * 2f, GlassH, t), root, mat, Color.white),
                MakeBox("Glass_Front", new Vector3(0f, GlassH / 2f, -FieldHalfZ), new Vector3(FieldHalfX * 2f, GlassH, t), root, mat, Color.white),
                MakeBox("Glass_Left", new Vector3(-FieldHalfX, GlassH / 2f, 0f), new Vector3(t, GlassH, FieldHalfZ * 2f), root, mat, Color.white),
                MakeBox("Glass_Right", new Vector3(FieldHalfX, GlassH / 2f, 0f), new Vector3(t, GlassH, FieldHalfZ * 2f), root, mat, Color.white),
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

        // ===== Hashi-watashi (橋渡し) — สเกลจริงหน่วยเมตร =====
        // คานท่อกลม 2 อัน (Ø3cm หุ้มยาง) พาดชนผนังตามแกน X เหมือน "สะพานข้ามหลุม"
        // ใต้คานทั้งหมดคือหลุมรับของ — กล่องร่วงจากคานตรงไหนก็ได้ = ได้รางวัล
        // อ้างอิง: กล่อง figure จริง ~20cm เกยคานข้างละ ~1cm
        private const float BarZ = 0.075f;      // ตำแหน่ง z กึ่งกลางคานแต่ละอัน (±)
        private const float BarDia = 0.025f;    // เส้นผ่านศูนย์กลางคาน 2.5cm
        private const float BarTopY = 0.16f;    // ผิวบนคาน
        private const float GapHalfZ = BarZ - BarDia / 2f; // ขอบในคาน (ช่องใน 12cm)

        private static void BuildBars(PhysicsMaterial mat)
        {
            var root = new GameObject("Bars").transform;
            float cy = BarTopY - BarDia / 2f;
            var barColor = new Color(0.85f, 0.2f, 0.2f); // คานสีแดงเด่น

            // คานจริงคลุมสายยาง/ท่อยาง = หนึบพอประมาณ — frictionCombine Maximum
            // เพื่อชนะ Minimum ของกล่อง (กล่องลื่นบน shovel แต่หนึบบนคาน)
            var rubber = new PhysicsMaterial("BarRubber")
            {
                dynamicFriction = 0.65f,
                staticFriction = 0.75f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(rubber, "Assets/Physics/BarRubber.physicMaterial");

            MakeBar(root, "Bar_Front", new Vector3(0f, cy, -BarZ), rubber, barColor);
            MakeBar(root, "Bar_Back", new Vector3(0f, cy, BarZ), rubber, barColor);
        }

        // คานกลม: cylinder นอนตามแกน X ยาวชนผนังสองข้าง
        private static void MakeBar(Transform parent, string name, Vector3 pos,
            PhysicsMaterial rubber, Color c)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = pos;
            bar.transform.localScale = new Vector3(BarDia, FieldHalfX, BarDia); // cylinder สูง 2 หน่วย -> ยาวชนผนัง
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);  // นอนตามแกน X
            Paint(bar, c);
            bar.GetComponent<Collider>().sharedMaterial = rubber;
        }

        // จุดได้ของ = ใต้ตู้ทั้งหมด (คานเป็นสะพานข้ามหลุม — ร่วงตรงไหนก็ได้รางวัล)
        private static PrizeCatchZone BuildWinZone()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "WinZone";
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>()); // trigger มองไม่เห็น
            go.transform.position = new Vector3(0f, 0.05f, 0f);
            go.transform.localScale = new Vector3(
                FieldHalfX * 2f - 0.02f, 0.09f, FieldHalfZ * 2f - 0.02f); // คลุมพื้นตู้ทั้งหมด
            go.GetComponent<BoxCollider>().isTrigger = true;
            return go.AddComponent<PrizeCatchZone>();
        }

        private static (ClawController, ClawGripSystem) BuildClaw(PhysicsMaterial mat)
        {
            var machine = new GameObject("ClawMachine").transform;

            var gantry = new GameObject("Gantry").transform;
            gantry.SetParent(machine, false);
            // เริ่มเหนือพื้นที่คาน (hashi-watashi คีบตรงไหนก็ปล่อยตรงนั้น)
            // ความสูงรางจริง: หัว UFO วิ่งใกล้เพดานกระจก
            gantry.localPosition = new Vector3(0f, 0.72f, -0.12f);
            var claw = gantry.gameObject.AddComponent<ClawController>();

            var clawHead = new GameObject("ClawHead").transform;
            clawHead.SetParent(gantry, false);
            clawHead.localPosition = Vector3.zero;

            // ลำตัวหัวคีบ = จานครอบ (saucer) แบบ UFO catcher — เครื่องจริงหัวใหญ่ Ø ~20cm
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "HeadBody";
            body.transform.SetParent(clawHead, false);
            body.transform.localScale = new Vector3(0.20f, 0.035f, 0.20f); // แบนเป็นจาน
            body.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            Paint(body, new Color(0.85f, 0.7f, 0.2f));
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // แกนกลางเชื่อมขา (mechanism กล่องใต้จาน)
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hub.name = "Hub";
            hub.transform.SetParent(clawHead, false);
            hub.transform.localScale = new Vector3(0.07f, 0.05f, 0.05f);
            hub.transform.localPosition = new Vector3(0f, -0.025f, 0f);
            Paint(hub, new Color(0.3f, 0.3f, 0.33f));
            Object.DestroyImmediate(hub.GetComponent<Collider>());

            var grip = clawHead.gameObject.AddComponent<ClawGripSystem>();
            var headRb = clawHead.GetComponent<Rigidbody>(); // มาจาก RequireComponent ของ grip
            headRb.isKinematic = true;
            headRb.useGravity = false;

            // การตั้งค่าตู้แบบ asset (แผงปรับ SEGA: POWER/สปริง/ขนาดขา/shovel)
            var settings = CreateMachineSettings();

            // ขา 2 ข้าง — Rigidbody + HingeJoint ห้อยจากหัว แกว่งเบนหลบของได้จริง
            // inwardSign: ทิศที่ปลายขางอเข้าหากึ่งกลาง (ซ้าย -1 / ขวา +1)
            var leftArm = BuildArm("LeftArm", clawHead, mat, -1f, headRb, settings);
            var rightArm = BuildArm("RightArm", clawHead, mat, 1f, headRb, settings);

            // GrabPoint กึ่งกลางปลายขา (ขา L ปลายอยู่ลึก ~0.20 ใต้หัว)
            var grabPoint = new GameObject("GrabPoint").transform;
            grabPoint.SetParent(clawHead, false);
            grabPoint.localPosition = new Vector3(0f, -0.19f, 0f);

            // จุดวัด "ของอยู่ใต้หัว" = ก้น hub (ไม่ใช่ปลายขา!) — ขาจะดิ่งลึกจน shovel
            // อยู่ใต้ก้นกล่องก่อนหัวจะแตะของแล้วค่อยหุบ (ช้อนได้จริง)
            var dropProbe = new GameObject("DropProbe").transform;
            dropProbe.SetParent(clawHead, false);
            dropProbe.localPosition = new Vector3(0f, -0.055f, 0f);

            var cso = new SerializedObject(claw);
            cso.FindProperty("dropProbe").objectReferenceValue = dropProbe;
            cso.FindProperty("groundCheckDistance").floatValue = 0.015f;
            cso.FindProperty("dropBlockingLayers").intValue = 1 << LayerMask.NameToLayer(PrizeLayerName);
            cso.ApplyModifiedPropertiesWithoutUndo();

            // wire grip ผ่าน SerializedObject (field เป็น private [SerializeField])
            var so = new SerializedObject(grip);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("leftArm").objectReferenceValue = leftArm;
            so.FindProperty("rightArm").objectReferenceValue = rightArm;
            so.FindProperty("grabPoint").objectReferenceValue = grabPoint;
            so.FindProperty("holdCheckRadius").floatValue = 0.05f;   // ตรวจว่ามีของในง่าม (HUD)
            so.FindProperty("resistanceAngle").floatValue = 10f;     // เผื่อขาเฉียด/ครูดตอนคร่อมกล่อง
            so.FindProperty("prizeLayer").intValue = 1 << LayerMask.NameToLayer(PrizeLayerName);
            so.ApplyModifiedPropertiesWithoutUndo();

            return (claw, grip);
        }

        private const string SettingsPath = "Assets/Physics/DefaultMachineSettings.asset";

        private static MachineSettings CreateMachineSettings()
        {
            var s = ScriptableObject.CreateInstance<MachineSettings>();
            s.power = 55;
            s.springStage = MachineSettings.SpringStage.Middle;
            s.armSize = MachineSettings.ArmSize.L; // กล่อง figure 20cm ร้านจริงใช้ขา L
            s.shovel = MachineSettings.ShovelType.W40;
            s.openArmAngle = 70f; // ขาตัว L ท่อนนอนยาว ต้องกางมากถึงคร่อมกล่อง 10cm
            s.shovelGapCm = 0.5f; // 13-4: ปลายเกือบแตะ ไม่ overlap
            s.clawYaw = 0f;       // กางขนานคาน (ปรับทแยงได้ในแผง Tab)
            s.armOffsetCm = 1.6f; // ขาขนานแต่เยื้องกัน — shovel สวนผ่านกันได้ตอนหุบ
            s.segaMode = true; // SEGA แท้: แรงคงที่ สู้ด้วยการจัดวาง (hashi-watashi)
            Directory.CreateDirectory("Assets/Physics");
            AssetDatabase.CreateAsset(s, SettingsPath);
            return s;
        }

        // ขาตัว L ตาม manual จริง (ARM S/M/L): ท่อนดิ่งจาก pivot + หักศอกเป็นท่อนนอนยื่นเข้าใน
        // + shovel แผ่นพับยึดปลายท่อนนอน — ขนาด/ระยะทั้งหมดมาจาก ArmGeometry (ชุดเดียวกับ grip)
        // ขาสองข้างขนานกันแต่ "เยื้องกัน" ตามแกน Z เหมือนเครื่องจริง (shovel สวนผ่านกันได้)
        // ขาเป็น Rigidbody ห้อยจากหัวด้วย HingeJoint -> เบนหลบของได้ (สมจริงตาม manual: ขาสปริง)
        private static Transform BuildArm(string name, Transform parent, PhysicsMaterial mat,
            float inwardSign, Rigidbody headAnchor, MachineSettings settings)
        {
            var metal = new Color(0.82f, 0.82f, 0.87f);

            // pivot แยกออกจากแกนกลางเล็กน้อย + เยื้องข้างตามแกน Z (ซ้าย +z / ขวา -z)
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(
                -inwardSign * ArmGeometry.PivotOut,
                -0.03f,
                -inwardSign * settings.armOffsetCm * 0.005f);

            // physics: ขาห้อยจากหัวคีบ แกว่ง/เบนได้รอบแกน Z
            var rb = pivot.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var hinge = pivot.gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = headAnchor;
            hinge.anchor = Vector3.zero;            // หมุนรอบจุดบนสุดของขา
            hinge.axis = Vector3.forward;           // แกน Z
            hinge.autoConfigureConnectedAnchor = false;
            hinge.connectedAnchor = pivot.localPosition; // local ของ clawHead (= connected body)
            hinge.useSpring = true;
            var spring = hinge.spring;
            spring.targetPosition = -inwardSign * settings.openArmAngle; // ซ้าย=+ / ขวา=- (มิเรอร์)
            spring.spring = 0.5f;   // หน่วย N·m/องศา — ขาจริงอ่อน แกว่งงอให้เห็น
            spring.damper = 0.05f;
            hinge.spring = spring;
            var limits = hinge.limits;
            limits.min = -95f;
            limits.max = 95f;
            hinge.limits = limits;
            hinge.useLimits = true;

            // ท่อนดิ่ง (shoulder): จาก pivot ตรงลงข้อศอก
            var shoulder = MakeCapsule(name + "_Shoulder", pivot, mat, metal);
            shoulder.localScale = new Vector3(0.016f, ArmGeometry.ShoulderLen / 2f, 0.012f);
            shoulder.localPosition = new Vector3(0f, -ArmGeometry.ShoulderLen / 2f, 0f);

            // ท่อนนอน (foot): หักศอก ~90° ยื่นเข้าหากึ่งกลาง
            var foot = MakeCapsule(name + "_Foot", pivot, mat, metal);
            foot.localScale = new Vector3(0.014f, ArmGeometry.FootLen / 2f, 0.012f);
            foot.localPosition = new Vector3(
                inwardSign * ArmGeometry.FootLen / 2f, -ArmGeometry.ShoulderLen, 0f);
            foot.localRotation = Quaternion.Euler(0f, 0f, inwardSign * 90f);

            // shovel = แผ่นพับยึดปลายท่อนนอน (13-4: W30/W40/W60 ตามชนิดของรางวัล)
            // แผ่นแบนช้อนใต้ของ — หนีบไม่ได้ ได้แต่รอง/ตักตามฟิสิกส์จริง
            // มุมแผ่นจะถูก ClawGripSystem.ApplyShovel ตั้งให้ราบพอดีตอนหุบ
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = name + "_Shovel_" + settings.shovel;
            tip.transform.SetParent(pivot, false);
            tip.transform.localScale = new Vector3(
                ArmGeometry.PlateLen, 0.004f, settings.ShovelWidthMeters);
            tip.transform.localPosition = new Vector3(
                inwardSign * (ArmGeometry.FootLen + ArmGeometry.PlateCenterBeyondFoot),
                -ArmGeometry.TipDown, 0f);
            Paint(tip, metal);
            var tcol = tip.GetComponent<BoxCollider>();
            if (mat != null) tcol.sharedMaterial = mat;

            return pivot;
        }

        private static Transform MakeCapsule(string name, Transform parent, PhysicsMaterial mat, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            Paint(go, c);
            var col = go.GetComponent<CapsuleCollider>();
            if (mat != null && col != null) col.sharedMaterial = mat;
            return go.transform;
        }

        private static PayoutManager BuildSystems()
        {
            var systems = new GameObject("Systems");
            return systems.AddComponent<PayoutManager>();
        }

        private static (Camera front, Camera side) BuildCameras()
        {
            var front = MakeCamera("FrontCamera", new Vector3(0f, 0.70f, -1.20f), true);
            front.gameObject.AddComponent<AudioListener>();

            var side = MakeCamera("SideCamera", new Vector3(1.25f, 0.70f, 0f), false);
            return (front, side);
        }

        private static Camera MakeCamera(string name, Vector3 pos, bool enabled)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.LookAt(new Vector3(0f, 0.22f, 0f));
            var cam = go.AddComponent<Camera>();
            cam.enabled = enabled;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 45f;
            return cam;
        }

        // กล่อง figure วางพาดขวางคาน 2 อัน (ปลายเกยคานข้างละ ~1cm กลางลอยเหนือช่อง)
        // หมุนขนานคานเมื่อไหร่ (กว้าง 10 < ช่องใน 12) ก็ร่วง = tatehame
        private static void SpawnBoxesOnBars(PhysicsMaterial mat, int layer)
        {
            var root = new GameObject("Figures").transform;
            // กล่อง figure สเกลจริง (Banpresto ฯลฯ ~20cm): ยาว Z=20 (พาดคาน) / กว้าง X=10 / สูง 9
            var boxSize = new Vector3(0.10f, 0.09f, 0.20f);
            float restY = BarTopY + boxSize.y / 2f + 0.001f;

            // โหมดทดลอง: กล่องเดียว วางท่ามาตรฐานร้านจริง — พาดขวางคานตรงๆ กลางตู้
            // (คีบได้แล้ว PrizeCatchZone จะ respawn กลับท่านี้ให้เล่นต่อ)
            var setups = new[]
            {
                new Vector2(0f, 0f),
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
                rb.mass = 0.3f; // วัดจริง: กล่อง figure รวมกล่อง ~265-500g ส่วนใหญ่ ~300g
                // จุดศูนย์ถ่วงจริงเกือบกึ่งกลาง ต่ำกว่านิดเดียว (รีวิวผู้เล่น: "ก้นหนักกว่า
                // เล็กน้อย ไม่ถึงระดับมีผลกับ hashiwatashi") — local ของ unit cube
                rb.centerOfMass = new Vector3(0f, -0.05f, 0f);
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
            so.FindProperty("machineSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<MachineSettings>(SettingsPath); // 13-2 sensor bracket
            so.FindProperty("returnToChute").boolValue = false; // hashi-watashi: ปล่อยตรงจุดที่คีบ
            so.FindProperty("yTop").floatValue = 0f;
            so.FindProperty("yBottom").floatValue = -0.58f; // gantry 0.72 - ปลายขา ~0.18 -> เกือบถึงพื้น
            // ค่าตั้งต้นเท่านั้น — ApplyFromSettings จะคำนวณจริงจาก 13-2 ตอน Start
            so.FindProperty("xLimits").vector2Value = new Vector2(-0.30f, 0.30f);
            so.FindProperty("zLimits").vector2Value = new Vector2(-0.19f, 0.19f);
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

            // ให้ PayoutManager อ่าน payoutEveryN จาก settings asset เดียวกัน
            var settingsAsset = AssetDatabase.LoadAssetAtPath<MachineSettings>(SettingsPath);
            var payso = new SerializedObject(payout);
            payso.FindProperty("settings").objectReferenceValue = settingsAsset;
            payso.ApplyModifiedPropertiesWithoutUndo();

            // แผงปรับตู้ (Tab) — จัดวางตาม manual จริง
            var panel = systems.AddComponent<TuningPanel>();
            var pso = new SerializedObject(panel);
            pso.FindProperty("grip").objectReferenceValue = grip;
            pso.FindProperty("claw").objectReferenceValue = claw;
            pso.FindProperty("payout").objectReferenceValue = payout;
            pso.ApplyModifiedPropertiesWithoutUndo();
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
