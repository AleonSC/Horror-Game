using System.Collections;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// A photo's polaroid (section 6.3): it slides out at the bottom right of the view, blank, and develops over a
    /// few seconds, then tucks away (the inventory keeps it).
    /// </summary>
    public sealed class PolaroidPopup : MonoBehaviour
    {
        const float Picture = 220f;

        VisualElement card, picture, milk;
        System.IDisposable sub;
        Coroutine showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<PolaroidPopup>() == null)
                root.gameObject.AddComponent<PolaroidPopup>();
        }

        void Start()
        {
            var ui = UiRoot.Get();
            if (ui == null) return;
            card = MakeCard(Picture, out picture, out milk);
            card.style.position = Position.Absolute;
            card.style.right = 60;
            card.style.bottom = 40;
            card.style.display = DisplayStyle.None;
            ui.Hud.Add(card);
            if (Services.TryGet<EventBus>(out var events)) sub = events.Subscribe<PhotoTaken>(OnPhoto);
        }

        void OnDestroy() => sub?.Dispose();

        /// <summary>A polaroid card: white with a deep bottom edge, the picture in it, and a "milk" layer over the
        /// picture (opaque while it's still developing).</summary>
        public static VisualElement MakeCard(float size, out VisualElement picture, out VisualElement milk)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.backgroundColor = new Color(0.86f, 0.85f, 0.8f, 1f);
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = size * 0.065f;
            card.style.paddingBottom = size * 0.24f;
            UiStyle.Rounded(card, 2f);
            picture = new VisualElement { pickingMode = PickingMode.Ignore };
            picture.style.width = size;
            picture.style.height = size;
            picture.style.backgroundColor = Color.black;
            card.Add(picture);
            milk = UiStyle.Fill(new VisualElement { pickingMode = PickingMode.Ignore });
            milk.style.backgroundColor = new Color(0.16f, 0.17f, 0.16f, 1f);
            picture.Add(milk);
            return card;
        }

        void OnPhoto(PhotoTaken e)
        {
            if (card == null || !Services.TryGet<Polaroids>(out var polaroids) || e.Index >= polaroids.Photos.Count) return;
            if (showing != null) StopCoroutine(showing);
            showing = StartCoroutine(Show(polaroids.Photos[e.Index]));
        }

        IEnumerator Show(Polaroids.Photo photo)
        {
            float develop = Services.TryGet<GameSetup>(out var setup) && setup.Get<ItemsConfig>() != null ? setup.Get<ItemsConfig>().settings.photoDevelopTime : 8f;
            picture.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(photo.Picture));
            milk.style.opacity = 1f;
            card.style.display = DisplayStyle.Flex;
            card.style.rotate = new Rotate(new Angle(Random.Range(-6f, -2f), AngleUnit.Degree));

            // Out of the camera: it slides up from below the edge.
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
                card.style.translate = new Translate(0f, (1f - k) * 360f);
                yield return null;
            }
            card.style.translate = new Translate(0f, 0f);
            // Developing: the picture comes up out of the murk, slow at first.
            for (float t = 0f; t < develop; t += Time.deltaTime)
            {
                float k = t / develop;
                milk.style.opacity = 1f - k * k * (3f - 2f * k);
                yield return null;
            }
            milk.style.opacity = 0f;
            yield return new WaitForSeconds(2.5f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                card.style.translate = new Translate(0f, Mathf.SmoothStep(0f, 1f, t / 0.8f) * 360f);
                yield return null;
            }
            card.style.display = DisplayStyle.None;
            showing = null;
        }
    }
}
