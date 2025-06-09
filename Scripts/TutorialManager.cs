using UnityEngine;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    [Tooltip("The GameObject that contains all the tutorial UI elements.")]
    public GameObject tutorialPanel;

    [Tooltip("How many seconds the tutorial panel should be visible for.")]
    public float displayDuration = 10f;

    void Start()
    {
        if (tutorialPanel != null)
        {
            // Make sure the panel is visible at the start
            tutorialPanel.SetActive(true);
            
            // Call the HideTutorial method after the displayDuration has passed
            Invoke(nameof(HideTutorial), displayDuration);
        }
        else
        {
            Debug.LogWarning("TutorialManager: No Tutorial Panel has been assigned in the Inspector.");
        }
    }

    /// <summary>
    /// Hides the tutorial panel.
    /// </summary>
    public void HideTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }
} 