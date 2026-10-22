using UnityEngine;

/// <summary>The big button at the top of the watchtower. Clicking it (via LampInteractor) turns on the floodlights.</summary>
public class WatchtowerButton : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public Watchtower tower;

    public void Press()
    {
        if (tower != null) tower.Activate();
    }
}
