using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    // Taking the player (section 6.7): you're held, your gaze is dragged to it as it walks up to you, it takes
    // hold of you and lifts you up to its eyes, and stares, while the world round you comes apart into motes
    // and the dark closes in. Then black.
    public sealed partial class Scour
    {
        Vector3 takeStart, takeForward, approachFrom;
        float approachTime, liftBy;
        bool caughtSent;
        WorldMotes motes;

        void StartTaking()
        {
            var flow = player.GetComponent<CatchFlow>();
            if (flow != null && !flow.CanTake) return;
            if (PlayerSense.OutOfReach(player)) return; // (up the watchtower: it can't get hold of you from the ground)

            peekTree = null;
            player.SetHeld(true);
            takeStart = player.transform.position;
            Vector3 away = transform.position - takeStart;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.ProjectOnPlane(player.Head.forward, Vector3.up);
            if (away.sqrMagnitude < 0.001f) away = player.transform.forward;
            takeForward = away.normalized;
            float dist = Mathf.Max(away.magnitude, s.approachMinDistance);
            approachFrom = takeStart + takeForward * dist;
            approachFrom = OutOfTrunks(approachFrom);
            approachFrom.y = PlayerSense.GroundHeight(approachFrom);
            transform.position = approachFrom;
            approachTime = Mathf.Clamp((dist - 0.9f) / Mathf.Max(s.approachSpeed, 0.1f), 2f, 6f);
            growth = Mathf.Max(growth, 1f);
            body.SetGrowth(growth);
            body.SetVisible(true);
            body.Face(takeStart);
            body.SetPose(ScourPose.Neutral, 1, false, 2f);
            caughtSent = false;
            Play(Sound.Shriek, 0.5f, 0.7f, 20f); // low and quiet: it has you
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Breath, MixerNames.Player, 0.9f, 0.8f);
            SetState(ScourState.Taking, "taking the player");
        }

        void UpdateTaking()
        {
            timer += Time.deltaTime;
            float dt = Time.deltaTime;
            float t = timer;
            float tGrasp = approachTime, tLift = tGrasp + s.graspTime, tDissolve = tLift + s.liftTime;
            float eyeHeight = s.height * 0.94f * growth;
            body.SetEyes(Mathf.Lerp(1.2f, 3.2f, Mathf.Clamp01(t / tLift)), s.spawnEyeGlow * 2f * Random.Range(0.85f, 1.15f), 0f, 0f, s.eyeLight, s.eyeLightRange);

            if (t < tGrasp)
            {
                // It walks up to you, slowly, lurching, its eyes on yours.
                float u = t / approachTime;
                float steps = Mathf.Max(2f, approachTime * 1.3f);
                float phase = u * steps;
                float lurch = Mathf.SmoothStep(0f, 1f, Mathf.Floor(phase) / steps + Mathf.SmoothStep(0f, 1f, phase - Mathf.Floor(phase)) / steps);
                Vector3 p = Vector3.Lerp(approachFrom, takeStart + takeForward * 0.9f, lurch);
                p = OutOfTrunks(p);
                p.y = PlayerSense.GroundHeight(p) + Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * 0.06f;
                transform.position = p;
                body.Face(takeStart);
                body.Shudder(s.crescendoVibration * 0.15f);
                body.SetMouth(Mathf.Lerp(0.3f, 1.2f, u));
                if (u > 0.75f && body.Pose != ScourPose.Reach) body.SetPose(ScourPose.Reach, RandomSide(), false, 3f); // a hand coming up for you
                LookAtIt(eyeHeight, 2.2f, Mathf.Lerp(0.3f, 2.2f, u * u), dt);
                return;
            }

            if (t < tLift)
            {
                // It takes hold of you, its face right up to yours.
                if (t - dt < tGrasp)
                {
                    Play(Sound.Moan, 0.8f, 0.6f, 15f);
                    body.SetPose(ScourPose.Lunge, 1, false, 6f); // both hands, its face right up to yours
                }
                body.SetMouth(1.6f);
                float u = (t - tGrasp) / s.graspTime;
                Vector3 p = takeStart + takeForward * Mathf.Lerp(0.9f, 0.75f, u);
                p.y = PlayerSense.GroundHeight(p);
                transform.position = p;
                body.Shudder(s.crescendoVibration * 0.25f);
                LookAtIt(eyeHeight, 6f, 2.6f, dt);
                return;
            }

            // It lifts you up, off your feet, to its eyes.
            Vector3 hold = takeStart + takeForward * 0.75f;
            var look = Services.TryGet<LookController>(out var lc) ? lc : null;
            if (t - dt < tLift)
            {
                Play(Sound.Moan, 0.7f, 0.5f, 12f);
                liftBy = Mathf.Clamp(eyeHeight + s.liftAboveEyes - (player.Head.position.y - player.transform.position.y), 0.2f, 2.5f);
            }
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - tLift) / Mathf.Max(0.01f, s.liftTime)));
            float bob = Mathf.Sin(t * 1.3f) * 0.03f * up;
            player.transform.position = takeStart + Vector3.up * (liftBy * up + bob) + takeForward * (0.15f * up);
            transform.position = new Vector3(hold.x, PlayerSense.GroundHeight(hold), hold.z);
            body.Face(player.transform.position);
            body.SetMouth(Mathf.Lerp(1.6f, 0.4f, up)); // (it goes quiet, and just looks)
            if (t < tDissolve)
            {
                body.Shudder(s.crescendoVibration * Mathf.Lerp(0.25f, 0.08f, up));
                LookAtIt(eyeHeight, 6f, Mathf.Lerp(2.6f, 1.2f, up), dt);
                return;
            }

            // The world round you comes apart, from the far edge in, and the dark closes in after it; at last
            // there's only its eyes. Then black.
            if (motes == null && !caughtSent)
            {
                motes = WorldMotes.Begin(player.Head.position, s.dissolveReach, s.worldMotes, s.dissolveTime);
                Play(Sound.Portal, 0.6f, 0.45f, 25f);
                if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Breath, MixerNames.Player, 0.7f, 0.65f);
            }
            float d = Mathf.Clamp01((t - tDissolve) / Mathf.Max(0.5f, s.dissolveTime));
            if (look != null && motes != null)
            {
                look.Void = Mathf.Clamp01(d * 2.5f);
                look.VoidSight = motes.Sight;
            }
            body.Shudder(s.crescendoVibration * 0.05f);
            LookAtIt(eyeHeight, 8f, Mathf.Lerp(1.2f, 0.4f, d), dt);

            if (!caughtSent && d > 0.8f)
            {
                caughtSent = true;
                var flow = player.GetComponent<CatchFlow>();
                if (flow != null) flow.Begin(EntityKind.Scour, id);
            }
            bool black = Services.TryGet<ScreenFade>(out var fade) && fade.Alpha >= 0.99f;
            if (caughtSent && (black || t > tDissolve + s.dissolveTime + 4f))
            {
                // You're gone; so is it. The catch flow wakes you up somewhere else (and the world comes back).
                if (motes != null) Destroy(motes.gameObject);
                motes = null;
                liftBy = 0f;
                body.SetVisible(false);
                State = ScourState.Hidden; // (no Hide(): the player stays held until the catch flow wakes them)
                timer = s.returnAfterCaught;
            }
        }

        void StopWorldGoing()
        {
            if (motes != null) Destroy(motes.gameObject);
            motes = null;
            liftBy = 0f;
            if (Services.TryGet<LookController>(out var look)) look.Void = 0f;
        }

        // Drags the held player's gaze onto its face (pull: how fast), trembling with fear (degrees).
        void LookAtIt(float eyeHeight, float pull, float fear, float dt)
        {
            var cam = player.Head;
            Vector3 head = transform.position + Vector3.up * eyeHeight;
            Quaternion look = Quaternion.LookRotation(head - cam.position);
            cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-pull * dt));
            float tt = Time.time;
            Vector3 shake = Random.insideUnitSphere * fear * 0.35f
                            + new Vector3(Mathf.PerlinNoise(tt * 6f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, tt * 6f) - 0.5f, 0f) * fear * 1.6f;
            cam.rotation *= Quaternion.Euler(shake);
        }
    }
}
