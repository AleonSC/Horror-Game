using UnityEngine;

/// <summary>
/// A cloud of loose needles and leaves, as a tree's crown is smashed apart: they burst out of the space the
/// boughs filled, tumble and flutter, get carried off on the wind (WeatherSystem), and slowly settle and
/// vanish. TreeFall spawns one per bough section when a tree crashes down.
/// </summary>
public static class LeafBurst
{
    static Material leafMaterial;

    public static void Spawn(Bounds area)
    {
        var go = new GameObject("Leaf Burst");
        go.transform.position = area.center;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // settings below can only change while stopped

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 13f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.025f; // they float down slowly
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        int count = Mathf.Clamp(Mathf.RoundToInt(area.size.x * area.size.y * area.size.z * 3f), 80, 450);
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area.size * 0.8f;

        // Fluttering, and carried off downwind.
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.3f;

        var weather = Object.FindAnyObjectByType<WeatherSystem>();
        Vector3 wind = weather != null ? weather.WindVector * (0.4f + weather.CurrentWind) * 1.2f : Vector3.zero;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(wind.x);
        velocity.y = new ParticleSystem.MinMaxCurve(0.05f);
        velocity.z = new ParticleSystem.MinMaxCurve(wind.z);

        var spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        // They shrink away at the end instead of popping out.
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (leafMaterial == null) leafMaterial = MaterialFactory.Lit(new Color(0.06f, 0.09f, 0.04f), 0.1f);
        renderer.sharedMaterial = leafMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        Object.Destroy(go, 15f);
    }
}
