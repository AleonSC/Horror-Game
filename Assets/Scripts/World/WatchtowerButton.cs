using UnityEngine;

/// <summary>The big button at the top of the watchtower. Clicking it (via LampInteractor) turns on the floodlights.</summary>
public class WatchtowerButton : MonoBehaviour
{
    public Watchtower tower;

    public void Press()
    {
        if (tower != null) tower.Activate();
    }
}
