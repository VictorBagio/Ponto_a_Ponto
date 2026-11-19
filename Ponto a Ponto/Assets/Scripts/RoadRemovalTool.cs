using UnityEngine;

public class RoadRemovalTool : MonoBehaviour
{
    [SerializeField]
    private RoadGridSelector gridSelector;
    public void ActivateRemoval()
    {
        if (gridSelector != null)
            gridSelector.SetMode(2);
    }

    public void ActivateStopRemoval()
    {
        ActivateRemoval();
    }

    public void ActivateLineRemoval()
    {
        ActivateRemoval();
    }
}
