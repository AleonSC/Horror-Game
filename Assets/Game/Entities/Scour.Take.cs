using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    // Taking the player (section 6.7): you're held, your gaze is dragged to it as it walks up to you, it takes
    // hold of you, a hole tears open behind it, and it backs into it, dragging you after it. The screen fades.
    public sealed partial class Scour
    {
        Vector3 takeStart, takeForward, approachFrom, portalCentre;
        float approachTime;
        bool caughtSent;

        void StartTaking()
        {
            var flow = player.GetComponent<CatchFlow>();
            if (flow != null && flow.Active) return;

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
            float tGrasp = approachTime, tPortal = tGrasp + s.graspTime, tDrag = tPortal + s.portalOpenTime;
            float eyeHeight = s.height * 0.94f * growth;
            body.SetEyes(Mathf.Lerp(1.2f, 3.2f, Mathf.Clamp01(t / tPortal)), s.spawnEyeGlow * 2f * Random.Range(0.85f, 1.15f), 0f, 0f, s.eyeLight, s.eyeLightRange);

            if (t < tGrasp)
            {
                // It walks up to you, slowly, lurching, its eyes on yours.
                float u = t / approachTime;
                float steps = Mathf.Max(2f, approachTime * 1.3f);
                float phase = u * steps;
                float lurch = Mathf.SmoothStep(0f, 1f, Mathf.Floor(phase) / steps + Mathf.SmoothStep(0f, 1f, phase - Mathf.Floor(phase)) / steps);
                Vector3 p = Vector3.Lerp(approachFrom, takeStart + takeForward * 0.9f, lurch);
                p.y = PlayerSense.GroundHeight(p) + Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * 0.06f;
                transform.position = p;
                body.Face(takeStart);
                body.Shudder(s.crescendoVibration * 0.15f);
                body.SetMouth(Mathf.Lerp(0.3f, 1.2f, u));
                if (u > 0.75f && body.Pose != ScourPose.Reach) body.SetPose(ScourPose.Reach, RandomSide(), false, 3f); // a hand coming up for you
                LookAtIt(eyeHeight, 2.2f, Mathf.Lerp(0.3f, 2.2f, u * u), dt);
                return;
            }

            if (t < tPortal)
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

            // A hole tears open in the air behind it.
            Vector3 hold = takeStart + takeForward * 0.75f;
            float toPlane = 0.75f + 0.55f;
            if (portal == null && !caughtSent)
            {
                portalCentre = hold + takeForward * 0.55f;
                portalCentre.y = PlayerSense.GroundHeight(hold) + s.height * 0.52f;
                portal = ScourPortal.Open(portalCentre, -takeForward, new Vector2(0.85f, 1.5f) * (s.height / 2.4f), s.portalOpenTime);
                Play(Sound.Portal, 1f, 0.8f, 20f);
            }
            if (t < tDrag)
            {
                body.Shudder(s.crescendoVibration * 0.3f);
                LookAtIt(eyeHeight, 6f, 3f, dt);
                return;
            }

            // It backs into the hole, dragging you after it, slowly and then all at once.
            float d = Mathf.Clamp01((t - tDrag) / Mathf.Max(s.dragTime, 0.01f));
            float pull = d * d * (3f - 2f * d) * d;
            float moved = pull * (toPlane - 0.18f); // (stopping just short: all you see is the black)
            Vector3 you = takeStart + takeForward * moved;
            you.y = Mathf.Lerp(takeStart.y, PlayerSense.GroundHeight(you), 0.5f) + 0.15f * d;
            player.transform.position = you;
            Vector3 it = takeStart + takeForward * (0.75f + moved);
            it.y = PlayerSense.GroundHeight(it);
            transform.position = it;
            body.Shudder(s.crescendoVibration * Mathf.Lerp(0.3f, 0.05f, d));

            var cam = player.Head;
            Quaternion look = Quaternion.LookRotation(portalCentre - cam.position);
            cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-4f * dt)) * Quaternion.Euler(Random.insideUnitSphere * Mathf.Lerp(2.5f, 0.6f, d));

            if (!caughtSent && d > 0.55f)
            {
                caughtSent = true;
                var flow = player.GetComponent<CatchFlow>();
                if (flow != null) flow.Begin(EntityKind.Scour, id);
            }
            if (d >= 1f)
            {
                // You're gone; so is it. The catch flow wakes you up somewhere else.
                if (portal != null) portal.Close();
                portal = null;
                body.SetVisible(false);
                State = ScourState.Hidden; // (no Hide(): the player stays held until the catch flow wakes them)
                timer = s.returnAfterCaught;
            }
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
