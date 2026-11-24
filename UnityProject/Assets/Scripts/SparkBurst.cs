using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Small pooled spark bursts made of tiny cubes. Avoids the Particle System module,
    /// which is not part of the project's packages.
    /// </summary>
    public sealed class SparkBurst : MonoBehaviour
    {
        const float MinSpeed = 0.6f;
        const float MaxSpeed = 1.8f;
        const float MinLife = 0.25f;
        const float MaxLife = 0.5f;
        const float MinSize = 0.015f;
        const float MaxSize = 0.035f;
        const float Spread = 0.04f;
        static readonly int ColorProperty = Shader.PropertyToID("_Color");

        Transform[] sparks;
        Renderer[] renderers;
        Vector3[] velocities;
        float[] ages;
        float[] lifetimes;
        float[] sizes;
        MaterialPropertyBlock properties;
        int next;

        public int Alive { get; private set; }

        public void Create(int capacity, Material material)
        {
            sparks = new Transform[capacity];
            renderers = new Renderer[capacity];
            velocities = new Vector3[capacity];
            ages = new float[capacity];
            lifetimes = new float[capacity];
            sizes = new float[capacity];
            properties = new MaterialPropertyBlock();

            for (int i = 0; i < capacity; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.name = "Spark";
                Destroy(spark.GetComponent<Collider>());
                spark.transform.SetParent(transform, false);
                var renderer = spark.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                spark.SetActive(false);
                sparks[i] = spark.transform;
                renderers[i] = renderer;
            }
        }

        public void Emit(Vector3 point, Color color, int count)
        {
            properties.SetColor(ColorProperty, color);
            for (int n = 0; n < count; n++)
            {
                int i = next;
                next = (next + 1) % sparks.Length;
                if (!sparks[i].gameObject.activeSelf)
                    Alive++;

                Vector3 direction = Random.onUnitSphere;
                velocities[i] = direction * Random.Range(MinSpeed, MaxSpeed);
                ages[i] = 0f;
                lifetimes[i] = Random.Range(MinLife, MaxLife);
                sizes[i] = Random.Range(MinSize, MaxSize);
                sparks[i].SetPositionAndRotation(point + direction * Spread, Random.rotation);
                sparks[i].localScale = Vector3.one * sizes[i];
                renderers[i].SetPropertyBlock(properties);
                sparks[i].gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (Alive == 0)
                return;

            float delta = Time.deltaTime;
            for (int i = 0; i < sparks.Length; i++)
            {
                Transform spark = sparks[i];
                if (!spark.gameObject.activeSelf)
                    continue;

                ages[i] += delta;
                float progress = ages[i] / lifetimes[i];
                if (progress >= 1f)
                {
                    spark.gameObject.SetActive(false);
                    Alive--;
                    continue;
                }

                velocities[i] *= 1f - 2.5f * delta;
                spark.position += velocities[i] * delta;
                spark.localScale = Vector3.one * (sizes[i] * (1f - progress));
            }
        }
    }
}
