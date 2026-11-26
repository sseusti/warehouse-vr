using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Warehouse.Editor
{
    /// <summary>
    /// Retro 1960s lab dressing for the warehouse room, built from primitives.
    /// Decor has no colliders, so it never blocks the remote ray or the teleport arc.
    /// </summary>
    static class LabDecor
    {
        const string Folder = "Assets/Day2";

        static Material tileDark, wallPanel, trim, ceiling, lamp, wood, darkWood, iron, brass, glass,
            chalkboard, door, ceilingSlab, signBoard, screen, signBack, bookRed, bookMustard, bookBlue, mugRed, pencil;

        /// <summary>Colors of the room materials created by <see cref="DayTwoScene"/>.</summary>
        public static readonly Color FloorColor = new(0.80f, 0.77f, 0.69f);
        public static readonly Color WallColor = new(0.93f, 0.89f, 0.78f);
        public static readonly Color PlatformColor = new(0.45f, 0.49f, 0.33f);

        /// <summary>Centre and tilt of the scale console face that carries the mass readout.</summary>
        public static readonly Vector3 ConsoleFace = new(-3.85f, 0.43f, -0.97f);
        public const float ConsoleTilt = 38f;

        public static void Build(WarehouseRoute route)
        {
            CreateMaterials();
            Lighting();
            Floor();
            Walls();
            Ceiling();
            Desk();
            Bookcase();
            Chalkboard();
            Clock();
            ShelfAndBalcony();
            ShelfProps();
            Vent();
            ExitDoor();
            SignBoards();
            DockHanger();
            Weight(route.weight.transform);
            Crate(route.box.transform);
        }

        /// <summary>Marks everything that never moves for static batching.</summary>
        public static void MarkStatic(Scene scene, params GameObject[] dynamic)
        {
            var skip = new HashSet<GameObject>(dynamic);
            foreach (var root in scene.GetRootGameObjects())
                MarkStatic(root.transform, skip);
        }

        static void MarkStatic(Transform node, HashSet<GameObject> skip)
        {
            GameObject go = node.gameObject;
            if (skip.Contains(go) || go.GetComponent<TMP_Text>() != null || go.GetComponent<ObjectiveMarker>() != null || go.GetComponent<Canvas>() != null ||
                go.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null || go.GetComponent<Rigidbody>() != null)
                return;

            if (go.GetComponent<MeshRenderer>() != null)
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            foreach (Transform child in node)
                MarkStatic(child, skip);
        }

        /// <summary>Loads or creates a Day 2 material and sets its color, so a rebuild picks up palette changes.</summary>
        public static Material Paint(string name, Color color, string shader = "Standard", float smoothness = 0.15f)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateMaterials()
        {
            tileDark = Paint("Floor Tile Dark", new Color(0.30f, 0.33f, 0.36f));
            wallPanel = Paint("Wall Panel", new Color(0.33f, 0.58f, 0.58f));
            trim = Paint("Trim", new Color(0.24f, 0.17f, 0.12f));
            ceiling = Paint("Ceiling", new Color(0.90f, 0.89f, 0.84f));
            // The single light comes from above, so the ceiling is unlit to stay light.
            ceilingSlab = Paint("Ceiling Slab", new Color(0.74f, 0.73f, 0.69f), "Unlit/Color");
            lamp = Paint("Ceiling Lamp", new Color(1f, 0.93f, 0.76f), "Unlit/Color");
            wood = Paint("Wood", new Color(0.62f, 0.42f, 0.24f));
            darkWood = Paint("Dark Wood", new Color(0.33f, 0.21f, 0.12f));
            iron = Paint("Iron", new Color(0.17f, 0.18f, 0.19f), smoothness: 0.35f);
            brass = Paint("Brass", new Color(0.80f, 0.64f, 0.30f), smoothness: 0.55f);
            glass = Paint("Glass", new Color(0.70f, 0.86f, 0.90f), smoothness: 0.8f);
            chalkboard = Paint("Chalkboard", new Color(0.13f, 0.26f, 0.20f));
            door = Paint("Door", new Color(0.24f, 0.45f, 0.33f));
            screen = Paint("Scope Screen", new Color(0.35f, 1f, 0.55f), "Unlit/Color");
            signBack = Paint("Exit Sign Back", new Color(0.05f, 0.30f, 0.15f), "Unlit/Color");
            signBoard = Paint("Sign Board", new Color(0.09f, 0.11f, 0.14f), "Unlit/Color");
            bookRed = Paint("Book Red", new Color(0.66f, 0.18f, 0.15f));
            bookMustard = Paint("Book Mustard", new Color(0.82f, 0.64f, 0.22f));
            bookBlue = Paint("Book Blue", new Color(0.18f, 0.30f, 0.48f));
            mugRed = bookRed;
            pencil = bookMustard;
        }

        static void Lighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.83f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.68f, 0.64f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.38f);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                    light.color = new Color(1f, 0.95f, 0.86f);
            }
        }

        // Large checkerboard tiles: the light tiles are the floor itself.
        static void Floor()
        {
            var parent = Group("Floor Tiles");
            for (int x = -5; x < 5; x++)
            {
                for (int z = -3; z < 3; z++)
                {
                    if (((x + z) & 1) == 0)
                        Block("Tile", new Vector3(x + 0.5f, 0.001f, z + 0.5f), new Vector3(1f, 0.002f, 1f), tileDark, parent);
                }
            }
        }

        // Two-tone walls: teal panel below, cream above, dark skirting and a trim line.
        static void Walls()
        {
            var parent = Group("Wall Panels");
            const float inner = 4.925f;
            const float depth = 2.925f;
            WallBand(parent, 0.6f, 1.2f, 0.02f, wallPanel, inner, depth);
            WallBand(parent, 1.21f, 0.06f, 0.04f, trim, inner, depth);
            WallBand(parent, 0.06f, 0.12f, 0.04f, trim, inner, depth);
        }

        static void WallBand(Transform parent, float y, float height, float thickness, Material material, float inner, float depth)
        {
            float offset = thickness * 0.5f + 0.005f;
            Block("Band", new Vector3(0f, y, depth - offset), new Vector3(inner * 2f, height, thickness), material, parent);
            Block("Band", new Vector3(0f, y, -depth + offset), new Vector3(inner * 2f, height, thickness), material, parent);
            Block("Band", new Vector3(-inner + offset, y, 0f), new Vector3(thickness, height, depth * 2f), material, parent);
            Block("Band", new Vector3(inner - offset, y, 0f), new Vector3(thickness, height, depth * 2f), material, parent);
        }

        static void Ceiling()
        {
            var parent = Group("Ceiling");
            Block("Ceiling Slab", new Vector3(0f, 4.35f, 0f), new Vector3(10f, 0.1f, 6f), ceilingSlab, parent);
            foreach (float x in new[] { -3f, 0f, 3f })
            {
                foreach (float z in new[] { -1.2f, 1.2f })
                {
                    Block("Lamp Frame", new Vector3(x, 4.29f, z), new Vector3(1.7f, 0.03f, 0.6f), iron, parent);
                    Block("Lamp", new Vector3(x, 4.27f, z), new Vector3(1.6f, 0.02f, 0.5f), lamp, parent);
                }
            }
        }

        // Scientist's desk along the front wall: mugs, flasks and a green oscilloscope.
        static void Desk()
        {
            var parent = Group("Desk");
            const float top = 0.785f;
            Block("Desk Top", new Vector3(-1f, 0.76f, -2.5f), new Vector3(2.2f, 0.05f, 0.7f), wood, parent);
            foreach (float x in new[] { -2.05f, 0.05f })
            {
                foreach (float z in new[] { -2.8f, -2.2f })
                    Block("Desk Leg", new Vector3(x, 0.37f, z), new Vector3(0.05f, 0.74f, 0.05f), iron, parent);
            }
            Block("Drawers", new Vector3(-1.7f, 0.42f, -2.5f), new Vector3(0.55f, 0.62f, 0.62f), wood, parent);
            Block("Drawer Line", new Vector3(-1.7f, 0.52f, -2.185f), new Vector3(0.5f, 0.01f, 0.01f), darkWood, parent);
            Block("Drawer Line", new Vector3(-1.7f, 0.32f, -2.185f), new Vector3(0.5f, 0.01f, 0.01f), darkWood, parent);

            Mug(parent, new Vector3(-1.75f, top, -2.35f), mugRed);
            Mug(parent, new Vector3(-1.55f, top, -2.6f), ceiling);

            for (int i = 0; i < 3; i++)
            {
                float x = -1.05f + i * 0.17f;
                float size = 0.12f + i * 0.02f;
                Shape("Flask", PrimitiveType.Sphere, new Vector3(x, top + size * 0.5f, -2.55f), Vector3.one * size, glass, parent);
                Shape("Flask Neck", PrimitiveType.Cylinder, new Vector3(x, top + size + 0.03f, -2.55f),
                    new Vector3(0.035f, 0.04f, 0.035f), glass, parent);
            }

            Block("Oscilloscope", new Vector3(-0.15f, top + 0.15f, -2.6f), new Vector3(0.42f, 0.3f, 0.36f), iron, parent);
            Block("Scope Screen", new Vector3(-0.2f, top + 0.17f, -2.419f), new Vector3(0.22f, 0.16f, 0.005f), screen, parent);
            for (int i = 0; i < 2; i++)
            {
                Shape("Scope Knob", PrimitiveType.Cylinder, new Vector3(-0.0f, top + 0.21f - i * 0.09f, -2.415f),
                    new Vector3(0.04f, 0.01f, 0.04f), ceiling, parent, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        static void Mug(Transform parent, Vector3 floor, Material material)
        {
            Shape("Mug", PrimitiveType.Cylinder, floor + Vector3.up * 0.05f, new Vector3(0.08f, 0.05f, 0.08f), material, parent);
            Block("Mug Handle", floor + new Vector3(0.05f, 0.05f, 0f), new Vector3(0.02f, 0.06f, 0.015f), material, parent);
        }

        // Bookcase on the west wall next to the start: books are the first scale reference.
        static void Bookcase()
        {
            var parent = Group("Bookcase");
            const float back = -4.9f;
            const float front = -4.56f;
            float centerX = (back + front) * 0.5f;
            float depth = front - back;
            const float zMin = -1.05f;
            const float zMax = 0.35f;
            float centerZ = (zMin + zMax) * 0.5f;
            float width = zMax - zMin;

            Block("Back", new Vector3(back, 1f, centerZ), new Vector3(0.02f, 2f, width), darkWood, parent);
            Block("Side", new Vector3(centerX, 1f, zMin), new Vector3(depth, 2f, 0.03f), wood, parent);
            Block("Side", new Vector3(centerX, 1f, zMax), new Vector3(depth, 2f, 0.03f), wood, parent);
            float[] boards = { 0.02f, 0.52f, 1.02f, 1.52f, 1.99f };
            foreach (float y in boards)
                Block("Board", new Vector3(centerX, y, centerZ), new Vector3(depth, 0.03f, width), wood, parent);

            Material[] covers = { bookRed, bookMustard, bookBlue, darkWood };
            var random = new System.Random(7);
            for (int shelf = 0; shelf < boards.Length - 1; shelf++)
            {
                float z = zMin + 0.03f;
                float floor = boards[shelf] + 0.015f;
                while (true)
                {
                    float thickness = 0.035f + (float)random.NextDouble() * 0.035f;
                    if (z + thickness > zMax - 0.03f)
                        break;
                    float height = 0.22f + (float)random.NextDouble() * 0.12f;
                    var book = Block("Book", new Vector3(centerX + 0.02f, floor + height * 0.5f, z + thickness * 0.5f),
                        new Vector3(0.24f, height, thickness), covers[random.Next(covers.Length)], parent);
                    // A few books lean on their neighbours.
                    if (random.NextDouble() < 0.12)
                        book.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                    z += thickness + 0.004f;
                }
            }
        }

        // Board behind the shelf with the physics of the room.
        static void Chalkboard()
        {
            var parent = Group("Chalkboard");
            var center = new Vector3(-1.6f, 2.65f, 2.9f);
            Block("Board Frame", center + Vector3.forward * 0.01f, new Vector3(2.1f, 1.2f, 0.02f), wood, parent);
            Block("Board", center, new Vector3(2f, 1.1f, 0.02f), chalkboard, parent);
            var chalk = LabText.Create("Board Text", "m = m₀ · k³\nF = m · g\ng Луны = 1,62 м/с²",
                center + Vector3.back * 0.015f, 1.6f, new Color(0.93f, 0.95f, 0.9f), parent, TextAlignmentOptions.Left);
            chalk.rectTransform.sizeDelta = new Vector2(1.75f, 0.95f);
        }

        static void Clock()
        {
            var parent = Group("Clock");
            var center = new Vector3(0.8f, 3.15f, 2.9f);
            var faceForward = Quaternion.Euler(90f, 0f, 0f);
            Shape("Clock Rim", PrimitiveType.Cylinder, center, new Vector3(0.5f, 0.02f, 0.5f), iron, parent, faceForward);
            Shape("Clock Face", PrimitiveType.Cylinder, center + Vector3.back * 0.012f,
                new Vector3(0.44f, 0.01f, 0.44f), ceiling, parent, faceForward);
            var hour = Block("Hour Hand", Vector3.zero, new Vector3(0.025f, 0.12f, 0.005f), iron, parent);
            hour.transform.SetPositionAndRotation(center + new Vector3(0.04f, 0.035f, -0.03f), Quaternion.Euler(0f, 0f, -50f));
            var minute = Block("Minute Hand", Vector3.zero, new Vector3(0.018f, 0.18f, 0.005f), iron, parent);
            minute.transform.SetPositionAndRotation(center + new Vector3(-0.03f, 0.08f, -0.034f), Quaternion.Euler(0f, 0f, 20f));
        }

        static void ShelfAndBalcony()
        {
            var parent = Group("Supports");
            const float underside = 1.41f;
            foreach (float x in new[] { -2.65f, -0.55f })
            {
                foreach (float z in new[] { 0.55f, 2.45f })
                    Block("Shelf Leg", new Vector3(x, underside * 0.5f, z), new Vector3(0.07f, underside, 0.07f), iron, parent);
            }

            foreach (float z in new[] { -2.3f, -0.8f, 0.8f, 2.3f })
                Block("Balcony Leg", new Vector3(3.17f, underside * 0.5f, z), new Vector3(0.08f, underside, 0.08f), iron, parent);

            // Railing only on the closed south end: the open side stays clear for the teleport arc.
            const float deck = 1.65f;
            foreach (float x in new[] { 3.18f, 4.02f, 4.86f })
                Block("Rail Post", new Vector3(x, deck + 0.5f, -2.36f), new Vector3(0.04f, 1f, 0.04f), iron, parent);
            Block("Hand Rail", new Vector3(4.02f, deck + 1f, -2.36f), new Vector3(1.76f, 0.05f, 0.05f), trim, parent);
            Block("Mid Rail", new Vector3(4.02f, deck + 0.5f, -2.36f), new Vector3(1.76f, 0.03f, 0.03f), iron, parent);
        }

        // Familiar small things on the shelf: they look huge when the player is 10 % tall.
        static void ShelfProps()
        {
            // Real sizes, placed on the left of the arrival point (-1.1, 1.0), out of the view towards the box.
            var parent = Group("Shelf Props");
            const float top = 1.65f;
            var lying = Quaternion.Euler(0f, 35f, 90f);
            var screw = new Vector3(-0.95f, top + 0.012f, 1.3f);
            Shape("Screw", PrimitiveType.Cylinder, screw, new Vector3(0.024f, 0.05f, 0.024f), brass, parent, lying);
            Shape("Screw Head", PrimitiveType.Cylinder, screw + Vector3.up * 0.006f + lying * Vector3.up * 0.052f,
                new Vector3(0.05f, 0.008f, 0.05f), brass, parent, lying);

            Shape("Nut", PrimitiveType.Cylinder, new Vector3(-0.75f, top + 0.01f, 1.42f),
                new Vector3(0.06f, 0.01f, 0.06f), iron, parent);
            Shape("Nut", PrimitiveType.Cylinder, new Vector3(-0.7f, top + 0.008f, 1.3f),
                new Vector3(0.045f, 0.008f, 0.045f), brass, parent);
            Shape("Coin", PrimitiveType.Cylinder, new Vector3(-1.05f, top + 0.002f, 1.5f),
                new Vector3(0.025f, 0.002f, 0.025f), brass, parent);
            Mug(parent, new Vector3(-0.8f, top, 1.75f), mugRed);
            var book = Block("Lying Book", new Vector3(-1.2f, top + 0.016f, 1.75f), new Vector3(0.17f, 0.032f, 0.24f), bookBlue, parent);
            book.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);

            var pencilTurn = Quaternion.Euler(0f, -25f, 90f);
            var pencilCenter = new Vector3(-1.3f, top + 0.008f, 1.38f);
            Shape("Pencil", PrimitiveType.Cylinder, pencilCenter, new Vector3(0.016f, 0.09f, 0.016f), pencil, parent, pencilTurn);
            Shape("Pencil Tip", PrimitiveType.Cylinder, pencilCenter + pencilTurn * Vector3.up * 0.1f,
                new Vector3(0.008f, 0.012f, 0.008f), iron, parent, pencilTurn);
            Shape("Pencil Eraser", PrimitiveType.Cylinder, pencilCenter - pencilTurn * Vector3.up * 0.095f,
                new Vector3(0.017f, 0.008f, 0.017f), bookRed, parent, pencilTurn);
        }

        static void Vent()
        {
            var parent = Group("Vent Dressing");
            Block("Vent Shadow", new Vector3(-3.85f, 0.44f, 2.664f), new Vector3(0.84f, 0.76f, 0.01f), iron, parent);
            Block("Vent Lintel", new Vector3(-3.85f, 0.985f, 2.15f), new Vector3(1.24f, 0.03f, 0.03f), trim, parent);
        }

        static void ExitDoor()
        {
            var parent = Group("Exit Dressing");
            const float deck = 1.65f;
            Block("Door Panel", new Vector3(4.02f, deck + 0.93f, 2.385f), new Vector3(1.05f, 1.82f, 0.02f), door, parent);
            Block("Door Window", new Vector3(4.02f, deck + 1.45f, 2.372f), new Vector3(0.45f, 0.35f, 0.01f), glass, parent);
            Shape("Door Knob", PrimitiveType.Sphere, new Vector3(4.42f, deck + 0.95f, 2.36f), Vector3.one * 0.06f, brass, parent);
            Block("Exit Sign Back", new Vector3(4.02f, 3.82f, 2.39f), new Vector3(0.62f, 0.24f, 0.03f), signBack, parent);
        }

        // Scale console in front of the plate; its tilted dark face carries the mass readout.
        static void SignBoards()
        {
            var parent = Group("Scale Console");
            var tilt = Quaternion.Euler(ConsoleTilt, 0f, 0f);
            Block("Console Body", new Vector3(ConsoleFace.x, 0.17f, ConsoleFace.z + 0.03f), new Vector3(0.92f, 0.34f, 0.2f), iron, parent);
            var face = Block("Console Face", ConsoleFace, new Vector3(0.92f, 0.3f, 0.02f), signBoard, parent);
            face.transform.localRotation = tilt;
            var rim = Block("Console Rim", ConsoleFace + tilt * Vector3.forward * 0.012f, new Vector3(0.96f, 0.34f, 0.01f), trim, parent);
            rim.transform.localRotation = tilt;
        }

        // The rod that holds the dock becomes an iron pipe with a flange at the ceiling.
        static void DockHanger()
        {
            var hanger = GameObject.Find("Dock Hanger");
            if (hanger == null)
                return;

            hanger.GetComponent<MeshRenderer>().sharedMaterial = iron;
            var parent = Group("Dock Dressing");
            Vector3 top = hanger.transform.position + Vector3.up * (hanger.transform.localScale.y * 0.5f);
            // Fills the small gap between the rod and the ceiling.
            const float ceilingY = 4.30f;
            Block("Hanger Top", new Vector3(top.x, (top.y + ceilingY) * 0.5f, top.z),
                new Vector3(0.18f, ceilingY - top.y + 0.02f, 0.18f), iron, parent);
            Shape("Hanger Flange", PrimitiveType.Cylinder, new Vector3(top.x, 4.29f, top.z), new Vector3(0.36f, 0.01f, 0.36f),
                iron, parent);
        }

        // Kettlebell handle on the weight. Children scale with the body, so the handle grows and shrinks with it.
        static void Weight(Transform weight)
        {
            Block("Handle Post", new Vector3(-0.3f, 0.66f, 0f), new Vector3(0.14f, 0.36f, 0.16f), iron, weight);
            Block("Handle Post", new Vector3(0.3f, 0.66f, 0f), new Vector3(0.14f, 0.36f, 0.16f), iron, weight);
            Block("Handle Bar", new Vector3(0f, 0.86f, 0f), new Vector3(0.74f, 0.12f, 0.16f), iron, weight);
        }

        // Dark wooden frame on the box edges, so it reads as a crate.
        static void Crate(Transform box)
        {
            const float edge = 0.09f;
            const float half = 0.5f;
            foreach (float a in new[] { -half, half })
            {
                foreach (float b in new[] { -half, half })
                {
                    Block("Crate Edge", new Vector3(0f, a, b), new Vector3(1.02f, edge, edge), darkWood, box);
                    Block("Crate Edge", new Vector3(a, 0f, b), new Vector3(edge, 1.02f, edge), darkWood, box);
                    Block("Crate Edge", new Vector3(a, b, 0f), new Vector3(edge, edge, 1.02f), darkWood, box);
                }
            }
        }

        static Transform Group(string name) => new GameObject(name).transform;

        static GameObject Block(string name, Vector3 position, Vector3 size, Material material, Transform parent) =>
            Shape(name, PrimitiveType.Cube, position, size, material, parent);

        static GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 size, Material material,
            Transform parent, Quaternion? rotation = null)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localRotation = rotation ?? Quaternion.identity;
            shape.transform.localScale = size;
            var renderer = shape.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return shape;
        }
    }
}
