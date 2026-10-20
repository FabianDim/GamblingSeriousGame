using UnityEngine;
using GamblingSeriousGame.UI;
using System.Collections.Generic;

public class AppNavigationManager : MonoBehaviour
{
    public enum NonSetupPageName { Game, Home, Resources, Intervention };
    [SerializeField] private GameObject petSelectionScreen;
    [SerializeField] private GameObject petCreationScreen;
    [SerializeField] private GameObject GamePlayScreen;
    [SerializeField] private GameObject interventionsScreen;
    [SerializeField] private GameObject homeScreen;
    [SerializeField] private GameObject resourcesScreen;
    private BasePageScreen gameplayDock;
    private BasePageScreen interventionsDock;
    private BasePageScreen homeDock;
    private BasePageScreen resourcesDock;

    List<GameObject> mostRecentPages = new List<GameObject>();

    public string SelectedAnimalId { get; private set; }

    private void OnEnable()
    {
        gameplayDock = GamePlayScreen != null ? GamePlayScreen.GetComponent<BasePageScreen>() : null;
        interventionsDock = interventionsScreen != null ? interventionsScreen.GetComponent<BasePageScreen>() : null;
        homeDock = homeScreen != null ? homeScreen.GetComponent<BasePageScreen>() : null;
        resourcesDock = resourcesScreen != null ? resourcesScreen.GetComponent<BasePageScreen>() : null;
        gameplayDock?.OnNavigate.AddListener(NavigateDock);
        interventionsDock?.OnNavigate.AddListener(NavigateDock);
        homeDock?.OnNavigate.AddListener(NavigateDock);
        resourcesDock?.OnNavigate.AddListener(NavigateDock);
    }

    private void OnDisable()
    {
        gameplayDock?.OnNavigate.RemoveListener(NavigateDock);
        interventionsDock?.OnNavigate.RemoveListener(NavigateDock);
        homeDock?.OnNavigate.RemoveListener(NavigateDock);
        resourcesDock?.OnNavigate.RemoveListener(NavigateDock);
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

    public void StartOnHomeScreen()
    {
        // Older scenes without the home shell retain their existing destination.
        NavigateDock(homeScreen != null ? "home" : "my-pet");
        if (petCreationScreen != null) petCreationScreen.SetActive(false);
        if (petSelectionScreen != null) petSelectionScreen.SetActive(false);
    }

    public void NavigateDock(string destination)
    {
        if (destination != "home" && destination != "interventions" && destination != "my-pet" && destination != "resources") return;
        var target = destination == "home" ? homeScreen : destination == "interventions" ? interventionsScreen
            : destination == "resources" ? resourcesScreen : GamePlayScreen;
        if (target == null)
        {
            Debug.LogWarning("Assign the " + destination + " screen on AppNavigationManager.", this);
            return;
        }
        if (GamePlayScreen != null && GamePlayScreen != target) GamePlayScreen.SetActive(false);
        if (interventionsScreen != null && interventionsScreen != target) interventionsScreen.SetActive(false);
        if (homeScreen != null && homeScreen != target) homeScreen.SetActive(false);
        if (resourcesScreen != null && resourcesScreen != target) resourcesScreen.SetActive(false);
        target.SetActive(true);
        target.GetComponent<BasePageScreen>()?.SelectDestination(destination);
    }

    public void ConfirmPetSelection(string animalId)
    {
        if (petSelectionScreen == null || petCreationScreen == null)
        {
            Debug.LogError("Assign both screens on AppNavigationManager.", this);
            return;
        }

        mostRecentPages.Add(petSelectionScreen);

        SelectedAnimalId = animalId;
        Debug.Log($"Selected animal ID: {animalId}");
        petCreationScreen.GetComponent<PetCustomizationScreen>()?.SetAnimal(animalId);
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

        FindAnyObjectByType<GameStateManager>()?.LoadSavedPet();
        StartOnHomeScreen();
    }

    public void BackButton()
    {
        if (mostRecentPages.Count == 0) return;
        GameObject previousPage = mostRecentPages[mostRecentPages.Count - 1];
        mostRecentPages.RemoveAt(mostRecentPages.Count - 1);
        petCreationScreen?.SetActive(false);
        previousPage.SetActive(true);
    }

    public void GeneralNavigation(Component from, NonSetupPageName to)
    {
        if (from == null)
        {
            Debug.LogWarning("A source screen component is required for navigation.", this);
            return;
        }

        var source = from.gameObject;
        if (source == GamePlayScreen)
            GeneralNavigation(NonSetupPageName.Game, to);
        else if (source == homeScreen)
            GeneralNavigation(NonSetupPageName.Home, to);
        else if (source == resourcesScreen)
            GeneralNavigation(NonSetupPageName.Resources, to);
        else if (source == interventionsScreen)
            GeneralNavigation(NonSetupPageName.Intervention, to);
        else
            Debug.LogWarning("The source component must belong to a screen assigned on AppNavigationManager.", from);
    }

    public void GeneralNavigation(NonSetupPageName from, NonSetupPageName to)
    {
        if (from == to) return;

        switch (to)
        {
            case NonSetupPageName.Game:
                NavigateDock("my-pet");
                break;
            case NonSetupPageName.Home:
                NavigateDock("home");
                break;
            case NonSetupPageName.Resources:
                NavigateDock("resources");
                break;
            case NonSetupPageName.Intervention:
                NavigateDock("interventions");
                break;
            default:
                Debug.LogWarning("Unknown navigation destination: " + to, this);
                break;
        }
    }
}
