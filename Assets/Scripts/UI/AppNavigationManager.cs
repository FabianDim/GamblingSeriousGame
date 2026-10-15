using UnityEngine;
using GamblingSeriousGame.UI;
using System.Collections.Generic;

public class AppNavigationManager : MonoBehaviour
{
    [SerializeField] private GameObject petSelectionScreen;
    [SerializeField] private GameObject petCreationScreen;
    [SerializeField] private GameObject GamePlayScreen;
    [SerializeField] private GameObject interventionsScreen;
    private BasePageScreen gameplayDock;
    private BasePageScreen interventionsDock;

    List<GameObject> mostRecentPages = new List<GameObject>();

    public string SelectedPetId { get; private set; }

    private void OnEnable()
    {
        gameplayDock = GamePlayScreen != null ? GamePlayScreen.GetComponent<BasePageScreen>() : null;
        interventionsDock = interventionsScreen != null ? interventionsScreen.GetComponent<BasePageScreen>() : null;
        gameplayDock?.OnNavigate.AddListener(NavigateDock);
        interventionsDock?.OnNavigate.AddListener(NavigateDock);
    }

    private void OnDisable()
    {
        gameplayDock?.OnNavigate.RemoveListener(NavigateDock);
        interventionsDock?.OnNavigate.RemoveListener(NavigateDock);
    }

    public void StartOnGameScreen()
    {
        if (GamePlayScreen == null || petCreationScreen == null)
        {
            Debug.LogError("Assign the gameplay screen.", this);
            return;
        }

        NavigateDock("my-pet");
        petCreationScreen.SetActive(false);
    }

    public void NavigateDock(string destination)
    {
        if (destination != "interventions" && destination != "my-pet") return;
        var target = destination == "interventions" ? interventionsScreen : GamePlayScreen;
        if (target == null)
        {
            Debug.LogWarning("Assign the " + destination + " screen on AppNavigationManager.", this);
            return;
        }
        if (GamePlayScreen != target) GamePlayScreen?.SetActive(false);
        if (interventionsScreen != target) interventionsScreen?.SetActive(false);
        target.SetActive(true);
        target.GetComponent<BasePageScreen>()?.SelectDestination(destination);
    }

    public void ConfirmPetSelection(string petId)
    {
        if (petSelectionScreen == null || petCreationScreen == null)
        {
            Debug.LogError("Assign both screens on AppNavigationManager.", this);
            return;
        }

        mostRecentPages.Add(petSelectionScreen);

        SelectedPetId = petId;
        Debug.Log($"Pet ID is set as: {petId}");
        petCreationScreen.GetComponent<PetCustomizationScreen>()?.SetPet(petId);
        petSelectionScreen.SetActive(false);
        petCreationScreen.SetActive(true);
    }

    public void ConfirmPetCustomisation()
    {
        if (GamePlayScreen == null || petCreationScreen == null)
        {
            Debug.LogError("Assign the gameplay screen.", this);
            return;
        }

        NavigateDock("my-pet");
        petCreationScreen.SetActive(false);
    }

    public void BackButton()
    {
        if (mostRecentPages.Count == 0) return;
        GameObject previousPage = mostRecentPages[mostRecentPages.Count - 1];
        mostRecentPages.RemoveAt(mostRecentPages.Count - 1);
        petCreationScreen?.SetActive(false);
        previousPage.SetActive(true);
    }
}
